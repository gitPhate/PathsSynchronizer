using PathsSynchronizer.Core.Checksum;

namespace PathsSynchronizer.Core.XXHash
{
    public static class XXHashDirectoryChecksumTableBuilder
    {
        public static DirectoryChecksumTableBuilder<ulong> CreateNew()
        {
            var builder = new DirectoryChecksumTableBuilder<ulong>();
            builder.WithHashProvider(new XXHashProvider());
            return builder;
        }
    }
}
