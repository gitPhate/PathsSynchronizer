using FluentAssertions;
using PathsSynchronizer;
using PathsSynchronizer.Hashing;
using PathsSyncronizer.Test.Support;
using System.Text.Json;

namespace PathsSyncronizer.Test
{
    public class DataHashTests
    {
        [Fact]
        public void Same_bytes_are_equal()
        {
            DataHash a = TestData.Hash(1, 2, 3);
            DataHash b = TestData.Hash(1, 2, 3);

            a.Equals(b).Should().BeTrue();
            (a == b).Should().BeTrue();
            (a != b).Should().BeFalse();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void Different_bytes_are_not_equal()
        {
            (TestData.Hash(1, 2, 3) == TestData.Hash(1, 2, 4)).Should().BeFalse();
        }

        [Fact]
        public void Different_lengths_are_not_equal()
        {
            (TestData.Hash(1, 2, 3) == TestData.Hash(1, 2)).Should().BeFalse();
        }

        [Fact]
        public void Null_and_empty_bytes_are_equal_and_do_not_throw()
        {
            DataHash defaultHash = default;
            DataHash empty = TestData.Hash();

            defaultHash.Equals(empty).Should().BeTrue();
            defaultHash.GetHashCode().Should().Be(empty.GetHashCode());
        }

        [Fact]
        public void Hash_is_uppercase_hex()
        {
            TestData.Hash(0x0A, 0xFF, 0x00).Hash.Should().Be("0AFF00");
        }

        [Fact]
        public void Json_round_trip_preserves_bytes_and_omits_hash_string()
        {
            DataHash original = TestData.Hash(9, 8, 7);

            string json = JsonSerializer.Serialize(original);
            DataHash restored = JsonSerializer.Deserialize<DataHash>(json);

            json.Should().NotContain("\"Hash\"");
            restored.Should().Be(original);
        }
    }

    public class FileHashTests
    {
        [Fact]
        public void Same_hashes_with_different_paths_are_equal()
        {
            FileHash a = new("a.txt", TestData.Hash(1));
            FileHash b = new("b.txt", TestData.Hash(1));

            a.Equals(b).Should().BeTrue();
            (a == b).Should().BeTrue();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void Different_hashes_with_same_path_are_not_equal()
        {
            FileHash a = new("a.txt", TestData.Hash(1));
            FileHash b = new("a.txt", TestData.Hash(2));

            (a == b).Should().BeFalse();
        }

        [Fact]
        public void Hash_order_matters()
        {
            FileHash a = new("f", [TestData.Hash(1), TestData.Hash(2)]);
            FileHash b = new("f", [TestData.Hash(2), TestData.Hash(1)]);

            a.Equals(b).Should().BeFalse();
        }

        [Fact]
        public void Single_hash_constructor_equals_one_element_array()
        {
            FileHash single = new("f", TestData.Hash(5));
            FileHash array = new("f", [TestData.Hash(5)]);

            single.Should().Be(array);
        }

        [Fact]
        public void Json_round_trip_preserves_path_and_hashes()
        {
            FileHash original = new("dir/f.txt", [TestData.Hash(1, 2), TestData.Hash(3)]);

            string json = JsonSerializer.Serialize(original);
            FileHash? restored = JsonSerializer.Deserialize<FileHash>(json);

            restored.Should().NotBeNull();
            restored!.FilePath.Should().Be(original.FilePath);
            restored.Should().Be(original);
        }
    }

    public class DirectoryHashTests
    {
        private static FileHash File(string path, byte hash) => new(path, TestData.Hash(hash));

        [Fact]
        public void Same_path_and_files_are_equal()
        {
            DirectoryHash a = new("root", [File("a", 1), File("b", 2)]);
            DirectoryHash b = new("root", [File("a", 1), File("b", 2)]);

            a.Should().Be(b);
        }

        [Fact]
        public void File_order_does_not_matter()
        {
            DirectoryHash a = new("root", [File("a", 1), File("b", 2)]);
            DirectoryHash b = new("root", [File("b", 2), File("a", 1)]);

            a.Should().Be(b);
        }

        [Fact]
        public void Different_path_is_not_equal()
        {
            DirectoryHash a = new("root1", [File("a", 1)]);
            DirectoryHash b = new("root2", [File("a", 1)]);

            a.Should().NotBe(b);
        }

        [Fact]
        public void Missing_file_is_not_equal()
        {
            DirectoryHash a = new("root", [File("a", 1), File("b", 2)]);
            DirectoryHash b = new("root", [File("a", 1)]);

            a.Should().NotBe(b);
            b.Should().NotBe(a);
        }

        [Fact]
        public void Changed_file_hash_is_not_equal()
        {
            DirectoryHash a = new("root", [File("a", 1)]);
            DirectoryHash b = new("root", [File("a", 2)]);

            a.Should().NotBe(b);
        }

        [Fact]
        public void Json_round_trip_returns_equal_object()
        {
            DirectoryHash original = new("root", [File("a", 1), File("b", 2)]);

            string json = JsonSerializer.Serialize(original);
            DirectoryHash? restored = JsonSerializer.Deserialize<DirectoryHash>(json);

            restored.Should().Be(original);
        }
    }

    public class ServiceOptionsTests
    {
        public static TheoryData<string, ServiceOptions> Presets => new()
        {
            { nameof(ServiceOptions.SSD), ServiceOptions.SSD },
            { nameof(ServiceOptions.ExternalHDD), ServiceOptions.ExternalHDD }
        };

        [Theory]
        [MemberData(nameof(Presets))]
        public void Preset_values_are_positive_and_consistent(string name, ServiceOptions options)
        {
            name.Should().NotBeNullOrEmpty();
            options.SampleCount.Should().BePositive();
            options.SampleBlockSize.Should().BePositive();
            options.ProducerChannelCapacity.Should().BePositive();
            options.WorkerCount.Should().BePositive();
            options.IOConcurrency.Should().BePositive();
            options.ReadBufferSize.Should().BePositive();
            options.FullHashThreshold.Should().BeGreaterThanOrEqualTo(options.SampleBlockSize);
        }
    }
}
