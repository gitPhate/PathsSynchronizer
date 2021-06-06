using HashDepot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PathsSynchronizer.Core.Checksum;
using PathsSynchronizer.Core.Checksum.XXHash;
using System.Threading.Tasks;

namespace PathsSynchronizer.Test
{
    [TestClass]
    public class MainTests
    {
        [TestMethod]
        public async Task TestDirectoryChecksumTableBuilderAsync()
        {
            DirectoryChecksumTable<ulong> table =
            await
                XXHashChecksumTableBuilder
                .CreateNew
                (
                    @"C:\Temp\Consul",
                    FileChecksumMode.FileHash,
                    stream =>
                    {
                        ulong hash = XXHash.Hash64(stream);
                        return Task.FromResult(hash);
                    }
                )
                .BuildAsync(0)
                .ConfigureAwait(false);
        }
    }
}
