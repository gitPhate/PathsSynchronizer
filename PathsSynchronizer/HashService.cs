using Microsoft.Win32.SafeHandles;
using PathsSynchronizer.Hashing;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PathsSynchronizer
{
    public class HashService(ServiceOptions options, IHashProvider hashProvider)
    {
        public async Task<DirectoryHash> ScanDirectoryAndHashAsync(string rootPath, IProgress<HashProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            ConcurrentBag<FileHash> index = [];
            int filesHashed = 0;
            int filesRead = 0;
            long bytesHashed = 0;

            void reportProgress()
            {
                int read = Volatile.Read(ref filesRead);
                int hashed = Volatile.Read(ref filesHashed);
                long bytes = Volatile.Read(ref bytesHashed);

                progress?.Report(new HashProgress(read, hashed, bytes));
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

            Task[] workers =
                Enumerable
                    .Range(0, options.WorkerCount)
                    .Select(_ => 
                        ConsumerWorkerAsync
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
                            cancellationToken
                        )
                    )
                    .ToArray();

            await ProducerAsync
            (
                rootPath,
                channel.Writer,
                x =>
                {
                    Interlocked.Increment(ref filesRead);
                    reportProgress();
                },
                cancellationToken
            )
            .ConfigureAwait(false);
            
            channel.Writer.Complete();
            await Task.WhenAll(workers).ConfigureAwait(false);

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

        public async Task<FileHash> HashFileAsync(string path)
        {
            MemoryPool<byte> bufferPool = MemoryPool<byte>.Shared;
            using SemaphoreSlim ioSemaphore = new(1);
            FileHash? hash = await HashFileAsync(GetFileTask(path), bufferPool, ioSemaphore, default).ConfigureAwait(false);
            return hash!;
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
                                .HashFileAsync(task.Path, bufferPool, cancellationToken)
                                .ConfigureAwait(false);
                    }
                    finally
                    {
                        ioSemaphore.Release();
                    }
                }
                else
                {
                    // Large file: compute sampled hashes
                    DataHash[] sampleHashes = new DataHash[options.SampleCount];
                    using FileStream fs = new(task.Path, FileMode.Open, FileAccess.Read, FileShare.Read, options.SampleBlockSize, useAsync: true);
                    SafeFileHandle handle = fs.SafeFileHandle;

                    var sampleTasks = Enumerable.Range(0, options.SampleCount).Select(async i =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        long offset = ComputeOffset(i, options.SampleCount, task.Length);
                        IMemoryOwner<byte> buf = bufferPool.Rent(options.SampleBlockSize);
                        try
                        {
                            await ioSemaphore
                                .WaitAsync(cancellationToken)
                                .ConfigureAwait(false);

                            try
                            {
                                Memory<byte> buffer = buf.Memory;
                                int read = await RandomAccess.ReadAsync(handle, buf.Memory, offset, cancellationToken).ConfigureAwait(false);

                                if (read < options.SampleBlockSize)
                                {
                                    buffer = buf.Memory.Slice(0, read);
                                }

                                sampleHashes[i] =
                                    await hashProvider
                                        .HashMemoryAsync(buffer, cancellationToken)
                                        .ConfigureAwait(false);
                            }
                            finally
                            {
                                ioSemaphore.Release();
                            }
                        }
                        finally
                        {
                            buf.Dispose();
                        }
                    })
                    .ToArray();

                    await Task.WhenAll(sampleTasks).ConfigureAwait(false);

                    fileHash = new FileHash(task.Path, sampleHashes);
                }

                return fileHash;
            }
            catch (OperationCanceledException) { return null; }
        }


        private long ComputeOffset(int index, int total, long fileSize)
        {
            if (fileSize <= options.SampleBlockSize) return 0;
            double fraction = (double)index / (total - 1);
            return (long)(fraction * Math.Max(0, fileSize - options.SampleBlockSize));
        }

        private static async Task ProducerAsync(string rootPath, ChannelWriter<FileTask> writer, Action<long>? onFileDiscovered, CancellationToken cancellationToken)
        {
            try
            {
                foreach (string path in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    FileTask fileTask = GetFileTask(path);
                    onFileDiscovered?.Invoke(fileTask.Length);

                    await writer.WriteAsync(fileTask, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }

        private static FileTask GetFileTask(string path)
        {
            return new FileTask(path, new FileInfo(path).Length);
        }
    }
}
