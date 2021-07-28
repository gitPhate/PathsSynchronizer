using PathsSynchronizer.Core.Support.CSharpTest.Net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PathsSynchronizer.Core.Support.IO
{
    public class FastFileInfo
    {
        public string ParentPath { get; }
        public string ParentPathUnc { get; }
        public string FullPath { get; }
        public string FullPathUnc { get; }
        public string Name { get; }
        public string Extension { get; }
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

        public FastFileInfo(FindFile.Info info)
        {
            ParentPath = info.ParentPath;
            ParentPathUnc = info.ParentPathUnc;
            FullPath = info.FullPath;
            FullPathUnc = info.FullPathUnc;
            Name = info.Name;
            Extension = info.Extension;
            Length = info.Length;
            CreationTimeUtc = info.CreationTimeUtc;
            LastAccessTimeUtc = info.LastAccessTimeUtc;
            LastWriteTimeUtc = info.LastWriteTimeUtc;
            IsReadOnly = (info.Attributes & FileAttributes.ReadOnly) != 0;
            IsHidden = (info.Attributes & FileAttributes.Hidden) != 0;
            IsSystem = (info.Attributes & FileAttributes.System) != 0;
            IsDirectory = (info.Attributes & FileAttributes.Directory) != 0;
            IsReparsePoint = (info.Attributes & FileAttributes.ReparsePoint) != 0;
            IsCompressed = (info.Attributes & FileAttributes.Compressed) != 0;
            IsOffline = (info.Attributes & FileAttributes.Offline) != 0;
            IsEncrypted = (info.Attributes & FileAttributes.Encrypted) != 0;
        }
    }

    public static class FastFileFinder
    {
        public static string[] GetFilePaths(string folder, string filePattern, bool recursive) => GetFilePaths(folder, filePattern, recursive, true, true);

        public static string[] GetFilePaths(string folder, string filePattern, bool recursive, bool includeFolders, bool includeFiles) =>
            InternalGetFiles(folder, filePattern, recursive, includeFolders, includeFiles)
                .Select(x => x.FullPath)
                .ToArray();

        public static FastFileInfo[] GetFiles(string folder, string filePattern, bool recursive, bool includeFolders, bool includeFiles) => InternalGetFiles(folder, filePattern, recursive, includeFolders, includeFiles);

        public static FastFileInfo[] GetFiles(string folder, string filePattern, bool recursive) => InternalGetFiles(folder, filePattern, recursive, true, true);

        private static FastFileInfo[] InternalGetFiles(string folder, string filePattern, bool recursive, bool includeFolders, bool includeFiles)
        {
            if (!includeFiles && !includeFolders)
            {
                return Array.Empty<FastFileInfo>();
            }

            IList<FindFile.Info> fileList = new List<FindFile.Info>();

            FindFile handler = new(folder, filePattern, recursive, includeFolders, includeFiles)
            {
                RaiseOnAccessDenied = false
            };

            handler.FileFound += (o, e) => fileList.Add(e.GetInfo());

            handler.Find();

            int count = fileList.Count;
            List<FastFileInfo> returnList = new(count);

            for (int i = 0; i < count; ++i)
            {
                FastFileInfo fi = new(fileList[i]);
                returnList.Add(fi);
            }

            return returnList.ToArray();
        }
    }
}
