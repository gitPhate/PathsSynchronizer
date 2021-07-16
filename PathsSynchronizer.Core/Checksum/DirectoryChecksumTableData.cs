using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PathsSynchronizer.Core.Checksum
{
    public class DirectoryChecksumTableData
    {
        public IReadOnlyDictionary<string, ulong> ChecksumTable { get; set; }
        public string DirectoryPath { get; set; }
        public FileChecksumMode Mode { get; set; }

        public DirectoryChecksumTableData()
        {
        }

        public DirectoryChecksumTableData(IDictionary<string, ulong> checksumTable, string directoryPath, FileChecksumMode mode)
        {
            ChecksumTable = new ReadOnlyDictionary<string, ulong>(checksumTable ?? new Dictionary<string, ulong>());
            DirectoryPath = directoryPath;
            Mode = mode;
        }
    }
}
