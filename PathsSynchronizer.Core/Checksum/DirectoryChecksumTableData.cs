using System.Collections.Generic;

namespace PathsSynchronizer.Core.Checksum
{
    public class DirectoryChecksumTableData<TChecksum>
    {
        public IDictionary<string, TChecksum> ChecksumTable { get; set; }
        public string DirectoryPath { get; set; }
        public FileChecksumMode Mode { get; set; }
    }
}
