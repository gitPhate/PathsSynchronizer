using PathsSynchronizer.Hashing;
using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace PathsSynchronizer
{
    readonly record struct FileTask(string Path, long Length);

    public record ServiceOptions(int SampleCount, int SampleBlockSize, long FullHashThreshold, int ProducerChannelCapacity, int WorkerCount, int IOConcurrency, int ReadBufferSize)
    {
        public static ServiceOptions SSD => new(16, 1 * 1024 * 1024, 100L * 1024 * 1024, 4096, Environment.ProcessorCount, 32, 256 * 1024);
        public static ServiceOptions ExternalHDD => new(16, 1 * 1024 * 1024, 100L * 1024 * 1024, 4096, Environment.ProcessorCount, 16, 1 * 1024 * 1024);
    }

    [method: JsonConstructor]
    public class DirectoryHash(string path, FileHash[] files)
    {
        public string Path { get; init; } = path;
        public FileHash[] Files { get; init; } = files;

        public override bool Equals(object? obj) =>
            obj is DirectoryHash other
                && string.Equals(Path ?? string.Empty, other.Path ?? string.Empty, StringComparison.Ordinal)
                && Files.OrderBy(x => x.FilePath).SequenceEqual(other.Files.OrderBy(x => x.FilePath));

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261;
                if (Files.Length != 0)
                {
                    const int p = 16777619;

                    for (int i = 0; i < Files.Length; i++)
                    {
                        hash = (hash ^ Files[i].GetHashCode()) * p;
                    }
                }

                return hash ^ (Path ?? string.Empty).GetHashCode();
            }
        }
    }

    public readonly record struct HashProgress(int FilesRead, int FilesHashed, long BytesHashed);
}