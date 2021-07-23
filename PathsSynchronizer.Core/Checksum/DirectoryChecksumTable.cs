using Newtonsoft.Json;
using PathsSynchronizer.Core.Support.Compression;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Checksum
{
    public class DirectoryChecksumTable : IReadOnlyDictionary<ulong, string>
    {
        private readonly IReadOnlyDictionary<ulong, string> _checksumTable;

        #region IRe

        public string this[ulong key] => _checksumTable[key];

        public IEnumerable<ulong> Keys => _checksumTable.Keys;

        public IEnumerable<string> Values => _checksumTable.Values;

        public int Count => _checksumTable.Count;

        public bool ContainsKey(ulong key) => _checksumTable.ContainsKey(key);

        public IEnumerator<KeyValuePair<ulong, string>> GetEnumerator() => _checksumTable.GetEnumerator();

        public bool TryGetValue(ulong key, [MaybeNullWhen(false)] out string value) => _checksumTable.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        public string DirectoryPath { get; }
        public FileChecksumMode Mode { get; }

        private DirectoryChecksumTable(DirectoryChecksumTableData data)
        {
            if (string.IsNullOrWhiteSpace(data.DirectoryPath))
            {
                throw new ArgumentNullException(nameof(data.DirectoryPath), "The provided path is null or blank");
            }

            if (!Directory.Exists(data.DirectoryPath))
            {
                throw new DirectoryNotFoundException($"The path {data.DirectoryPath} has not been found");
            }

            DirectoryPath = data.DirectoryPath;
            Mode = data.Mode;
            _checksumTable = data.ChecksumTable ?? new ReadOnlyDictionary<ulong, string>(new Dictionary<ulong, string>());
        }

        public DirectoryChecksumTable(string directoryPath, FileChecksumMode mode, IDictionary<ulong, string> table)
            : this(new DirectoryChecksumTableData(table, directoryPath, mode))
        {
        }

        public async Task<byte[]> SerializeAsync()
        {
            string dirPath = (DirectoryPath.Substring(DirectoryPath.Length - 1) == "\\") ? DirectoryPath : $"{DirectoryPath}\\";

            IDictionary<ulong, string> auxDictionary = new Dictionary<ulong, string>();
            foreach (var item in _checksumTable)
            {
                auxDictionary.Add(item.Key, item.Value.Replace(dirPath, string.Empty));
            }

            DirectoryChecksumTableData toSerializeObj = new(auxDictionary, DirectoryPath, Mode);

            string json = JsonConvert.SerializeObject(toSerializeObj);
            return await GZipHelper.CompressStringAsync(json).ConfigureAwait(false);
        }

        public static async Task<DirectoryChecksumTable> FromSerializedAsync(byte[] bytes)
        {
            string json = await GZipHelper.DecompressStringAsync(bytes).ConfigureAwait(false);
            DirectoryChecksumTableData data = JsonConvert.DeserializeObject<DirectoryChecksumTableData>(json);

            IDictionary<ulong, string> auxDictionary = new Dictionary<ulong, string>();
            foreach (var item in data.ChecksumTable)
            {
                string value = Path.Combine(data.DirectoryPath, item.Value);
                auxDictionary.Add(item.Key, value);
            }

            DirectoryChecksumTableData newData = new(auxDictionary, data.DirectoryPath, data.Mode);

            return new DirectoryChecksumTable(newData);
        }
    }
}
