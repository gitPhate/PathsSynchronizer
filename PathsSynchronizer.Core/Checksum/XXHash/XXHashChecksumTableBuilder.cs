using System;
using System.IO;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Checksum.XXHash
{
    public class XXHashChecksumTableBuilder : DirectoryChecksumTableBuilder<ulong>
    {
        protected XXHashChecksumTableBuilder(string directoryPath, FileChecksumMode mode, Func<Stream, Task<ulong>> hashFunction)
            : base(directoryPath, mode, hashFunction)
        {
        }
    }
}
