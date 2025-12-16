using FluentAssertions;
using PathsSynchronizer;
using PathsSynchronizer.Hashing;
using PathsSynchronizer.Hashing.XXHash;

namespace PathsSyncronizer.Test
{
    public class MainTests
    {
        [Fact]
        public static async Task TestSingleFile()
        {
            const string fileName = "testfile.txt";
            Service service = new(ServiceOptions.Default, new XXHashProvider());

            File.WriteAllText(fileName, "test");

            try
            {
                FileHash results = await service.HashFileAsync(fileName);
                FileHash results2 = await service.HashFileAsync(fileName);

                results.Should().Be(results2);
            }
            finally
            {
                File.Delete(fileName);
            }
        }

        [Fact]
        public static async Task TestDirectoryScan()
        {
            Service service = new(ServiceOptions.Default, new XXHashProvider());
            await service.ScanDirectoryAndHashAsync(@"C:\Temp");
        }
    }
}
