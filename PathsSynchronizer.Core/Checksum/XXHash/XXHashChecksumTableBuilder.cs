using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
