using FluentAssertions;
using PathsSynchronizer;
using PathsSynchronizer.Hashing;
using PathsSynchronizer.Hashing.XXHash;
using PathsSyncronizer.Test.Support;

namespace PathsSyncronizer.Test
{
    // Defaults: threshold 100 bytes, 4 samples of 16 bytes.
    // For a 1000-byte file the sampled blocks start at 0, 328, 656 and 984.
    public class HashServiceFileTests : IDisposable
    {
        private readonly TempDirectory _dir = new();
        private readonly XXHashProvider _provider = new();

        public void Dispose() => _dir.Dispose();

        private HashService Service(ServiceOptions? options = null) => new(options ?? TestData.Options(), _provider);

        [Fact]
        public async Task Small_file_is_fully_hashed_with_one_hash_and_keeps_its_path()
        {
            string path = _dir.Write("small.bin", TestData.Bytes(50));

            FileHash hash = await Service().HashFileAsync(path);

            hash.FilePath.Should().Be(path);
            hash.Hashes.Should().HaveCount(1);
        }

        [Fact]
        public async Task Identical_content_at_two_paths_gives_equal_hashes()
        {
            byte[] content = TestData.Bytes(1000);
            string a = _dir.Write("a.bin", content);
            string b = _dir.Write("b.bin", content);

            (await Service().HashFileAsync(a)).Should().Be(await Service().HashFileAsync(b));
        }

        [Fact]
        public async Task One_byte_change_in_small_file_changes_hash()
        {
            byte[] content = TestData.Bytes(50);
            string a = _dir.Write("a.bin", content);
            string b = _dir.Write("b.bin", TestData.WithByteChanged(content, 25));

            (await Service().HashFileAsync(a)).Should().NotBe(await Service().HashFileAsync(b));
        }

        [Fact]
        public async Task Large_file_is_sampled_with_SampleCount_hashes()
        {
            string path = _dir.Write("large.bin", TestData.Bytes(1000));

            FileHash hash = await Service().HashFileAsync(path);

            hash.Hashes.Should().HaveCount(4);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(15)]
        [InlineData(328)]
        [InlineData(343)]
        [InlineData(656)]
        [InlineData(671)]
        [InlineData(984)]
        [InlineData(999)]
        public async Task Change_inside_a_sampled_block_changes_hash(int changedIndex)
        {
            byte[] content = TestData.Bytes(1000);
            string a = _dir.Write("a.bin", content);
            string b = _dir.Write("b.bin", TestData.WithByteChanged(content, changedIndex));

            (await Service().HashFileAsync(a)).Should().NotBe(await Service().HashFileAsync(b));
        }

        [Theory]
        [InlineData(16)]
        [InlineData(100)]
        [InlineData(327)]
        [InlineData(344)]
        [InlineData(655)]
        [InlineData(983)]
        public async Task Change_outside_every_sampled_block_does_not_change_hash(int changedIndex)
        {
            byte[] content = TestData.Bytes(1000);
            string a = _dir.Write("a.bin", content);
            string b = _dir.Write("b.bin", TestData.WithByteChanged(content, changedIndex));

            (await Service().HashFileAsync(a)).Should().Be(await Service().HashFileAsync(b));
        }

        [Fact]
        public async Task File_exactly_at_threshold_is_fully_hashed()
        {
            byte[] content = TestData.Bytes(100);
            string a = _dir.Write("a.bin", content);
            string b = _dir.Write("b.bin", TestData.WithByteChanged(content, 50));

            FileHash hashA = await Service().HashFileAsync(a);
            FileHash hashB = await Service().HashFileAsync(b);

            hashA.Hashes.Should().HaveCount(1);
            hashA.Should().NotBe(hashB);
        }

        [Fact]
        public async Task File_one_byte_over_threshold_is_sampled()
        {
            byte[] content = TestData.Bytes(101);
            string a = _dir.Write("a.bin", content);
            string b = _dir.Write("b.bin", TestData.WithByteChanged(content, 50)); // between sampled blocks

            FileHash hashA = await Service().HashFileAsync(a);
            FileHash hashB = await Service().HashFileAsync(b);

            hashA.Hashes.Should().HaveCount(4);
            hashA.Should().Be(hashB);
        }

        [Fact]
        public async Task Sampled_file_smaller_than_block_size_hashes_whole_content_for_every_sample()
        {
            byte[] content = TestData.Bytes(10);
            string path = _dir.Write("tiny.bin", content);
            HashService service = Service(TestData.Options(fullHashThreshold: 0, sampleBlockSize: 16));

            FileHash hash = await service.HashFileAsync(path);

            DataHash expected = await _provider.HashMemoryAsync(content);
            hash.Hashes.Should().HaveCount(4).And.AllBeEquivalentTo(expected);
        }

        [Fact]
        public async Task Single_sample_reads_from_start_of_file()
        {
            byte[] content = TestData.Bytes(1000);
            string a = _dir.Write("a.bin", content);
            string b = _dir.Write("b.bin", TestData.WithByteChanged(content, 999));
            string c = _dir.Write("c.bin", TestData.WithByteChanged(content, 0));
            HashService service = Service(TestData.Options(sampleCount: 1));

            FileHash hashA = await service.HashFileAsync(a);

            hashA.Hashes.Should().HaveCount(1);
            hashA.Should().Be(await service.HashFileAsync(b));
            hashA.Should().NotBe(await service.HashFileAsync(c));
        }

        [Fact]
        public async Task Cancelled_token_throws_OperationCanceled()
        {
            string path = _dir.Write("a.bin", TestData.Bytes(50));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Func<Task> act = async () => await Service().HashFileAsync(path, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task Missing_file_throws()
        {
            Func<Task> act = async () => await Service().HashFileAsync(_dir.Combine("missing.bin"));

            await act.Should().ThrowAsync<FileNotFoundException>();
        }
    }
}
