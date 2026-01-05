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
            const string fileName = @"C:\Users\gabriele.ricci\Downloads\ArkOS_RG351MP_v2.0_12242024.img.xz";
            HashService service = new(ServiceOptions.ExternalHDD, _hashProvider);

            FileHash results = await service.HashFileAsync(fileName);
            FileHash results2 = await service.HashFileAsync(fileName);

            results.Should().Be(results2);
        }

        [Fact]
        public static async Task TestDirectoryScan()
        {
            HashService service = new(ServiceOptions.ExternalHDD, _hashProvider);
            HashProgress progressDetails = default;
            Progress<HashProgress> progress = new(p => progressDetails = p);

            DirectoryHash result = await service.ScanDirectoryAndHashAsync(@"C:\Development\dotnet\Kering-APEEvo", progress);
            result.Files.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public static async Task TestDirectoryHashStorageAndReading()
        {
            const string fileName = "scan.dat";

            HashService service = new(ServiceOptions.ExternalHDD, _hashProvider);
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

            HashService service = new(ServiceOptions.ExternalHDD, new XXHashProvider());
            DirectoryHash result = await service.ScanDirectoryAndHashAsync(@"E:\Foto");
            await StorageService.StoreDirectoryHashAsync(result, filePath);
        }

        [Fact]
        public static async Task TestReadDirectoryScansAndCompare()
        {
            DirectoryHash eResult = await StorageService.ReadStorageFileAsync(@"C:\Temp\directoryhash_E_20260105161648.dat");
            DirectoryHash fResult = await StorageService.ReadStorageFileAsync(@"C:\Temp\directoryhash_F_20260105163800.dat");

            FileHash eFile = eResult.Files.Where(x => x.FilePath.Contains("IMG_1849.JPG")).First();
            var fFile = fResult.Files.Where(x => x.FilePath.Contains("IMG_1849.JPG")).First();

            var missingFiles = eResult.Files.Except(fResult.Files).ToArray();
            var missingFiles2 = fResult.Files.Except(eResult.Files).ToArray();

            var missingLines = missingFiles.Select(x => x.FilePath).Distinct().ToArray();
            File.WriteAllLines("missing.txt", missingLines);


        }
    }
}
