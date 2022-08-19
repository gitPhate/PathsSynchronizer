using K4os.Hash.xxHash;
using PathsSynchronizer.Core.Hashing;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.XXHash
{
    public class XXHashProvider : IHashProvider<ulong>
    {
        const int _chucksBufferSize = 1024 * 1024 * 50; //50MB

        public async ValueTask<ulong> HashFileAsync(string filePath)
        {
            FileInfo fileInfo = new(filePath);

            ulong hash =
                fileInfo.Length switch
                {
                    < _chucksBufferSize => await HashFileOneShotAsync(filePath).ConfigureAwait(false),
                    _ => await HashFileByChuncksAsync(filePath).ConfigureAwait(false)
                };

            return hash;
        }

        public async ValueTask<ulong> HashFileByChuncksAsync(string filePath)
        {
            XXH64 fileHash = new();

            using FileStream fs = File.OpenRead(filePath);
            using BufferedStream bs = new(fs, _chucksBufferSize);

            int bytesRead;
            byte[] buffer = new byte[_chucksBufferSize];
            while ((bytesRead = await bs.ReadAsync(buffer)) > 0)
            {
                Memory<byte> memory = buffer.AsMemory(0, bytesRead);
                fileHash.Update(memory.Span);
            }

            ulong chucksFileHash = fileHash.Digest();
            return chucksFileHash;
        }

        public async Task<ulong> HashFileOneShotAsync(string filePath)
        {
            byte[] allFile =
                await File
                    .ReadAllBytesAsync(filePath)
                    .ConfigureAwait(false);

            ulong oneShotFileHash = XXH64.DigestOf(allFile, 0, allFile.Length);
            return oneShotFileHash;
        }

        public ValueTask<ulong> HashBytesAsync(byte[] bytes)
        {
            ulong oneShotFileHash = XXH64.DigestOf(bytes, 0, bytes.Length);
            return ValueTask.FromResult(oneShotFileHash);
        }
    }
}
