using PathsSynchronizer.Core.Support.CSharpTest.Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PathsSynchronizer.Core.Support.IO
{
    public class FastFileInfo
    {
        public long Length { get; }
        public DateTime CreationTimeUtc { get; }
        public DateTime LastAccessTimeUtc { get; }
        public DateTime LastWriteTimeUtc { get; }
        public bool IsReadOnly { get; }
        public bool IsHidden { get; }
        public bool IsSystem { get; }
        public bool IsDirectory { get; }
        public bool IsReparsePoint { get; }
        public bool IsCompressed { get; }
        public bool IsOffline { get; }
        public bool IsEncrypted { get; }

        public FastFileInfo(long length, DateTime creationTimeUtc, DateTime lastAccessTimeUtc, DateTime lastWriteTimeUtc, bool isReadOnly, bool isHidden, bool isSystem, bool isDirectory, bool isReparsePoint, bool isCompressed, bool isOffline, bool isEncrypted)
        {
            Length = length;
            CreationTimeUtc = creationTimeUtc;
            LastAccessTimeUtc = lastAccessTimeUtc;
            LastWriteTimeUtc = lastWriteTimeUtc;
            IsReadOnly = isReadOnly;
            IsHidden = isHidden;
            IsSystem = isSystem;
            IsDirectory = isDirectory;
            IsReparsePoint = isReparsePoint;
            IsCompressed = isCompressed;
            IsOffline = isOffline;
            IsEncrypted = isEncrypted;
        }
    }

    public static class FastFileFinder
    {
        public static IList<string> GetFiles(string folder, string filePattern, bool recursive) => GetFiles(folder, filePattern, recursive, true, true);
        public static IList<string> GetFiles(string folder, string filePattern, bool recursive, bool includeFolders, bool includeFiles)
        {
            if (!includeFiles && !includeFolders)
            {
                return Array.Empty<string>();
            }

            IList<(bool, string)> fileList = new List<(bool, string)>();

            FindFile handler = new(folder, filePattern, recursive, includeFolders, includeFiles)
            {
                RaiseOnAccessDenied = false
            };

            handler.FileFound += (o, e) => fileList.Add((e.IsDirectory, e.FullPath));

            handler.Find();

            Func<(bool, string), bool> wherePredicate = x => true;

            if (includeFolders != includeFiles)
            {
                if (includeFolders)
                {
                    wherePredicate = x => x.Item1;
                }
                else
                {
                    wherePredicate = x => !x.Item1;
                }
            }

            return
                fileList
                    .Where(wherePredicate)
                    .Select(x => x.Item2)
                    .ToArray();
        }
    }
}
