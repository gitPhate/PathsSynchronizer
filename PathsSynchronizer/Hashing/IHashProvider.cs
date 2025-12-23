using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;

namespace PathsSynchronizer.Hashing
{
    public interface IHashProvider
    {
        ValueTask<FileHash> HashFileAsync(string path, MemoryPool<byte> pool, CancellationToken cancellationToken = default);
        ValueTask<DataHash> HashMemoryAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
    }
}
