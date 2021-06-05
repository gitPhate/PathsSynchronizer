namespace PathsSynchronizer.Core.Checksum
{
    public class FileChecksum<T>
    {
        public FileChecksumMode Mode { get; }
        public T Checksum { get; }
    }
}
