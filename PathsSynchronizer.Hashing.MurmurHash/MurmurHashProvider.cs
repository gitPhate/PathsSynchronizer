using Microsoft.Win32.SafeHandles;
using PathsSynchronizer.Hashing;
using System.Buffers;

namespace PathsSynchronizer.Hashing.MurmurHash
{
    public class MurmurHashProvider : IHashProvider
    {
        public ValueTask<FileHash> HashFileAsync(string path, long length, MemoryPool<byte> pool, int bufferSize, CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bufferSize);

            if (length == 0)
            {
                return new ValueTask<FileHash>(new FileHash(path, new DataHash(MurmurHash3x64.Hash(ReadOnlySpan<byte>.Empty))));
            }

            MurmurHash3x64 hasher = new();
            using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read, FileOptions.SequentialScan);
            using IMemoryOwner<byte> rentedBuffer = pool.Rent(bufferSize);
            Span<byte> buffer = rentedBuffer.Memory.Span.Slice(0, bufferSize);

            long offset = 0;
            int read;
            while (offset < length && (read = RandomAccess.Read(handle, buffer, offset)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                hasher.Append(buffer.Slice(0, read));
                offset += read;
            }

            return new ValueTask<FileHash>(new FileHash(path, new DataHash(hasher.GetCurrentHash()))); // 16 bytes (128 bits)
        }

        public ValueTask<DataHash> HashMemoryAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(new DataHash(MurmurHash3x64.Hash(buffer.Span)));
    }
}
