using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PathsSynchronizer.Core.Support.IO
{
    internal class IOHelper
    {
        public static IEnumerable<string> EnumerateFiles(string rootDirectory, Func<string, bool> directoryFilter, string filePattern)
        {
            foreach (string matchedFile in Directory.EnumerateFiles(rootDirectory, filePattern, SearchOption.TopDirectoryOnly))
            {
                yield return matchedFile;
            }

            var matchedDirectories =
                Directory
                    .EnumerateDirectories(rootDirectory, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(directoryFilter);

            foreach (string? dir in matchedDirectories)
            {
                foreach (string file in EnumerateFiles(dir, directoryFilter, filePattern))
                {
                    yield return file;
                }
            }
        }
    }
}
