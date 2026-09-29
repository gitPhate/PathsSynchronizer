using Microsoft.Win32.SafeHandles;
using PathsSynchronizer.Hashing;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PathsSynchronizer
{
    public class HashService(ServiceOptions options, IHashProvider hashProvider)
    {
        private const long ProgressIntervalMs = 100;
        private const int ThreadPoolHeadroom = 4;

        /// <summary>
        /// Raises the process-wide thread pool minimum so blocked workers do not starve other continuations.
        /// Never lowers the current value. Call once at startup; the library never calls it on its own.
        /// </summary>
        public static void EnsureThreadPoolCapacity(ServiceOptions options)
        {
            ThreadPool.GetMinThreads(out int workerThreads, out int completionPortThreads);
            int required = options.WorkerCount + ThreadPoolHeadroom;

            if (workerThreads < required)
            {
                ThreadPool.SetMinThreads(required, completionPortThreads);
            }
        }

        public async Task<DirectoryHash> ScanDirectoryAndHashAsync(string rootPath, IProgress<HashProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            ConcurrentBag<FileHash> index = [];
            int filesHashed = 0;
            int filesRead = 0;
            long bytesHashed = 0;

            long progressStart = Stopwatch.GetTimestamp();
            long lastReportMs = 0;

            void reportProgress(bool force = false)
            {
                if (progress is null)
                {
                    return;
                }

                if (!force)
                {
                    long now = (long)Stopwatch.GetElapsedTime(progressStart).TotalMilliseconds;
                    long last = Volatile.Read(ref lastReportMs);
                    if (now - last < ProgressIntervalMs || Interlocked.CompareExchange(ref lastReportMs, now, last) != last)
                    {
                        return;
                    }
                }

                int read = Volatile.Read(ref filesRead);
                int hashed = Volatile.Read(ref filesHashed);
                long bytes = Volatile.Read(ref bytesHashed);

                progress.Report(new HashProgress(read, hashed, bytes));
            }

            Channel<FileTask> channel =
                Channel
                    .CreateBounded<FileTask>(new BoundedChannelOptions(options.ProducerChannelCapacity)
                    {
                        FullMode = BoundedChannelFullMode.Wait,
                        SingleReader = false,
                        SingleWriter = true
                    });

            using SemaphoreSlim ioSemaphore = new(options.IOConcurrency);

            // Cancelled when a worker fails, so the producer does not block forever on a channel nobody drains
            using CancellationTokenSource abortCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            CancellationToken scanToken = abortCts.Token;

            async Task runWorkerAsync()
            {
                try
                {
                    await ConsumerWorkerAsync
                    (
                        channel.Reader,
                        ioSemaphore,
                        index,
                        x =>
                        {
                            Interlocked.Increment(ref filesHashed);
                            Interlocked.Add(ref bytesHashed, x);
                            reportProgress();
                        },
                        scanToken
                    )
                    .ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    abortCts.Cancel();
                    throw;
                }
            }

            Task[] workers =
                Enumerable
                    .Range(0, options.WorkerCount)
                    .Select(_ => runWorkerAsync())
                    .ToArray();

            Exception? producerException = null;
            try
            {
                await ProducerAsync
                (
                    rootPath,
                    channel.Writer,
                    x =>
                    {
                        Interlocked.Increment(ref filesRead);
                        reportProgress();
                    },
                    scanToken
                )
                .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                producerException = ex;
            }
            finally
            {
                channel.Writer.TryComplete(producerException);
            }

            try
            {
                await Task.WhenAll(workers).ConfigureAwait(false);
            }
            catch
            {
                // Surface the worker's own failure rather than the cancellation it triggered in its siblings
                Task? faulted = workers.FirstOrDefault(t => t.IsFaulted);
                if (faulted is not null)
                {
                    await faulted.ConfigureAwait(false);
                }

                throw;
            }

            reportProgress(force: true);

            return new DirectoryHash(rootPath, index.ToArray());
        }

        private async Task ConsumerWorkerAsync(ChannelReader<FileTask> reader, SemaphoreSlim ioSemaphore, ConcurrentBag<FileHash> index, Action<long>? onFileHashed, CancellationToken cancellationToken)
        {
            MemoryPool<byte> bufferPool = MemoryPool<byte>.Shared;

            while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (reader.TryRead(out FileTask task))
                {
                    FileHash? fileHash = await HashFileAsync(task, bufferPool, ioSemaphore, cancellationToken).ConfigureAwait(false);
                    if (fileHash is null)
                    {
                        continue;
                    }

                    index.Add(fileHash);
                    onFileHashed?.Invoke(task.Length);
                }
            }
        }

        public async Task<FileHash> HashFileAsync(string path, CancellationToken cancellationToken = default)
        {
            MemoryPool<byte> bufferPool = MemoryPool<byte>.Shared;
            using SemaphoreSlim ioSemaphore = new(1);
            FileHash? hash = await HashFileAsync(new FileTask(path, new FileInfo(path).Length), bufferPool, ioSemaphore, cancellationToken).ConfigureAwait(false);
            return hash ?? throw new OperationCanceledException(cancellationToken);
        }

        private async Task<FileHash?> HashFileAsync(FileTask task, MemoryPool<byte> bufferPool, SemaphoreSlim ioSemaphore, CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                FileHash fileHash;

                if (task.Length <= options.FullHashThreshold)
                {
                    await ioSemaphore
                        .WaitAsync(cancellationToken)
                        .ConfigureAwait(false);

                    try
                    {
                        fileHash =
                            await hashProvider
                                .HashFileAsync(task.Path, bufferPool, options.ReadBufferSize, cancellationToken)
                                .ConfigureAwait(false);
                    }
                    finally
                    {
                        ioSemaphore.Release();
                    }
                }
                else
                {
                    // Large file: compute sampled hashes, reading samples in ascending offset order
                    DataHash[] sampleHashes = new DataHash[options.SampleCount];
                    using SafeFileHandle handle = File.OpenHandle(task.Path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.Asynchronous | FileOptions.RandomAccess);
                    using IMemoryOwner<byte> buf = bufferPool.Rent(options.SampleBlockSize);

                    for (int i = 0; i < options.SampleCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        long offset = ComputeOffset(i, options.SampleCount, task.Length);
                        Memory<byte> buffer = buf.Memory.Slice(0, options.SampleBlockSize);

                        await ioSemaphore
                            .WaitAsync(cancellationToken)
                            .ConfigureAwait(false);

                        try
                        {
                            int read = await RandomAccess.ReadAsync(handle, buffer, offset, cancellationToken).ConfigureAwait(false);

                            if (read < options.SampleBlockSize)
                            {
                                buffer = buffer.Slice(0, read);
                            }
                        }
                        finally
                        {
                            ioSemaphore.Release();
                        }

                        sampleHashes[i] =
                            await hashProvider
                                .HashMemoryAsync(buffer, cancellationToken)
                                .ConfigureAwait(false);
                    }

                    fileHash = new FileHash(task.Path, sampleHashes);
                }

                return fileHash;
            }
            catch (OperationCanceledException) { return null; }
        }


        private long ComputeOffset(int index, int total, long fileSize)
        {
            if (fileSize <= options.SampleBlockSize) return 0;
            if (total <= 1) return 0;
            double fraction = (double)index / (total - 1);
            return (long)(fraction * Math.Max(0, fileSize - options.SampleBlockSize));
        }

        private static async Task ProducerAsync(string rootPath, ChannelWriter<FileTask> writer, Action<long>? onFileDiscovered, CancellationToken cancellationToken)
        {
            await TraverseAsync(new DirectoryInfo(rootPath)).ConfigureAwait(false);

            async Task TraverseAsync(DirectoryInfo directory)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Files in THIS directory
                foreach (var file in directory.EnumerateFiles()
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    onFileDiscovered?.Invoke(file.Length);

                    await writer.WriteAsync(new(file.FullName, file.Length), cancellationToken).ConfigureAwait(false);
                }

                // Subdirectories
                foreach (var subDir in directory.EnumerateDirectories()
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
                {
                    await TraverseAsync(subDir).ConfigureAwait(false);
                }
            }
        }
    }
}
