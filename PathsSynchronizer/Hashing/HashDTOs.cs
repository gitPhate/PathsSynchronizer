using System;
using System.Linq;
using System.Text.Json.Serialization;

namespace PathsSynchronizer.Hashing
{
    [method: JsonConstructor]
    public readonly struct DataHash(byte[] bytes)
    {
        public byte[] Bytes { get; } = bytes;

        [JsonIgnore]
        public string Hash => Convert.ToHexString(Bytes);

        public bool Equals(DataHash other) => Bytes.SequenceEqual(other.Bytes);

        public override bool Equals(object? obj) => obj is DataHash xx && Equals(xx);

        public override int GetHashCode()
        {
            if (Bytes is null || Bytes.Length == 0) return 0;

            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                ReadOnlySpan<byte> span = Bytes.AsSpan();
                for (int i = 0; i < span.Length; i++)
                    hash = (hash ^ span[i]) * p;

                return hash;
            }
        }

        public static bool operator ==(DataHash left, DataHash right) => left.Equals(right);

        public static bool operator !=(DataHash left, DataHash right) => !(left == right);
    }

    [method: JsonConstructor]
    public class FileHash(string filePath, DataHash[] hashes)
    {
        public string FilePath { get; } = filePath;
        public DataHash[] Hashes { get; } = hashes;

        public FileHash(string filePath, DataHash hash)
            : this(filePath, [hash])
        {
        }

        public override bool Equals(object? obj) =>
            obj is FileHash other && Hashes.SequenceEqual(other.Hashes);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261;
                if (Hashes.Length != 0)
                {
                    const int p = 16777619;

                    for (int i = 0; i < Hashes.Length; i++)
                    {
                        hash = (hash ^ Hashes[i].GetHashCode()) * p;
                    }
                }

                return hash;
            }
        }

        public static bool operator ==(FileHash left, FileHash right) => left.Equals(right);

        public static bool operator !=(FileHash left, FileHash right) => !(left == right);
    }
}
