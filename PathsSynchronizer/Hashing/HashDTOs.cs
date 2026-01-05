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

        public bool Equals(DataHash other)
        {
            if (ReferenceEquals(Bytes, other.Bytes)) return true; // same array
            if (Bytes.Length != other.Bytes.Length) return false;

            for (int i = 0; i < Bytes.Length; ++i)
                if (Bytes[i] != other.Bytes[i]) return false;

            return true;
        }

        public override bool Equals(object? obj) => obj is DataHash xx && Equals(xx);

        public override int GetHashCode()
        {
            if (Bytes is null || Bytes.Length == 0) return 0;

            unchecked
            {
                const int p = 16777619;
                int hash = (int)2166136261;

                for (int i = 0; i < Bytes.Length; i++)
                    hash = (hash ^ Bytes[i]) * p;

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

        public override bool Equals(object? obj)
        {
            if (obj is null || obj is not FileHash other)
            {
                return false;
            }

            if (Hashes.Length != other.Hashes.Length)
            {
                return false;
            }

            for (int i = 0; i < Hashes.Length; ++i)
            {
                if (!Hashes[i].Equals(other.Hashes[i]))
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
    }
}
