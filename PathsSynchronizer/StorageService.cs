using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PathsSynchronizer
{
    public static class StorageService
    {
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            WriteIndented = false
        };

        public static async Task StoreDirectoryHashAsync(DirectoryHash directoryHash, string filePath, CancellationToken cancellationToken = default)
        {
            using FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using GZipStream zipStream = new(fileStream, CompressionMode.Compress, leaveOpen: false);
            await JsonSerializer.SerializeAsync(zipStream, directoryHash, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        public static async Task<DirectoryHash> ReadStorageFileAsync(string filePath, CancellationToken cancellationToken = default)
        {
            using FileStream fileStream = File.OpenRead(filePath);
            using GZipStream gzipStream = new(fileStream, CompressionMode.Decompress, leaveOpen: false);
            DirectoryHash directoryHash =
                await JsonSerializer.DeserializeAsync<DirectoryHash>(gzipStream, SerializerOptions, cancellationToken).ConfigureAwait(false)
                ?? throw new SerializationException($"Unable to deserialize the file {filePath}");
            return directoryHash;
        }
    }
}
