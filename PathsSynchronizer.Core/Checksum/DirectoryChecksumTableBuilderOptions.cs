using PathsSynchronizer.Core.Support.XXHash;

namespace PathsSynchronizer.Core.Checksum
{
    public class DirectoryChecksumTableBuilderOptions
    {
        public static readonly DirectoryChecksumTableBuilderOptions Default =
            new()
            {
                HashingPlatform = XXHashPlatform.x64,
                MaxParallelOperations = 1000,
                Mode = FileChecksumMode.FileHash
            };

        public FileChecksumMode Mode { get; set; }
        public int MaxParallelOperations { get; set; }
        public XXHashPlatform HashingPlatform { get; set; }
    }
}
