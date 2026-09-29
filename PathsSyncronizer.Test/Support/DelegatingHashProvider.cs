using PathsSynchronizer.Hashing;
using System.Buffers;

namespace PathsSyncronizer.Test.Support
{
    public sealed class DelegatingHashProvider(IHashProvider inner) : IHashProvider
    {
        public Func<string, CancellationToken, Task>? BeforeHashFile { get; set; }

        public async ValueTask<FileHash> HashFileAsync(string path, long length, MemoryPool<byte> pool, int bufferSize, CancellationToken cancellationToken = default)
        {
            if (BeforeHashFile is not null)
            {
                await BeforeHashFile(path, cancellationToken);
            }

            return await inner.HashFileAsync(path, length, pool, bufferSize, cancellationToken);
        }

        public ValueTask<DataHash> HashMemoryAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            inner.HashMemoryAsync(buffer, cancellationToken);
    }
}
