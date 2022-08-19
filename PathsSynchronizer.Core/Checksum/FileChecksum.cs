using System.Text.Json.Serialization;

namespace PathsSynchronizer.Core.Checksum
{
    public class FileChecksum<THash> where THash : notnull
    {
        public string FilePath { get; }
        public THash Hash { get; }

        [JsonConstructor]
        public FileChecksum(string filePath, THash hash)
        {
            FilePath = filePath;
            Hash = hash;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = (int)2166136261;
                hash = (hash * 16777619) ^ (Hash?.GetHashCode() ?? 0);
                return hash;
            }
        }

        public override bool Equals(object? obj)
        {
            if (obj is not FileChecksum<THash> fileChecksum)
            {
                return false;
            }

            return fileChecksum.Hash.Equals(Hash);
        }

        public override string ToString() => Hash?.ToString() ?? string.Empty;
    }
}
