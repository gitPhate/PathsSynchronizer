using Microsoft.VisualStudio.TestTools.UnitTesting;
using PathsSynchronizer.Core.Checksum;
using System.Threading.Tasks;

namespace PathsSynchronizer.Test
{
    [TestClass]
    public class MainTests
    {
        [TestMethod]
        public async Task TestDirectoryChecksumTableBuilderAsync()
        {
            DirectoryChecksumTable table =
                await
                    DirectoryChecksumTableBuilder
                    .CreateNew
                    (
                        @"C:\Temp\Consul",
                        FileChecksumMode.FileHash
                    )
                    .BuildAsync(0)
                    .ConfigureAwait(false);

            Assert.IsTrue(true);
        }
    }
}
