namespace PathsSyncronizer.Test.Support
{
    public sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PathsSynchronizerTests_" + Guid.NewGuid().ToString("N"));

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public string Combine(string relativePath) => System.IO.Path.Combine(Path, relativePath);

        public string Write(string relativePath, byte[] content)
        {
            string fullPath = Combine(relativePath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
            File.WriteAllBytes(fullPath, content);
            return fullPath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
