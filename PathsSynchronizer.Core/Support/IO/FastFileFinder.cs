using PathsSynchronizer.Core.Support.CSharpTest.Net;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PathsSynchronizer.Core.Support.IO
{
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
            IList<FastFileInfo> returnList = new List<FastFileInfo>(count);

            for (int i = 0; i < count; ++i)
            {
                FastFileInfo fi = new(fileList[i]);
                returnList.Add(fi);
            }

            return returnList.ToArray();
        }
    }
}
