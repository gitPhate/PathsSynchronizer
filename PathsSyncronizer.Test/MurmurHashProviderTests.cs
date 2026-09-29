using FluentAssertions;
using PathsSynchronizer.Hashing;
using PathsSynchronizer.Hashing.MurmurHash;
using PathsSyncronizer.Test.Support;
using System.Buffers;
using System.Text;

namespace PathsSyncronizer.Test
{
    public class MurmurHashProviderTests : IDisposable
    {
        private readonly MurmurHashProvider _provider = new();
        private readonly TempDirectory _dir = new();
        private readonly MemoryPool<byte> _pool = MemoryPool<byte>.Shared;

        public void Dispose() => _dir.Dispose();

        private async Task<FileHash> HashAsync(string path, int bufferSize = 64) =>
            await _provider.HashFileAsync(path, new FileInfo(path).Length, _pool, bufferSize);

        // Reference MurmurHash3_x64_128 vectors, seed 0, bytes in canonical order (h1 LE, h2 LE).
        [Theory]
        [InlineData("", "00000000000000000000000000000000")]
        [InlineData("hello", "029BBD41B3A7D8CB191DAE486A901E5B")]
        [InlineData("The quick brown fox jumps over the lazy dog", "6C1B07BC7BBC4BE347939AC4A93C437A")]
        public async Task HashMemory_matches_reference_vectors(string input, string expectedHex)
        {
            DataHash hash = await _provider.HashMemoryAsync(Encoding.ASCII.GetBytes(input));

            hash.Hash.Should().Be(expectedHex);
        }

        [Theory]
        [InlineData("hello", "029BBD41B3A7D8CB191DAE486A901E5B")]
        [InlineData("The quick brown fox jumps over the lazy dog", "6C1B07BC7BBC4BE347939AC4A93C437A")]
        public async Task HashFile_matches_reference_vectors(string input, string expectedHex)
        {
            string path = _dir.Write("a.txt", Encoding.ASCII.GetBytes(input));

            FileHash hash = await HashAsync(path, bufferSize: 5);

            hash.Hashes.Should().ContainSingle().Which.Hash.Should().Be(expectedHex);
        }

        [Fact]
        public async Task Identical_content_at_different_paths_gives_same_hash()
        {
            byte[] content = TestData.Bytes(500);
            string a = _dir.Write("a.bin", content);
            string b = _dir.Write("sub/b.bin", content);

            (await HashAsync(a)).Should().Be(await HashAsync(b));
        }

        [Fact]
        public async Task Different_content_gives_different_hash()
        {
            string a = _dir.Write("a.bin", TestData.Bytes(500, seed: 1));
            string b = _dir.Write("b.bin", TestData.Bytes(500, seed: 2));

            (await HashAsync(a)).Should().NotBe(await HashAsync(b));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(7)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(64)]
        [InlineData(10_000)]
        public async Task Hash_does_not_depend_on_buffer_size(int bufferSize)
        {
            string path = _dir.Write("a.bin", TestData.Bytes(1003));

            FileHash reference = await HashAsync(path, bufferSize: 1003);
            FileHash result = await HashAsync(path, bufferSize);

            result.Should().Be(reference);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(15)]
        [InlineData(16)]
        [InlineData(17)]
        [InlineData(32)]
        public async Task Tail_lengths_are_all_covered_and_distinct(int length)
        {
            byte[] content = TestData.Bytes(length);
            byte[] shorter = content[..^1];

            DataHash full = await _provider.HashMemoryAsync(content);
            DataHash truncated = await _provider.HashMemoryAsync(shorter);

            full.Should().NotBe(truncated);
        }

        [Fact]
        public async Task Empty_file_hashes_without_throwing()
        {
            string path = _dir.Write("empty.bin", []);

            FileHash hash = await HashAsync(path);

            hash.Hashes.Should().HaveCount(1);
            hash.Hashes[0].Bytes.Should().HaveCount(16);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public async Task Non_positive_buffer_size_throws(int bufferSize)
        {
            string path = _dir.Write("a.bin", TestData.Bytes(10));

            Func<Task> act = async () => await HashAsync(path, bufferSize);

            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        [Fact]
        public async Task Missing_file_throws_FileNotFound()
        {
            Func<Task> act = async () => await HashAsync(_dir.Combine("missing.bin"));

            await act.Should().ThrowAsync<FileNotFoundException>();
        }

        [Fact]
        public async Task Cancelled_token_throws_OperationCanceled()
        {
            string path = _dir.Write("a.bin", TestData.Bytes(10));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Func<Task> act = async () => await _provider.HashFileAsync(path, 10, _pool, bufferSize: 1, cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task HashMemory_is_deterministic_and_16_bytes()
        {
            byte[] content = TestData.Bytes(100);

            DataHash a = await _provider.HashMemoryAsync(content);
            DataHash b = await _provider.HashMemoryAsync((byte[])content.Clone());

            a.Bytes.Should().HaveCount(16);
            a.Should().Be(b);
        }

        [Fact]
        public async Task HashMemory_differs_for_different_bytes()
        {
            DataHash a = await _provider.HashMemoryAsync(TestData.Bytes(100, seed: 1));
            DataHash b = await _provider.HashMemoryAsync(TestData.Bytes(100, seed: 2));

            a.Should().NotBe(b);
        }

        [Fact]
        public async Task HashMemory_matches_HashFile_of_same_content()
        {
            byte[] content = TestData.Bytes(300);
            string path = _dir.Write("a.bin", content);

            DataHash fromMemory = await _provider.HashMemoryAsync(content);
            FileHash fromFile = await HashAsync(path, bufferSize: 50);

            fromFile.Hashes.Should().Equal(fromMemory);
        }
    }
}
