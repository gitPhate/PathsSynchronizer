using System.Collections.Generic;

namespace PathsSynchronizer.Core.Checksum.XXHash
{
    public class XXHashChecksumTable : DirectoryChecksumTable<ulong>
    {
        public XXHashChecksumTable(string directoryPath, FileChecksumMode mode, IDictionary<string, ulong> table)
            : base(directoryPath, mode, table)
        {
        }
    }
}
