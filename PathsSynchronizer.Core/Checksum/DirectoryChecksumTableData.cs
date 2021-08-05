using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PathsSynchronizer.Core.Checksum
{
    public class DirectoryChecksumTableData
    {
        public IReadOnlyDictionary<ulong, string> ChecksumTable { get; set; }
        public string DirectoryPath { get; set; }
        public FileChecksumMode Mode { get; set; }

        public DirectoryChecksumTableData(IDictionary<ulong, string> checksumTable, string directoryPath, FileChecksumMode mode)
        {
            ChecksumTable = new ReadOnlyDictionary<ulong, string>(checksumTable ?? new Dictionary<ulong, string>());
            DirectoryPath = directoryPath;
            Mode = mode;
        }
    }
}
