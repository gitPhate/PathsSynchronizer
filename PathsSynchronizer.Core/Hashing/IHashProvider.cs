using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Hashing
{
    public interface IHashProvider<THash> where THash : notnull
    {
        ValueTask<THash> HashFileAsync(string filePath);
        ValueTask<THash> HashBytesAsync(byte[] bytes);
    }
}
