using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PathsSynchronizer.Core.Checksum
{
    public class DirectoryChecksumTableData<THash> where THash : notnull
    {
        public IReadOnlyDictionary<string, FileChecksum<THash>> ChecksumTable { get; }
        public string DirectoryPath { get; }
        public FileChecksumMode Mode { get; }

        [JsonConstructor]
        public DirectoryChecksumTableData(IReadOnlyDictionary<string, FileChecksum<THash>> checksumTable, string directoryPath, FileChecksumMode mode)
        {
            ChecksumTable = checksumTable;
            DirectoryPath = directoryPath;
            Mode = mode;
        }
    }
}
