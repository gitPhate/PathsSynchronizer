using FluentAssertions;
using PathsSynchronizer;
using PathsSynchronizer.Hashing;
using PathsSyncronizer.Test.Support;
using System.IO.Compression;
using System.Runtime.Serialization;

namespace PathsSyncronizer.Test
{
    public class StorageServiceTests : IDisposable
    {
        private readonly TempDirectory _dir = new();

        public void Dispose() => _dir.Dispose();

        private static DirectoryHash Sample(string root = "root") =>
            new(root, [new FileHash(Path.Combine(root, "a.txt"), TestData.Hash(1, 2)), new FileHash(Path.Combine(root, "b.txt"), [TestData.Hash(3), TestData.Hash(4)])]);

        [Fact]
        public async Task Store_then_read_returns_equal_directory_hash()
        {
            string path = _dir.Combine("scan.dat");
            DirectoryHash original = Sample();

            await StorageService.StoreDirectoryHashAsync(original, path);
            DirectoryHash restored = await StorageService.ReadStorageFileAsync(path);

            restored.Should().Be(original);
        }

        [Fact]
        public async Task Empty_directory_hash_round_trips()
        {
            string path = _dir.Combine("empty.dat");
            DirectoryHash original = new("root", []);

            await StorageService.StoreDirectoryHashAsync(original, path);
            DirectoryHash restored = await StorageService.ReadStorageFileAsync(path);

            restored.Should().Be(original);
        }

        [Fact]
        public async Task Non_ascii_and_spaced_paths_survive_round_trip()
        {
            string path = _dir.Combine("unicode.dat");
            DirectoryHash original = new("C:\\Foto è ñ 日本", [new FileHash("C:\\Foto è ñ 日本\\my file – 1.jpg", TestData.Hash(7))]);

            await StorageService.StoreDirectoryHashAsync(original, path);
            DirectoryHash restored = await StorageService.ReadStorageFileAsync(path);

            restored.Path.Should().Be(original.Path);
            restored.Files[0].FilePath.Should().Be(original.Files[0].FilePath);
        }

        [Fact]
        public async Task Stored_file_is_gzip_compressed()
        {
            string path = _dir.Combine("scan.dat");

            await StorageService.StoreDirectoryHashAsync(Sample(), path);

            byte[] header = File.ReadAllBytes(path).Take(2).ToArray();
            header.Should().Equal(0x1F, 0x8B);
        }

        [Fact]
        public async Task Storing_to_existing_path_overwrites_it()
        {
            string path = _dir.Combine("scan.dat");
            await StorageService.StoreDirectoryHashAsync(Sample("first"), path);

            DirectoryHash second = Sample("second");
            await StorageService.StoreDirectoryHashAsync(second, path);
            DirectoryHash restored = await StorageService.ReadStorageFileAsync(path);

            restored.Should().Be(second);
        }

        [Fact]
        public async Task Reading_missing_file_throws_FileNotFound()
        {
            Func<Task> act = async () => await StorageService.ReadStorageFileAsync(_dir.Combine("missing.dat"));

            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task Reading_non_gzip_file_throws_InvalidData()
        {
            string path = _dir.Write("plain.dat", System.Text.Encoding.UTF8.GetBytes("{\"Path\":\"x\",\"Files\":[]}"));

            Func<Task> act = async () => await StorageService.ReadStorageFileAsync(path);

            await act.Should().ThrowAsync<InvalidDataException>();
        }

        [Fact]
        public async Task Reading_gzip_json_null_throws_SerializationException()
        {
            string path = _dir.Combine("null.dat");
            await using (FileStream file = File.Create(path))
            await using (GZipStream zip = new(file, CompressionMode.Compress))
            {
                await zip.WriteAsync(System.Text.Encoding.UTF8.GetBytes("null"));
            }

            Func<Task> act = async () => await StorageService.ReadStorageFileAsync(path);

            await act.Should().ThrowAsync<SerializationException>();
        }

        [Fact]
        public async Task Store_with_cancelled_token_throws_OperationCanceled()
        {
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Func<Task> act = async () => await StorageService.StoreDirectoryHashAsync(Sample(), _dir.Combine("scan.dat"), cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task Read_with_cancelled_token_throws_OperationCanceled()
        {
            string path = _dir.Combine("scan.dat");
            await StorageService.StoreDirectoryHashAsync(Sample(), path);
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Func<Task> act = async () => await StorageService.ReadStorageFileAsync(path, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
    }
}
