using PathsSynchronizer.Core.Support.CSharpTest.Net;
using System.Collections.Generic;

namespace PathsSynchronizer.Core.Support.IO
{
    public static class FastFileFinder
    {
        public static IList<string> GetFiles(string folder, string filePattern, bool recursive) => GetFiles(folder, filePattern, recursive, true, true);
        public static IList<string> GetFiles(string folder, string filePattern, bool recursive, bool includeFolders, bool includeFiles)
        {
            var handler = new FindFile(folder, filePattern, recursive, includeFolders, includeFiles);
            handler.RaiseOnAccessDenied = false;

            IList<string> fileList = new List<string>();
            handler.FileFound +=
                (o, e) =>
                {
                    if (!e.IsDirectory)
                    {
                        fileList.Add(e.FullPath);
                    }
                };

            handler.Find();

            return fileList;
        }
    }
}
