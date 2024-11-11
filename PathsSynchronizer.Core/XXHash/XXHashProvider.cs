using K4os.Hash.xxHash;
using PathsSynchronizer.Core.Hashing;
using PathsSynchronizer.Core.Support.IO;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.XXHash
{
    public class XXHashProvider : IHashProvider<ulong>
    {
        private static readonly bool _isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public async ValueTask<ulong> HashFileAsync(string filePath)
        {
            bool isRemovableDrive = IsRemovableDrive(filePath);
            int chucksBufferSize = isRemovableDrive ? 4096 : 1048576;
            return await HashFileByChuncksAsync(filePath, chucksBufferSize).ConfigureAwait(false);
        }

        public ValueTask<ulong> HashBytesAsync(byte[] bytes)
        {
            ulong oneShotFileHash = XXH64.DigestOf(bytes, 0, bytes.Length);
            return ValueTask.FromResult(oneShotFileHash);
        }

        private static async ValueTask<ulong> HashFileByChuncksAsync(string filePath, int chucksBufferSize)
        {
            XXH64 fileHash = new();

            using FileStream fs = File.OpenRead(filePath);

            int bytesRead;
            byte[] buffer = new byte[chucksBufferSize];
            while ((bytesRead = await fs.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                fileHash.Update(buffer, 0, bytesRead);
            }

            ulong chucksFileHash = fileHash.Digest();
            return chucksFileHash;
        }

        private static bool IsRemovableDrive(string path)
        {
            if (!_isWindows)
            {
                return false;
            }

#pragma warning disable CA1416
            return WindowsExternalHDDHelper.IsRemovableDrive(path);
        }
    }
}
