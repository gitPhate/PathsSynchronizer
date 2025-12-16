using PathsSynchronizer.Hashing;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PathsSynchronizer
{
    public record ServiceOptions(int SampleCount, int SampleBlockSize, long FullHashThreshold, int ProducerChannelCapacity, int WorkerCount, int IOConcurrency)
    {
        public static ServiceOptions Default => new(16, 1 * 1024 * 1024, 100L * 1024 * 1024, 4096, Environment.ProcessorCount, 32);
    }

    public class Service(ServiceOptions options, IHashProvider hashProvider)
    {
        public async Task<IReadOnlyDictionary<string, FileHash>> ScanDirectoryAndHashAsync(string rootPath, CancellationToken cancellationToken = default)
        {
            ConcurrentDictionary<string, FileHash> index = new();

            Channel<FileTask> channel =
                Channel.CreateBounded<FileTask>(new BoundedChannelOptions(options.ProducerChannelCapacity)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = true
                });

            using SemaphoreSlim ioSemaphore = new(options.IOConcurrency);

            Task[] workers =
                Enumerable
                    .Range(0, options.WorkerCount)
                    .Select(_ => ConsumerWorkerAsync(channel.Reader, ioSemaphore, index, cancellationToken))
                    .ToArray();

            await ProducerAsync(rootPath, channel.Writer, cancellationToken).ConfigureAwait(false);
            channel.Writer.Complete();
            await Task.WhenAll(workers).ConfigureAwait(false);

            return index;
        }

        private async Task ConsumerWorkerAsync(ChannelReader<FileTask> reader, SemaphoreSlim ioSemaphore, ConcurrentDictionary<string, FileHash> index, CancellationToken cancellationToken)
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

                    index.TryAdd(task.Path, fileHash);
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
                    IHash[] sampleHashes = new IHash[options.SampleCount];
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
                                using FileStream fs = new(task.Path, FileMode.Open, FileAccess.Read, FileShare.Read, options.SampleBlockSize, useAsync: true);
                                fs.Seek(offset, SeekOrigin.Begin);
                                int read = await fs.ReadAsync(buf.Memory, cancellationToken).ConfigureAwait(false);

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

                    fileHash = new FileHash(sampleHashes);
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

        private static async Task ProducerAsync(string rootPath, ChannelWriter<FileTask> writer, CancellationToken cancellationToken)
        {
            try
            {
                foreach (string path in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await writer.WriteAsync(GetFileTask(path), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }

        private static FileTask GetFileTask(string path)
        {
            return new FileTask(path, new FileInfo(path).Length);
        }
    }

    readonly record struct FileTask(string Path, long Length);
}
