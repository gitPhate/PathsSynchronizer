using PathsSynchronizer.Core.Hashing;

namespace PathsSynchronizer.Hashing.XXHash
{
    public readonly struct XXHash(byte[] bytes) : IHash
    {
        public byte[] Bytes { get; } = bytes;

        public bool Equals(IHash? other)
        {
            if (other is null || other is not XXHash xx) return false;
            return Equals(xx);
        }

        public bool Equals(XXHash other)
        {
            if (ReferenceEquals(Bytes, other.Bytes)) return true; // same array
            if (Bytes.Length != other.Bytes.Length) return false;

            for (int i = 0; i < Bytes.Length; i++)
                if (Bytes[i] != other.Bytes[i]) return false;

            return true;
        }

        public override bool Equals(object? obj) => obj is XXHash xx && Equals(xx);

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

        public static bool operator ==(XXHash left, XXHash right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(XXHash left, XXHash right)
        {
            return !(left == right);
        }
    }
}
