using Newtonsoft.Json;
using PathsSynchronizer.Core.Support.Compression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Checksum
{
    public class DirectoryChecksumTable<TChecksum>
    {
        private IDictionary<string, TChecksum> _checksumTable;
        public string DirectoryPath { get; }
        public FileChecksumMode Mode { get; }

        public DirectoryChecksumTable(string directoryPath, FileChecksumMode mode, IDictionary<string, TChecksum> table)
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
            _checksumTable = table ?? new Dictionary<string, TChecksum>();
        }

        public async Task<byte[]> SerializeAsync()
        {
            DirectoryChecksumTableData<TChecksum> toSerializeObj =
                new DirectoryChecksumTableData<TChecksum>
                {
                    Mode = Mode,
                    ChecksumTable = _checksumTable,
                    DirectoryPath = DirectoryPath
                };

            string json = JsonConvert.SerializeObject(toSerializeObj);
            return await GZipHelper.CompressStringAsync(json).ConfigureAwait(false);
        }

        public static async Task<DirectoryChecksumTable<TChecksum>> FromSerialized(byte[] bytes)
        {
            string json = await GZipHelper.DecompressStringAsync(bytes).ConfigureAwait(false);
            DirectoryChecksumTableData<TChecksum> data = JsonConvert.DeserializeObject<DirectoryChecksumTableData<TChecksum>>(json);
            return new DirectoryChecksumTable<TChecksum>(data.DirectoryPath, data.Mode, data.ChecksumTable);
        }
    }
}
