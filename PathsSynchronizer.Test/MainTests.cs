using Microsoft.VisualStudio.TestTools.UnitTesting;
using PathsSynchronizer.Core.Checksum;
using PathsSynchronizer.Core.Support.CSharpTest.Net;
using PathsSynchronizer.Core.Support.IO;
using System;
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
            var fileList = FastFileFinder.GetFiles(@"C:\development", "*", true, false, true);
        }
    }
}
