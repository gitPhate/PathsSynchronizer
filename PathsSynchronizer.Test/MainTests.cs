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
            await 
                XXHashChecksumTableBuilder
                .CreateNew
                (
                    @"E:\Ema\DEKSTOP EMA",
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
