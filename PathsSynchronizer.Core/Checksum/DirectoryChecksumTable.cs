using PathsSynchronizer.Core.Support.Collections;
using PathsSynchronizer.Core.Support.GZip;
using PathsSynchronizer.Core.Support.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Checksum
{
    public class DirectoryChecksumTable<THash> : IReadOnlyDictionary<string, FileChecksum<THash>> where THash : notnull
    {
        private readonly IReadOnlyDictionary<string, FileChecksum<THash>> _checksumTable;

        #region IReadOnlyDictionary

        public FileChecksum<THash> this[string key] => _checksumTable[key];

        public IEnumerable<string> Keys => _checksumTable.Keys;

        public IEnumerable<FileChecksum<THash>> Values => _checksumTable.Values;

        public int Count => _checksumTable.Count;

        public bool ContainsKey(string key) => _checksumTable.ContainsKey(key);

        public IEnumerator<KeyValuePair<string, FileChecksum<THash>>> GetEnumerator() => _checksumTable.GetEnumerator();

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out FileChecksum<THash> value) => _checksumTable.TryGetValue(key, out value);

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        public string DirectoryPath { get; }
        public FileChecksumMode Mode { get; }

        private DirectoryChecksumTable(DirectoryChecksumTableData<THash> data)
        {
            if (!Directory.Exists(data.DirectoryPath))
            {
                throw new DirectoryNotFoundException($"The path {data.DirectoryPath} has not been found");
            }

            DirectoryPath = data.DirectoryPath;
            _checksumTable = data.ChecksumTable ?? new ReadOnlyDictionary<string, FileChecksum<THash>>(new Dictionary<string, FileChecksum<THash>>());
            Mode = data.Mode;
        }

        [JsonConstructor]
        public DirectoryChecksumTable(string directoryPath, FileChecksumMode mode, IDictionary<string, FileChecksum<THash>> table)
            : this(new DirectoryChecksumTableData<THash>(table.AsReadOnly(), directoryPath, mode))
        {
        }

        private DirectoryChecksumTableData<THash> ToDirectoryChecksumTableData()
        {
            string dirPath = (DirectoryPath[^1..] == "\\") ? DirectoryPath : $"{DirectoryPath}\\";

            IDictionary<string, FileChecksum<THash>> auxDictionary = new Dictionary<string, FileChecksum<THash>>();
            foreach (var item in _checksumTable)
            {
                auxDictionary.Add(item.Key.Replace(dirPath, string.Empty), item.Value);
            }

            DirectoryChecksumTableData<THash> toSerializeObj = new(auxDictionary.AsReadOnly(), DirectoryPath, Mode);
            return toSerializeObj;
        }

        public async Task SerializeAsync(string filePath)
        {
            DirectoryChecksumTableData<THash> data = ToDirectoryChecksumTableData();

            using MemoryStream jsonFileStream = new();
            await JsonStreamSerializer.SerializeAsync(data, jsonFileStream).ConfigureAwait(false);

            using FileStream dataFileStream = File.OpenWrite(filePath);
            await GZipHelper.CompressAsync(jsonFileStream, dataFileStream).ConfigureAwait(false);
        }

        public async Task<byte[]> SerializeAsync()
        {
            DirectoryChecksumTableData<THash> data = ToDirectoryChecksumTableData();

            using MemoryStream jsonFileStream = new();
            await JsonStreamSerializer.SerializeAsync(data, jsonFileStream).ConfigureAwait(false);

            using MemoryStream dataStream = new();
            await GZipHelper.CompressAsync(jsonFileStream, dataStream).ConfigureAwait(false);

            dataStream.Seek(0, SeekOrigin.Begin);
            return dataStream.ToArray();
        }

        public static async Task<DirectoryChecksumTable<THash>> DeserializeAsync(string dataFilePath)
        {
            using FileStream dataFileStream = File.OpenRead(dataFilePath);
            return await DeserializeAsync(dataFileStream).ConfigureAwait(false);
        }

        public static async Task<DirectoryChecksumTable<THash>> DeserializeAsync(byte[] bytes)
        {
            using MemoryStream dataStream = new MemoryStream(bytes);
            return await DeserializeAsync(dataStream).ConfigureAwait(false);
        }

        private static async Task<DirectoryChecksumTable<THash>> DeserializeAsync(Stream stream)
        {
            using MemoryStream jsonFileStream = new();
            await GZipHelper.DecompressAsync(stream, jsonFileStream).ConfigureAwait(false);
            DirectoryChecksumTableData<THash>? data =
                await JsonStreamSerializer
                    .DeserializeAsync<DirectoryChecksumTableData<THash>>(jsonFileStream)
                    .ConfigureAwait(false);

            if (data == null)
            {
                throw new JsonException("Unable to deserialize the file correctly");
            }

            IDictionary<string, FileChecksum<THash>> auxDictionary = new Dictionary<string, FileChecksum<THash>>();
            foreach (var item in data.ChecksumTable)
            {
                string key = Path.Combine(data.DirectoryPath, item.Key);
                auxDictionary.Add(key, item.Value);
            }

            DirectoryChecksumTableData<THash> newData = new(auxDictionary.AsReadOnly(), data.DirectoryPath, data.Mode);

            return new DirectoryChecksumTable<THash>(newData);
        }
    }
}