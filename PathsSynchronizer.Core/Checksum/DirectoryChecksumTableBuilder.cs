using HashDepot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Checksum
{
    public class DirectoryChecksumTableBuilder<TChecksum>
    {
        private readonly Func<Stream, TChecksum> _hashFunction;

        public string DirectoryPath { get; }
        public FileChecksumMode Mode { get; }

        public DirectoryChecksumTableBuilder(string directoryPath, FileChecksumMode mode, Func<Stream, TChecksum> hashFunction)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                throw new ArgumentNullException(nameof(directoryPath), "The provided path is null or blank");
            }

            if (!Directory.Exists(directoryPath))
            {
                throw new DirectoryNotFoundException($"The path {directoryPath} has not been found");
            }

            _hashFunction = hashFunction;

            DirectoryPath = directoryPath;
            Mode = mode;
        }

        //public async Task<DirectoryChecksumTable<TChecksum>> BuildAsync(int maxParallelOperations)
        //{
        //    IDictionary<string, TChecksum> dataDict = new Dictionary<string, TChecksum>();
        //    var dirHandle = Directory.EnumerateFiles(DirectoryPath, "*", SearchOption.AllDirectories);

        //    foreach(string file in dirHandle)
        //    {
        //        TChecksum checksum = CalculateFileChecksum(file);
        //        dataDict.Add(file, checksum);
        //    }
        //}

        private TChecksum CalculateFileChecksum(string filePath)
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

                TChecksum checksum = _hashFunction(hashFxInputStream);
                return checksum;
            }
            finally
            {
                hashFxInputStream.Dispose();
            }
        }
    }
}
