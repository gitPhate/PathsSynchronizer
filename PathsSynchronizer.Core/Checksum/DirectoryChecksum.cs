using PathsSynchronizer.Core.Hashing;
using PathsSynchronizer.Core.Support.GZip;
using PathsSynchronizer.Core.Support.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Checksum
{
    public enum FileChecksumMode { FileName, FileHash }

    public record FileChecksum<T>(string FilePath, T Hash) where T : notnull
    {
        public override int GetHashCode() => Hash.GetHashCode() ^ 16777619;

        public virtual bool Equals(FileChecksum<T>? obj) => obj != null && Hash.Equals(obj.Hash);
    }

    public record FileChecksumGroup<T>(T Hash, string[] FilePathList) where T : notnull
    {
        public override int GetHashCode() => Hash.GetHashCode() ^ 16777619;

        public virtual bool Equals(FileChecksum<T>? obj) => obj != null && Hash.Equals(obj.Hash);
    }

    public record DirectoryChecksumModel<T>(FileChecksum<T>[] FileChecksumList, string DirectoryPath, FileChecksumMode Mode) where T : notnull;

    public class DirectoryChecksum<T>(string directoryPath, FileChecksum<T>[] fileChecksumList, FileChecksumMode mode)
        where T : notnull
    {
        public FileChecksum<T>[] FileChecksumList { get; } = fileChecksumList;
        public string DirectoryPath { get; } = directoryPath;
        public FileChecksumMode Mode { get; } = mode;

        private DirectoryChecksum(DirectoryChecksumModel<T> model)
            : this(model.DirectoryPath, model.FileChecksumList, model.Mode)
        {
        }

        public async Task SerializeAsync(Stream outputStream)
        {
            DirectoryChecksumModel<T> model = new(FileChecksumList, DirectoryPath, Mode);

            using MemoryStream jsonFileStream = new();
            await JsonSerializer.SerializeAsync(jsonFileStream, model).ConfigureAwait(false);
            jsonFileStream.Seek(0, SeekOrigin.Begin);

            await GZipHelper.CompressAsync(jsonFileStream, outputStream).ConfigureAwait(false);
        }

        public async Task<byte[]> SerializeAsync()
        {
            using MemoryStream ms = new();
            await SerializeAsync(ms).ConfigureAwait(false);
            return ms.ToArray();
        }

        public static async Task<DirectoryChecksum<T>> DeserializeAsync(byte[] bytes)
        {
            using MemoryStream dataStream = new(bytes);
            return await DeserializeAsync(dataStream).ConfigureAwait(false);
        }

        public static async Task<DirectoryChecksum<T>> DeserializeAsync(Stream stream)
        {
            using MemoryStream jsonFileStream = new();

            await GZipHelper.DecompressAsync(stream, jsonFileStream).ConfigureAwait(false);

            DirectoryChecksumModel<T>? model =
                await JsonSerializer
                    .DeserializeAsync<DirectoryChecksumModel<T>>(jsonFileStream)
                    .ConfigureAwait(false)
                ?? throw new JsonException("Unable to deserialize the file correctly");

            return new DirectoryChecksum<T>(model);
        }

        public static async ValueTask<DirectoryChecksum<T>> CreateDirectoryChecksum(string dirPath, IHashProvider<T> hashProvider, FileChecksumMode fileChecksumMode = FileChecksumMode.FileHash, string[]? dirNamesToExclude = null)
        {
            if (!Directory.Exists(dirPath))
            {
                throw new DirectoryNotFoundException($"The directory {dirPath} was not found");
            }

            dirNamesToExclude =
                (dirNamesToExclude ?? [])
                .Concat(["system volume information", "recycle"])
                .ToArray();

            FileHashProvider<T> fileHashProvider = new(hashProvider, fileChecksumMode);

            var files =
                IOHelper
                    .EnumerateFiles(dirPath, x => !(dirNamesToExclude ?? []).Any(z => x.Contains(z, StringComparison.OrdinalIgnoreCase)), "*");

            List<FileChecksum<T>> list = [];

            foreach (string filePath in files)
            {
                T hash = await fileHashProvider.HashFileAsync(filePath).ConfigureAwait(false);
                FileChecksum<T> fileChecksum = new(filePath, hash);
                list.Add(fileChecksum);
            }

            return new DirectoryChecksum<T>(dirPath, list.ToArray(), fileChecksumMode);
        }

        public FileChecksumGroup<T>[] GetDuplicates()
        {
            FileChecksumGroup<T>[] duplicates =
                FileChecksumList
                    .GroupBy(x => x.Hash)
                    .Where(x => x.Count() > 1)
                    .Select(x => new FileChecksumGroup<T>(x.Key, x.Select(x => x.FilePath).ToArray()))
                    .ToArray();

            return duplicates;
        }
    }
}