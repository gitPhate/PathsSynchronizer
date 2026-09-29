using System.Buffers;
using System.IO.Hashing;
using Microsoft.Win32.SafeHandles;

namespace PathsSynchronizer.Hashing.XXHash
{
    public class XXHashProvider : IHashProvider
    {
        private const int BufferSize = 81920;

        public ValueTask<FileHash> HashFileAsync(string path, MemoryPool<byte> pool, CancellationToken cancellationToken = default)
        {
            XxHash128 hasher = new();
            using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
            using IMemoryOwner<byte> rentedBuffer = pool.Rent(BufferSize);
            Span<byte> buffer = rentedBuffer.Memory.Span;

            long offset = 0;
            int read;
            while ((read = RandomAccess.Read(handle, buffer, offset)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hasher.Append(buffer.Slice(0, read));
                offset += read;
            }

            return new ValueTask<FileHash>(new FileHash(path, new DataHash(hasher.GetCurrentHash()))); // 16 bytes (128 bits)
        }

        public ValueTask<DataHash> HashMemoryAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            XxHash128 hasher = new();
            hasher.Append(buffer.Span);
            return new ValueTask<DataHash>(new DataHash(hasher.GetCurrentHash()));
        }
    }
}
