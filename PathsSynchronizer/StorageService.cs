using PathsSynchronizer.Support;
using System.IO;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading.Tasks;

namespace PathsSynchronizer
{
    public class StorageService
    {
        public static async Task StoreDirectoryHashAsync(DirectoryHash directoryHash, string filePath)
        {
            using MemoryStream jsonStream = new();
            using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(jsonStream, directoryHash);
            jsonStream.Position = 0;
            await GZipHelper.CompressAsync(jsonStream, fileStream).ConfigureAwait(false);
        }

        public static async Task<DirectoryHash> ReadStorageFileAsync(string filePath)
        {
            using FileStream fileStream = File.OpenRead(filePath);
            using MemoryStream memoryStream = new();
            await GZipHelper.DecompressAsync(fileStream, memoryStream);
            memoryStream.Position = 0;
            DirectoryHash directoryHash =
                JsonSerializer.Deserialize<DirectoryHash>(memoryStream)
                ?? throw new SerializationException($"Unable to deserialize the file {filePath}");
            return directoryHash;
        }
    }
}
