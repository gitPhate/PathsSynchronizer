using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Support.Compression
{
    public static class GZipHelper
    {
        public static async Task<MemoryStream> CompressAsync(Stream inputStream)
        {
            MemoryStream resultStream = new MemoryStream();
            using (GZipStream zipStream = new GZipStream(resultStream, CompressionMode.Compress, false))
            {
                await inputStream.CopyToAsync(zipStream).ConfigureAwait(false);
            }

            return resultStream;
        }

        public static async Task<MemoryStream> DecompressAsync(Stream inputStream)
        {
            MemoryStream resultStream = new MemoryStream();

            using (GZipStream gzipStream = new GZipStream(inputStream, CompressionMode.Decompress))
            {
                await gzipStream.CopyToAsync(resultStream).ConfigureAwait(false);
            }

            return resultStream;
        }

        public static async Task<byte[]> CompressStringAsync(string s, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            byte[] stringBytes = encoding.GetBytes(s);
            using (MemoryStream stringStream = new MemoryStream(stringBytes))
            using (MemoryStream resultStream = await CompressAsync(stringStream).ConfigureAwait(false))
            {
                byte[] gzipBytes = resultStream.ToArray();
                return gzipBytes;
            }
        }

        public static async Task<string> DecompressStringAsync(byte[] bytes, Encoding encoding = null)
        {
            encoding = encoding ?? Encoding.UTF8;
            using (MemoryStream inputStream = new MemoryStream(bytes))
            using (MemoryStream resultStream = await DecompressAsync(inputStream).ConfigureAwait(false))
            {
                byte[] unzippedBytes = resultStream.ToArray();
                return encoding.GetString(unzippedBytes);
            }
        }
    }
}
