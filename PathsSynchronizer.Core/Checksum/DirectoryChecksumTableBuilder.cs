using Hector.Core.Support;
using PathsSynchronizer.Core.Support.IO;
using PathsSynchronizer.Core.Support.XXHash;
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
        private readonly Func<Stream, ulong> _XXHashFx;

        public string DirectoryPath { get; }
        public FileChecksumMode Mode { get; }
        public int MaxParallelOperations { get; }
        public XXHashPlatform HashingPlatform { get; }

        public static DirectoryChecksumTableBuilder CreateNew(string directoryPath, DirectoryChecksumTableBuilderOptions options)
        {
            return new DirectoryChecksumTableBuilder(directoryPath, options);
        }

        protected DirectoryChecksumTableBuilder(string directoryPath, DirectoryChecksumTableBuilderOptions options)
        {
            directoryPath.AssertHasText(nameof(directoryPath), "The provided path is null or blank");

            options ??= DirectoryChecksumTableBuilderOptions.Default;

            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"The path {directoryPath} has not been found");
            }

            DirectoryPath = directoryPath;
            Mode = options.Mode;
            MaxParallelOperations = options.MaxParallelOperations;
            HashingPlatform = options.HashingPlatform;

            _XXHashFx = XXHashHelper.GetHashFx(HashingPlatform);
        }

        public DirectoryChecksumTable Build()
        {
            ConcurrentDictionary<ulong, string> dataDict = new();
            IList<string> fileList = FastFileFinder.GetFilePaths(DirectoryPath, "*", true);

            Func<string, ulong> hashModeFx = Mode switch
            {
                FileChecksumMode.FileHash => CalculateFileChecksumUsingFileContent,
                FileChecksumMode.FileName => CalculateFileChecksumUsingFilePath,
                _ => throw new NotImplementedException()
            };

            Parallel
                .ForEach
                (
                    fileList,
                    new ParallelOptions { MaxDegreeOfParallelism = MaxParallelOperations <= 0 ? fileList.Count : MaxParallelOperations },
                    file =>
                    {
                        ulong checksum = hashModeFx(file);
                        dataDict.TryAdd(checksum, file);
                    }
                );

            DirectoryChecksumTable table = new(DirectoryPath, Mode, dataDict);
            return table;
        }

        private ulong CalculateFileChecksumUsingFileContent(string filePath)
        {
            using Stream hashFxInputStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096);
            ulong checksum = _XXHashFx(hashFxInputStream);
            return checksum;
        }

        private ulong CalculateFileChecksumUsingFilePath(string filePath)
        {
            byte[] filePathBytes = Encoding.UTF8.GetBytes(filePath);
            using Stream hashFxInputStream = new MemoryStream(filePathBytes);
            ulong checksum = _XXHashFx(hashFxInputStream);
            return checksum;
        }
    }
}
