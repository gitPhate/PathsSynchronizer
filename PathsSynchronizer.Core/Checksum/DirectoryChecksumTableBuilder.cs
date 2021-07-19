using HashDepot;
using PathsSynchronizer.Core.Support.IO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Checksum
{
    public class DirectoryChecksumTableBuilder
    {
        public string DirectoryPath { get; }
        public FileChecksumMode Mode { get; }

        public static DirectoryChecksumTableBuilder CreateNew(string directoryPath, FileChecksumMode mode)
        {
            return new DirectoryChecksumTableBuilder(directoryPath, mode);
        }

        protected DirectoryChecksumTableBuilder(string directoryPath, FileChecksumMode mode)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentNullException(nameof(directoryPath), "The provided path is null or blank");
            }

            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"The path {directoryPath} has not been found");
            }

            DirectoryPath = directoryPath;
            Mode = mode;
        }

        public async Task<DirectoryChecksumTable> BuildAsync(int maxParallelOperations)
        {
            IDictionary<string, ulong> dataDict = new Dictionary<string, ulong>();
            var dirHandle = FastFileFinder.GetFiles(DirectoryPath, "*", true);

            Func<string, ulong> _hashFx = Mode switch
            {
                FileChecksumMode.FileHash => CalculateFileChecksumUsingFileContent,
                FileChecksumMode.FileName => CalculateFileChecksumUsingFileName,
                _ => throw new NotImplementedException()
            };

            foreach (string file in dirHandle)
            {
                ulong checksum = _hashFx(file);
                dataDict.Add(file, checksum);
            }

            var table = new DirectoryChecksumTable(DirectoryPath, Mode, dataDict);
            return await Task.FromResult(table).ConfigureAwait(false);
        }

        private ulong CalculateFileChecksumUsingFileContent(string filePath)
        {
            using Stream hashFxInputStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 16 * 1024 * 1024, true);
            ulong checksum = XXHash.Hash64(hashFxInputStream);
            return checksum;
        }

        private ulong CalculateFileChecksumUsingFileName(string filePath)
        {
            byte[] filePathBytes = Encoding.UTF8.GetBytes(filePath);
            using MemoryStream hashFxInputStream = new MemoryStream(filePathBytes);
            ulong checksum = XXHash.Hash64(hashFxInputStream);
            return checksum;
        }
    }
}
