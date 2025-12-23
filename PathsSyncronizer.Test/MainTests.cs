using FluentAssertions;
using PathsSynchronizer;
using PathsSynchronizer.Hashing;
using PathsSynchronizer.Hashing.XXHash;

namespace PathsSyncronizer.Test
{
    public class MainTests
    {
        private static readonly IHashProvider _hashProvider = new XXHashProvider();

        [Fact]
        public static async Task TestSingleFile()
        {
            const string fileName = "testfile.txt";
            HashService service = new(ServiceOptions.Default, _hashProvider);

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
            HashService service = new(ServiceOptions.Default, _hashProvider);
            DirectoryHash result = await service.ScanDirectoryAndHashAsync(@"C:\Temp");
            result.Files.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public static async Task TestDirectoryHashStorageAndReading()
        {
            const string fileName = "scan.dat";

            HashService service = new(ServiceOptions.Default, _hashProvider);
            DirectoryHash result = await service.ScanDirectoryAndHashAsync(@"C:\UX");

            try
            {
                await StorageService.StoreDirectoryHashAsync(result, fileName);
                DirectoryHash deserialized = await StorageService.ReadStorageFileAsync(fileName);
                deserialized.Should().Be(result);
            }
            finally
            {
                if (File.Exists(fileName))
                {
                    File.Delete(fileName);
                }
            }
        }
    }
}
