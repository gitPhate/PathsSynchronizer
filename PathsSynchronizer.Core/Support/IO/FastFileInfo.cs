using PathsSynchronizer.Core.Support.CSharpTest.Net;
using System;
using System.IO;

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
}
