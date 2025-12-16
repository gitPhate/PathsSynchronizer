using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace PathsSynchronizer.Hashing
{
    public interface IHash : IEquatable<IHash>
    {
        public byte[] Bytes { get; }
        public string Hash { get; }
    }

    public class FileHash
    {
        public IHash[] Hashes { get; }

        public FileHash(IHash single) => Hashes = [single];
        public FileHash(IHash[] hashes) => Hashes = hashes;

        // Compare all hashes
        public bool Equals(FileHash other)
        {
            if (other == null || Hashes.Length != other.Hashes.Length)
            {
                return false;
            }

            for (int i = 0; i < Hashes.Length; i++)
            {
                if (Hashes[i] != other.Hashes[i])
                    return false;
            }

            return true;
        }
    }

    public interface IHashProvider
    {
        ValueTask<FileHash> HashFileAsync(string path, MemoryPool<byte> pool, CancellationToken cancellationToken = default);
        ValueTask<IHash> HashMemoryAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
    }
}
