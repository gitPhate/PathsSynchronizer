using FluentAssertions;
using PathsSynchronizer;
using PathsSynchronizer.Hashing;
using PathsSynchronizer.Hashing.XXHash;
using PathsSyncronizer.Test.Support;

namespace PathsSyncronizer.Test
{
    public class HashServiceScanTests : IDisposable
    {
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        private readonly TempDirectory _dir = new();
        private readonly XXHashProvider _provider = new();

        public void Dispose() => _dir.Dispose();

        private HashService Service(ServiceOptions? options = null, IHashProvider? provider = null) =>
            new(options ?? TestData.Options(), provider ?? _provider);

        private async Task<DirectoryHash> ScanAsync(HashService? service = null, IProgress<HashProgress>? progress = null, CancellationToken ct = default) =>
            await (service ?? Service()).ScanDirectoryAndHashAsync(_dir.Path, progress, ct).WaitAsync(Timeout);

        [Fact]
        public async Task Empty_directory_gives_no_files_and_keeps_root_path()
        {
            DirectoryHash result = await ScanAsync();

            result.Path.Should().Be(_dir.Path);
            result.Files.Should().BeEmpty();
        }

        [Fact]
        public async Task Flat_directory_returns_one_hash_per_file_with_full_paths()
        {
            string a = _dir.Write("a.bin", TestData.Bytes(10, 1));
            string b = _dir.Write("b.bin", TestData.Bytes(20, 2));

            DirectoryHash result = await ScanAsync();

            result.Files.Select(f => f.FilePath).Should().BeEquivalentTo([a, b]);
        }

        [Fact]
        public async Task Nested_subdirectories_are_scanned()
        {
            string a = _dir.Write("a.bin", TestData.Bytes(10, 1));
            string b = _dir.Write(Path.Combine("x", "b.bin"), TestData.Bytes(10, 2));
            string c = _dir.Write(Path.Combine("x", "y", "z", "c.bin"), TestData.Bytes(10, 3));

            DirectoryHash result = await ScanAsync();

            result.Files.Select(f => f.FilePath).Should().BeEquivalentTo([a, b, c]);
        }

        [Fact]
        public async Task Each_scanned_hash_equals_hashing_that_file_directly()
        {
            _dir.Write("small.bin", TestData.Bytes(50, 1));
            _dir.Write("sub/large.bin", TestData.Bytes(1000, 2));
            HashService service = Service();

            DirectoryHash result = await ScanAsync(service);

            result.Files.Should().HaveCount(2);
            foreach (FileHash scanned in result.Files)
            {
                FileHash direct = await service.HashFileAsync(scanned.FilePath);
                scanned.Should().Be(direct);
            }
        }

        [Fact]
        public async Task Mix_of_small_and_large_files_uses_the_right_strategy_for_each()
        {
            _dir.Write("small.bin", TestData.Bytes(50, 1));
            _dir.Write("large.bin", TestData.Bytes(1000, 2));

            DirectoryHash result = await ScanAsync();

            result.Files.Select(f => f.Hashes.Length).Should().BeEquivalentTo([1, 4]);
        }

        [Fact]
        public async Task Scanning_twice_gives_equal_results_with_many_workers()
        {
            for (int i = 0; i < 50; i++)
            {
                _dir.Write($"d{i % 5}/f{i}.bin", TestData.Bytes(20 + i * 30, i));
            }

            HashService service = Service(TestData.Options(workerCount: 8));

            DirectoryHash first = await ScanAsync(service);
            DirectoryHash second = await ScanAsync(service);

            first.Files.Should().HaveCount(50);
            first.Should().Be(second);
        }

        [Fact]
        public async Task Changing_a_file_makes_scans_differ()
        {
            string path = _dir.Write("a.bin", TestData.Bytes(50));
            _dir.Write("b.bin", TestData.Bytes(50, 2));
            DirectoryHash before = await ScanAsync();

            File.WriteAllBytes(path, TestData.Bytes(50, 99));
            DirectoryHash after = await ScanAsync();

            before.Should().NotBe(after);
        }

        [Fact]
        public async Task Adding_a_file_makes_scans_differ()
        {
            _dir.Write("a.bin", TestData.Bytes(50));
            DirectoryHash before = await ScanAsync();

            _dir.Write("b.bin", TestData.Bytes(50, 2));
            DirectoryHash after = await ScanAsync();

            before.Should().NotBe(after);
        }

        [Fact]
        public async Task Deleting_a_file_makes_scans_differ()
        {
            string path = _dir.Write("a.bin", TestData.Bytes(50));
            _dir.Write("b.bin", TestData.Bytes(50, 2));
            DirectoryHash before = await ScanAsync();

            File.Delete(path);
            DirectoryHash after = await ScanAsync();

            before.Should().NotBe(after);
        }

        [Fact]
        public async Task Renaming_a_file_keeps_its_hash_but_changes_its_path()
        {
            string original = _dir.Write("a.bin", TestData.Bytes(50));
            DirectoryHash before = await ScanAsync();

            string renamed = _dir.Combine("renamed.bin");
            File.Move(original, renamed);
            DirectoryHash after = await ScanAsync();

            before.Files[0].Should().Be(after.Files[0]);
            before.Files[0].FilePath.Should().NotBe(after.Files[0].FilePath);
        }

