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
            HashService service = new(ServiceOptions.Default, new XXHashProvider());

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
            HashService service = new(ServiceOptions.Default, new XXHashProvider());
            HashProgress progressDetails = default;
            Progress<HashProgress> progress = new(p => progressDetails = p);

            DirectoryHash result = await service.ScanDirectoryAndHashAsync(@"C:\Temp\pictures\missing", progress);
            result.Files.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public static async Task TestDirectoryHashStorageAndReading()
        {
            const string fileName = "scan.dat";

            HashService service = new(ServiceOptions.Default, new XXHashProvider());
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

        [Fact]
        public static async Task TestDirectoryScanAndStore()
        {
            const string filePath = @"C:\temp\scan.dat";

            HashService service = new(ServiceOptions.Default, new XXHashProvider());
            DirectoryHash result = await service.ScanDirectoryAndHashAsync(@"E:\Foto");
            await StorageService.StoreDirectoryHashAsync(result, filePath);
        }
    }
}
