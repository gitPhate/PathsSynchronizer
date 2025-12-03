using PathsSynchronizer.Core.Hashing;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core
{
    public record ServiceOptions(int SampleCount, int SampleBlockSize, long FullHashThreshold, int ProducerChannelCapacity, int WorkerCount, int IOConcurrency)
    {
        public static ServiceOptions Default => new(16, 1 * 1024 * 1024, 100L * 1024 * 1024, 4096, Environment.ProcessorCount, 32);
    }

    internal class Service(ServiceOptions options, IHashProvider hashProvider)
    {
        public async Task<IReadOnlyDictionary<string, FileHash>> ScanAsync(string rootPath, CancellationToken cancellationToken)
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

                        index.TryAdd(task.Path, fileHash);
                    }
                    catch (OperationCanceledException) { return; }
                }
            }
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
                    var fi = new FileInfo(path);
                    await writer.WriteAsync(new FileTask(path, fi.Length), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
        }
    }

    readonly record struct FileTask(string Path, long Length);
}