        [Fact]
        public async Task Files_with_same_content_have_equal_hashes()
        {
            byte[] content = TestData.Bytes(1000);
            _dir.Write("a.bin", content);
            _dir.Write("sub/copy.bin", content);
            _dir.Write("other.bin", TestData.Bytes(1000, 2));

            DirectoryHash result = await ScanAsync();

            var duplicateGroups = result.Files.GroupBy(f => f).Where(g => g.Count() > 1).ToArray();
            duplicateGroups.Should().ContainSingle();
            duplicateGroups[0].Select(f => Path.GetFileName(f.FilePath)).Should().BeEquivalentTo(["a.bin", "copy.bin"]);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 8)]
        [InlineData(8, 1)]
        [InlineData(4, 4)]
        public async Task Result_does_not_depend_on_concurrency_settings(int channelCapacity, int workerCount)
        {
            for (int i = 0; i < 30; i++)
            {
                _dir.Write($"f{i}.bin", TestData.Bytes(20 + i * 40, i));
            }

            DirectoryHash reference = await ScanAsync(Service(TestData.Options(channelCapacity: 64, workerCount: 2)));
            DirectoryHash result = await ScanAsync(Service(TestData.Options(channelCapacity: channelCapacity, workerCount: workerCount)));

            result.Should().Be(reference);
        }

        [Fact]
        public async Task Missing_root_throws_DirectoryNotFound_and_does_not_hang()
        {
            HashService service = Service();

            Func<Task> act = async () => await service.ScanDirectoryAndHashAsync(_dir.Combine("missing"), null, default).WaitAsync(Timeout);

            await act.Should().ThrowAsync<DirectoryNotFoundException>();
        }

        [Fact]
        public async Task Pre_cancelled_token_throws_OperationCanceled()
        {
            _dir.Write("a.bin", TestData.Bytes(50));
            using CancellationTokenSource cts = new();
            cts.Cancel();

            Func<Task> act = async () => await ScanAsync(ct: cts.Token);

            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task Cancelling_mid_scan_throws_OperationCanceled_and_does_not_hang()
        {
            for (int i = 0; i < 5; i++)
            {
                _dir.Write($"f{i}.bin", TestData.Bytes(50, i));
            }

            TaskCompletionSource started = new(TaskCreationOptions.RunContinuationsAsynchronously);
            DelegatingHashProvider provider = new(_provider)
            {
                BeforeHashFile = async (_, ct) =>
                {
                    started.TrySetResult();
                    await Task.Delay(System.Threading.Timeout.Infinite, ct);
                }
            };

            using CancellationTokenSource cts = new();
            Task<DirectoryHash> scan = ScanAsync(Service(provider: provider), ct: cts.Token);

            await started.Task.WaitAsync(Timeout);
            cts.Cancel();

            Func<Task> act = async () => await scan;
            await act.Should().ThrowAsync<OperationCanceledException>();
        }

        [Fact]
        public async Task Provider_failure_on_one_file_aborts_the_whole_scan()
        {
            for (int i = 0; i < 6; i++)
            {
                _dir.Write($"f{i}.bin", TestData.Bytes(50, i));
            }

            DelegatingHashProvider provider = new(_provider)
            {
                BeforeHashFile = (path, _) =>
                    Path.GetFileName(path) == "f0.bin"
                        ? throw new InvalidOperationException("boom")
                        : Task.CompletedTask
            };

            Func<Task> act = async () => await ScanAsync(Service(provider: provider));

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        }

        [Fact]
        public async Task Provider_failure_does_not_hang_when_single_worker_and_small_channel()
        {
            for (int i = 0; i < 10; i++)
            {
                _dir.Write($"f{i}.bin", TestData.Bytes(50, i));
            }

            DelegatingHashProvider provider = new(_provider)
            {
                BeforeHashFile = (path, _) =>
                    Path.GetFileName(path) == "f0.bin"
                        ? throw new InvalidOperationException("boom")
                        : Task.CompletedTask
            };
            HashService service = Service(TestData.Options(channelCapacity: 1, workerCount: 1), provider);

            Func<Task> act = async () => await ScanAsync(service);

            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task Final_progress_report_has_exact_totals()
        {
            _dir.Write("a.bin", TestData.Bytes(10, 1));
            _dir.Write("sub/b.bin", TestData.Bytes(1000, 2));
            _dir.Write("sub/c.bin", TestData.Bytes(250, 3));
            SyncProgress progress = new();

            await ScanAsync(progress: progress);

            HashProgress last = progress.Reports.Last();
            last.FilesRead.Should().Be(3);
            last.FilesHashed.Should().Be(3);
            last.BytesHashed.Should().Be(1260);
        }

        [Fact]
        public async Task Progress_reports_never_exceed_totals()
        {
            for (int i = 0; i < 20; i++)
            {
                _dir.Write($"f{i}.bin", TestData.Bytes(30, i));
            }

            SyncProgress progress = new();

            await ScanAsync(progress: progress);

            progress.Reports.Should().OnlyContain(p => p.FilesRead <= 20 && p.FilesHashed <= 20 && p.BytesHashed <= 600);
        }

        [Fact]
        public async Task Null_progress_is_allowed()
        {
            _dir.Write("a.bin", TestData.Bytes(10));

            DirectoryHash result = await ScanAsync(progress: null);

            result.Files.Should().HaveCount(1);
        }
    }
}
