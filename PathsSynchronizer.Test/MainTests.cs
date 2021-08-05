using Microsoft.VisualStudio.TestTools.UnitTesting;
using PathsSynchronizer.Core.Checksum;
using PathsSynchronizer.Core.Support.CSharpTest.Net;
using PathsSynchronizer.Core.Support.IO;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace PathsSynchronizer.Test
{
    [TestClass]
    public class MainTests
    {
        [TestMethod]
        public async Task TestDirectoryChecksumTableSerialization()
        {
            Stopwatch sw = Stopwatch.StartNew();
            DirectoryChecksumTable table =
                DirectoryChecksumTableBuilder
                    .CreateNew
                    (
                        @"C:\development\dotnet\GitFashion",
                        //@"C:\temp",
                        new DirectoryChecksumTableBuilderOptions
                        {
                            HashingPlatform = Core.Support.XXHash.XXHashPlatform.x86,
                            MaxParallelOperations = 1000,
                            Mode = FileChecksumMode.FileHash
                        }
                    )
                    .Build();

            var elapsed = sw.Elapsed;

            byte[] bytes = await table.SerializeAsync().ConfigureAwait(false);
            Assert.IsTrue(bytes.Any());

            DirectoryChecksumTable tableDeserialized = await DirectoryChecksumTable.DeserializeAsync(bytes).ConfigureAwait(false);
            Assert.IsTrue(table.Count == tableDeserialized.Count);
            CollectionAssert.AreEqual(table.Keys.ToArray(), tableDeserialized.Keys.ToArray());
        }

        [TestMethod]
        public void FindInFilesTest()
        {
            //No actual test, just for testing the FindFile class
            var fcounter = new FindFile(@"C:\Publishes", "*", true, true, true);
            fcounter.RaiseOnAccessDenied = false;

            List<string> paths = new();
            long size = 0, total = 0;
            fcounter.FileFound +=
                (o, e) =>
                {
                    paths.Add(e.FullPath);
                    //if (!e.IsDirectory)
                    //{
                    //    total++;
                    //    size += e.Length;
                    //}
                };

            Stopwatch sw = Stopwatch.StartNew();
            fcounter.Find();
            Console.WriteLine("Enumerated {0:n0} files totaling {1:n0} bytes in {2:n3} seconds.",
                              total, size, sw.Elapsed.TotalSeconds);
        }

        [TestMethod]
        public void FastFileOnlyPathsVsAllDataBenchmarch()
        {
            Stopwatch sw = Stopwatch.StartNew();
            var fileListOld = OldFastFileFinder.GetFiles(@"C:\development", "*", true);

            var oldElapsed = sw.Elapsed;
            sw.Restart();

            var fileList = FastFileFinder.GetFiles(@"C:\development", "*", true, false, true);

            var newElapsed = sw.Elapsed;
        }

        [TestMethod]
        public void FastFileFinderTestFilterFileOrDirInsideEventHandlerOrLater()
        {
            IList<(bool, string)> fileList = new List<(bool, string)>();

            FindFile handler = new(@"C:\development", "*", true, false, true)
            {
                RaiseOnAccessDenied = false
            };

            handler.FileFound += (o, e) => fileList.Add((e.IsDirectory, e.FullPath));

            Stopwatch sw = Stopwatch.StartNew();
            handler.Find();
            var onlyFileList = fileList.Where(x => !x.Item1).ToArray();
            var noIfElapsed = sw.Elapsed;

            //----

            IList<string> fileList2 = new List<string>();

            FindFile handler2 = new(@"C:\development", "*", true, false, true)
            {
                RaiseOnAccessDenied = false
            };

            handler2.FileFound += (o, e) => { if (!e.IsDirectory) fileList2.Add(e.FullPath); };

            Stopwatch sw2 = Stopwatch.StartNew();
            handler2.Find();

            var yesIfElapsed = sw2.Elapsed;

            var delta = yesIfElapsed - noIfElapsed;
        }

        [TestMethod]
        public void CollectionAddBenchmark()
        {
            int max = 4500000;
            List<int> list = new();
            LinkedList<int> linkedlist = new();
            Queue<int> queue = new();
            HashSet<int> hashset = new();
            int[] array = new int[max];

            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < max; ++i)
            {
                list.Add(i);
            }

            var listElapsed = sw.Elapsed;
            sw.Restart();

            for (int i = 0; i < max; ++i)
            {
                linkedlist.AddLast(i);
            }

            var linkedListElapsed = sw.Elapsed;
            sw.Restart();

            for (int i = 0; i < max; ++i)
            {
                queue.Enqueue(i);
            }

            var queueElapsed = sw.Elapsed;
            sw.Restart();

            for (int i = 0; i < max; ++i)
            {
                hashset.Add(i);
            }

            var hashSetElapsed = sw.Elapsed;
            sw.Restart();

            for (int i = 0; i < max; ++i)
            {
                array[i] = i;
            }

            var arrayElapsed = sw.Elapsed;
            sw.Restart();

            //List wins
        }
    }

    public static class OldFastFileFinder
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
