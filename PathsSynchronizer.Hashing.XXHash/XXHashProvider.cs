using System.Buffers;
using System.IO.Hashing;

namespace PathsSynchronizer.Hashing.XXHash
{
    public class XXHashProvider : IHashProvider
    {
        public async ValueTask<FileHash> HashFileAsync(string path, MemoryPool<byte> pool, CancellationToken cancellationToken = default)
        {
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, // 80KB buffer
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            XxHash128 hasher = new();
            await hasher.AppendAsync(fs, cancellationToken).ConfigureAwait(false);
            return new(path, new DataHash(hasher.GetCurrentHash())); // 16 bytes (128 bits)
        }

        public ValueTask<DataHash> HashMemoryAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            XxHash128 hasher = new();
            hasher.Append(buffer.Span);
            return new ValueTask<DataHash>(new DataHash(hasher.GetCurrentHash()));
        }
    }
}
