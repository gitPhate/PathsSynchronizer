using Newtonsoft.Json;
using PathsSynchronizer.Core.Support.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Checksum
{
    public class DirectoryChecksumTable
    {
        private readonly IDictionary<string, ulong> _checksumTable;
        public string DirectoryPath { get; }
        public FileChecksumMode Mode { get; }

        public int Count => _checksumTable.Count;

        public DirectoryChecksumTable(string directoryPath, FileChecksumMode mode, IDictionary<string, ulong> table)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentNullException(nameof(directoryPath), "The provided path is null or blank");
            }

            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"The path {directoryPath} has not been found");
            }

            DirectoryPath = directoryPath;
            Mode = mode;
            _checksumTable = table ?? new Dictionary<string, ulong>();
        }

        public async Task<byte[]> SerializeAsync()
        {
            DirectoryChecksumTableData<ulong> toSerializeObj =
                new()
                {
                    Mode = Mode,
                    ChecksumTable = _checksumTable,
                    DirectoryPath = DirectoryPath
                };

            string json = JsonConvert.SerializeObject(toSerializeObj);
            return await GZipHelper.CompressStringAsync(json).ConfigureAwait(false);
        }

        public static async Task<DirectoryChecksumTable> FromSerializedAsync(byte[] bytes)
        {
            string json = await GZipHelper.DecompressStringAsync(bytes).ConfigureAwait(false);
            DirectoryChecksumTableData<ulong> data = JsonConvert.DeserializeObject<DirectoryChecksumTableData<ulong>>(json);
            return new DirectoryChecksumTable(data.DirectoryPath, data.Mode, data.ChecksumTable);
        }
    }
}
