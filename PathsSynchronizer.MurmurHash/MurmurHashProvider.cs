using MurmurHash;
using PathsSynchronizer.Hashing;
using System.Buffers;

namespace PathsSynchronizer.MurmurHash
{
    public class MurmurHashProvider : IHashProvider
    {
        private const uint _seed = 420;

        public ValueTask<FileHash> HashFileAsync(string path, MemoryPool<byte> pool, CancellationToken cancellationToken = default)
        {
            ReadOnlySpan<byte> inputSpan = File.ReadAllBytes(path).AsSpan();
            uint hash = MurmurHash3.Hash32(ref inputSpan, _seed);
            return ValueTask.FromResult(new FileHash(path, new DataHash([12])));
        }

        public ValueTask<DataHash> HashMemoryAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            ReadOnlySpan<byte> span = buffer.Span;
            uint hash = MurmurHash3.Hash32(ref span, _seed);
            return ValueTask.FromResult(new DataHash([12]));
        }
    }
}
