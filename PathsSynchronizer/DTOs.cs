using PathsSynchronizer.Hashing;
using System;
using System.Text.Json.Serialization;

namespace PathsSynchronizer
{
    readonly record struct FileTask(string Path, long Length);

    public record ServiceOptions(int SampleCount, int SampleBlockSize, long FullHashThreshold, int ProducerChannelCapacity, int WorkerCount, int IOConcurrency)
    {
        public static ServiceOptions SSD => new(16, 1 * 1024 * 1024, 100L * 1024 * 1024, 4096, Environment.ProcessorCount * 2, 128);
        public static ServiceOptions ExternalHDD => new(16, 1 * 1024 * 1024, 100L * 1024 * 1024, 4096, Environment.ProcessorCount, 32);
    }

    [method: JsonConstructor]
    public class DirectoryHash(string path, FileHash[] files)
    {
        public string Path { get; set; } = path;
        public FileHash[] Files { get; set; } = files;

        public override bool Equals(object? obj)
        {
            if (obj is null || obj is not DirectoryHash other)
            {
                return false;
            }

            if(!(Path ?? string.Empty).Equals(other.Path))
            {
                return false;
            }

            for (int i = 0; i < Files.Length; ++i)
            {
                if (!Files[i].Equals(other.Files[i]))
                {
                    return false;
                }
            }

            return true;
        }

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