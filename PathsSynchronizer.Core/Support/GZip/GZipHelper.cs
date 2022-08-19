using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Support.GZip
{
    public static class GZipHelper
    {
        public static async Task CompressAsync(Stream inputStream, Stream outputStream)
        {
            using GZipStream zipStream = new(outputStream, CompressionMode.Compress, true);
            await inputStream.CopyToAsync(zipStream).ConfigureAwait(false);
        }

        public static async Task DecompressAsync(Stream inputStream, Stream outputStream)
        {
            using GZipStream gzipStream = new(inputStream, CompressionMode.Decompress, true);
            await gzipStream.CopyToAsync(outputStream).ConfigureAwait(false);
            outputStream.Seek(0, SeekOrigin.Begin);
        }
    }
}
