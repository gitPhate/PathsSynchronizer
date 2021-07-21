using HashDepot;
using PathsSynchronizer.Core.Support.IO;
using System;
using System.Collections.Concurrent;
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
            ConcurrentDictionary<string, ulong> dataDict = new();
            IList<string> fileList = FastFileFinder.GetFiles(DirectoryPath, "*", true);

            Func<string, ulong> _hashFx = Mode switch
            {
                FileChecksumMode.FileHash => CalculateFileChecksumUsingFileContent,
                FileChecksumMode.FileName => CalculateFileChecksumUsingFileName,
                _ => throw new NotImplementedException()
            };

            Parallel
                .ForEach
                (
                    fileList,
                    new ParallelOptions { MaxDegreeOfParallelism = maxParallelOperations <= 0 ? fileList.Count : maxParallelOperations },
                    file =>
                    {
                        ulong checksum = _hashFx(file);
                        dataDict.TryAdd(file, checksum);
                    }
                );

            var table = new DirectoryChecksumTable(DirectoryPath, Mode, dataDict);
            return await Task.FromResult(table).ConfigureAwait(false);
        }

        private ulong CalculateFileChecksumUsingFileContent(string filePath)
        {
            using Stream hashFxInputStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
            ulong checksum = XXHash.Hash32(hashFxInputStream);
            return checksum;
        }

        private ulong CalculateFileChecksumUsingFileName(string filePath)
        {
            byte[] filePathBytes = Encoding.UTF8.GetBytes(filePath);
            using Stream hashFxInputStream = new MemoryStream(filePathBytes);
            ulong checksum = XXHash.Hash32(hashFxInputStream);
            return checksum;
        }
    }
}
