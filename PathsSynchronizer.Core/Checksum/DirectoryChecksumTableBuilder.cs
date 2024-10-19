using PathsSynchronizer.Core.Hashing;
using PathsSynchronizer.Core.Support.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PathsSynchronizer.Core.Checksum
{
    public static class DirectoryChecksumTableBuilder
    {
        public static DirectoryChecksumTableBuilder<THash> CreateNew<THash>() where THash : notnull
        {
            return new DirectoryChecksumTableBuilder<THash>();
        }
    }

    public class DirectoryChecksumTableBuilder<THash> where THash : notnull
    {
        private IHashProvider<THash>? _hashProvider;
        private FileChecksumMode? _fileChecksumMode;
        private FileHashProvider<THash>? _fileHashProvider;

        internal DirectoryChecksumTableBuilder()
        {
        }

        public DirectoryChecksumTableBuilder<THash> WithFileHashMode()
        {
            _fileChecksumMode = FileChecksumMode.FileHash;
            return this;
        }

        public DirectoryChecksumTableBuilder<THash> WithFileNameMode()
        {
            _fileChecksumMode = FileChecksumMode.FileName;
            return this;
        }

        public DirectoryChecksumTableBuilder<THash> WithHashProvider(IHashProvider<THash> hashProvider)
        {
            _hashProvider = hashProvider;
            return this;
        }

        public async Task<DirectoryChecksumTable<THash>> BuildAsync(string folderPath)
        {
            if (_fileChecksumMode == null)
            {
                throw new InvalidOperationException("Hash mode not set");
            }

            if (_hashProvider == null)
            {
                throw new InvalidOperationException("Hash provider not set");
            }

            if (!Directory.Exists(folderPath))
            {
                throw new DirectoryNotFoundException($"The directory {folderPath} was not found");
            }

            _fileHashProvider = new(_hashProvider, _fileChecksumMode.Value);
            var files = IOHelper.EnumerateFiles(folderPath, x => !x.Contains("system volume information", StringComparison.OrdinalIgnoreCase) && !x.Contains("recycle", StringComparison.OrdinalIgnoreCase), "*");
            Dictionary<string, FileChecksum<THash>> data = new();

            foreach (string filePath in files)
            {
                THash hash = await _fileHashProvider.HashFileAsync(filePath).ConfigureAwait(false);
                FileChecksum<THash> fileChecksum = new(filePath, hash);
                data.Add(filePath, fileChecksum);
            }

            return new DirectoryChecksumTable<THash>(folderPath, _fileChecksumMode.Value, data);
        }
    }
}
