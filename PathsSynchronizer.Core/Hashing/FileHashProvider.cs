using PathsSynchronizer.Core.Checksum;
using System;
using System.Text;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Hashing
{
    public class FileHashProvider<T> where T : notnull
    {
        private readonly IHashProvider<T> _hashProvider;
        private readonly FileChecksumMode _mode;

        public FileHashProvider(IHashProvider<T> hashProvider, FileChecksumMode mode)
        {
            _hashProvider = hashProvider;
            _mode = mode;
        }

        public async ValueTask<T> HashFileAsync(string filePath)
        {
            T hash =
                await (_mode switch
                {
                    FileChecksumMode.FileName => HashFileByPathAsync(filePath),
                    FileChecksumMode.FileHash => HashFileByBytesAsync(filePath),
                    _ => throw new NotImplementedException()
                })
                .ConfigureAwait(false);

            return hash;
        }

        private ValueTask<T> HashFileByBytesAsync(string filePath) =>
            _hashProvider.HashFileAsync(filePath);

        private async ValueTask<T> HashFileByPathAsync(string filePath)
        {
            byte[] allFile = Encoding.UTF8.GetBytes(filePath);
            return await _hashProvider.HashBytesAsync(allFile).ConfigureAwait(false);
        }
    }
}
