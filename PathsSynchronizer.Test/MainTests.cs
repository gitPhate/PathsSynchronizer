using Microsoft.VisualStudio.TestTools.UnitTesting;
using PathsSynchronizer.Core.Checksum;
using System.Linq;
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

        [TestMethod]
        public async Task TestDirectoryChecksumTableSerialization()
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

            byte[] bytes = await table.SerializeAsync().ConfigureAwait(false);
            Assert.IsTrue(bytes.Any());

            DirectoryChecksumTable tableDeserialized = await DirectoryChecksumTable.FromSerializedAsync(bytes).ConfigureAwait(false);
            Assert.IsTrue(table.Count == tableDeserialized.Count);
        }
    }
}
