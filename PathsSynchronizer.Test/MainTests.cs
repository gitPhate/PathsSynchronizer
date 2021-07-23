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

            DirectoryChecksumTable tableDeserialized = await DirectoryChecksumTable.FromSerializedAsync(bytes).ConfigureAwait(false);
            Assert.IsTrue(table.Count == tableDeserialized.Count);
            CollectionAssert.AreEqual(table.Keys.ToArray(), tableDeserialized.Keys.ToArray());
        }

        [TestMethod]
        public void FindInFilesTest()
        {
            //No actual test, just for testing the FindFile class
            var fcounter = new FindFile(@"C:\development", "*", true, true, true);
            fcounter.RaiseOnAccessDenied = false;

            long size = 0, total = 0;
            fcounter.FileFound +=
                (o, e) =>
                {
                    if (!e.IsDirectory)
                    {
                        total++;
                        size += e.Length;
                    }
                };

            Stopwatch sw = Stopwatch.StartNew();
            fcounter.Find();
            Console.WriteLine("Enumerated {0:n0} files totaling {1:n0} bytes in {2:n3} seconds.",
                              total, size, sw.Elapsed.TotalSeconds);
        }

        [TestMethod]
        public void FastFileFinderTest()
        {
            //No actual test, just for testing the FastFileFinder class
            var fileList = FastFileFinder.GetFiles(@"C:\temp", "*", true, true, false);
        }

        [TestMethod]
        public void FastFileFinderTestFilterFileOrDirInsideEventHandlerOrLater()
        {
            IList<(bool, string)> fileList = new List<(bool, string)>();

            FindFile handler = new(@"C:\development\dotnet\GitFashion", "*", true, false, true)
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

            FindFile handler2 = new(@"C:\development\dotnet\GitFashion", "*", true, false, true)
            {
                RaiseOnAccessDenied = false
            };

            handler2.FileFound += (o, e) => { if (!e.IsDirectory) fileList2.Add(e.FullPath); };

            Stopwatch sw2 = Stopwatch.StartNew();
            handler2.Find();

            var yesIfElapsed = sw.Elapsed;

            var delta = yesIfElapsed - noIfElapsed;
        }
    }
}
