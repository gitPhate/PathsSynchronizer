using System.Buffers;
using System.IO.Hashing;

namespace PathsSynchronizer.Hashing.XXHash
{
    public class XXHashProvider : IHashProvider
    {
        public async ValueTask<FileHash> HashFileAsync(string path, MemoryPool<byte> pool, CancellationToken cancellationToken = default)
        {
            XxHash128 hasher = new();
            using FileStream fs = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 1, FileOptions.Asynchronous | FileOptions.SequentialScan);
            using IMemoryOwner<byte> rentedBuffer = pool.Rent(81920);

            while (true)
            {
                int read = await fs.ReadAsync(rentedBuffer.Memory, cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                hasher.Append(rentedBuffer.Memory.Span.Slice(0, read));
            }

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
