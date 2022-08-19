using PathsSynchronizer.Core.Checksum;
using PathsSynchronizer.Core.XXHash;

namespace PathsSyncronizer.Test
{
    public class MainTests
    {
        [Fact]
        public async Task Test1()
        {
            string folder = @"C:\temp";

            var table =
                await XXHashDirectoryChecksumTableBuilder
                    .CreateNew()
                    .WithFileHashMode()
                    .BuildAsync(folder);

            byte[] bytes = await table.SerializeAsync();

            DirectoryChecksumTable<ulong> table2 = await DirectoryChecksumTable<ulong>.DeserializeAsync(bytes);
        }
    }
}