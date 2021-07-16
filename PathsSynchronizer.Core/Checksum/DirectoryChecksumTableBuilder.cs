using HashDepot;
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
            var dirHandle = Directory.EnumerateFiles(DirectoryPath, "*", SearchOption.AllDirectories);

            foreach (string file in dirHandle)
            {
                ulong checksum = CalculateFileChecksum(file);
                dataDict.Add(file, checksum);
            }

            var table = new DirectoryChecksumTable(DirectoryPath, Mode, dataDict);
            return await Task.FromResult(table).ConfigureAwait(false);
        }

        private ulong CalculateFileChecksum(string filePath)
        {
            Stream hashFxInputStream = null;
            try
            {
                switch (Mode)
                {
                    case FileChecksumMode.FileName:
                        byte[] filePathBytes = Encoding.UTF8.GetBytes(filePath);
                        hashFxInputStream = new MemoryStream(filePathBytes);
                        break;
                    case FileChecksumMode.FileHash:
                        hashFxInputStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 16 * 1024 * 1024);
                        break;
                }

                ulong checksum = XXHash.Hash64(hashFxInputStream);
                return checksum;
            }
            finally
            {
                hashFxInputStream.Dispose();
            }
        }
    }
}
