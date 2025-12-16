using System.Buffers;
using System.IO.Hashing;

namespace PathsSynchronizer.Hashing.XXHash
{
    public class XXHashProvider : IHashProvider
    {
        public async ValueTask<FileHash> HashFileAsync(string path, MemoryPool<byte> pool, CancellationToken cancellationToken = default)
        {
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            XxHash128 hasher = new();
            await hasher.AppendAsync(fs, cancellationToken).ConfigureAwait(false);
            return new(new XXHash(hasher.GetCurrentHash())); // 16 bytes (128 bits)
        }

        public ValueTask<IHash> HashMemoryAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            XxHash128 hasher = new();
            hasher.Append(buffer.Span);
            return new ValueTask<IHash>(new XXHash(hasher.GetCurrentHash()));
        }
    }
}
