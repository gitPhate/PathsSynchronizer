using K4os.Hash.xxHash;
using PathsSynchronizer.Core.Checksum;
using PathsSynchronizer.Core.XXHash;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Management;
using System.Security.Cryptography;
using System.Threading.Tasks.Dataflow;

namespace PathsSyncronizer.Test
{
    public class MainTests
    {
        [Fact]
        public async Task Test1()
        {
            const string folder = @"E:\";

            DirectoryChecksumTable<ulong> table =
                await XXHashDirectoryChecksumTableBuilder
                    .CreateNew()
                    .WithFileHashMode()
                    .BuildAsync(folder)
                    .ConfigureAwait(false);

            byte[] bytes = await table.SerializeAsync().ConfigureAwait(false);

            DirectoryChecksumTable<ulong> table2 = await DirectoryChecksumTable<ulong>.DeserializeAsync(bytes);
        }

        [Fact]
        public async Task TestHashingLargeFile()
        {
            const string filePath1 = @"C:\Users\ServiceAccount\Desktop\300.avi";
            const string filePath2 = @"E:\Film\300.avi";

            bool isRemovableDrive = IsRemovableDrive(filePath1);
            int chucksBufferSize = isRemovableDrive ? 4096 : 1048576;

            ulong fileSystemHash = await FinalHashFileByChuncksAsync(filePath1, chucksBufferSize).ConfigureAwait(false);

            isRemovableDrive = IsRemovableDrive(filePath2);
            chucksBufferSize = isRemovableDrive ? 4096 : 1048576;

            ulong removableDriveHash = await FinalHashFileByChuncksAsync(filePath2, chucksBufferSize).ConfigureAwait(false);

            Assert.Equal(fileSystemHash, removableDriveHash);
        }

        private static async Task<ulong> NewHashFileByChuncksAsync(string filePath)
        {
            BufferBlock<Memory<byte>> dataFlowBuffer = new(new DataflowBlockOptions { EnsureOrdered = true });
            Task<ulong> consumerTask = ConsumeAsync(dataFlowBuffer);
            await ProduceAsync(filePath, dataFlowBuffer).ConfigureAwait(false);
            ulong chucksFileHash = await consumerTask.ConfigureAwait(false);
            return chucksFileHash;
        }

        private static async ValueTask ProduceAsync(string filePath, BufferBlock<Memory<byte>> dataFlowBuffer)
        {
            const int _chucksBufferSize = 1024 * 256; //0.5MB

            using FileStream fs = File.OpenRead(filePath);
            using BufferedStream bs = new(fs, _chucksBufferSize);

            int bytesRead;
            byte[] buffer = new byte[_chucksBufferSize];

            while ((bytesRead = await bs.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                Memory<byte> memory = buffer.AsMemory(0, bytesRead);
                dataFlowBuffer.Post(memory);
            }

            dataFlowBuffer.Complete();
        }

        class Item
        {
            public byte[] Buffer { get; set; }
            public int Length { get; set; }
        }

        private static async Task<ulong> ConsumeAsync(ISourceBlock<Memory<byte>> dataFlowBuffer)
        {
            XXH64 fileHash = new();
            while (await dataFlowBuffer.OutputAvailableAsync().ConfigureAwait(false))
            {
                Memory<byte> memory = await dataFlowBuffer.ReceiveAsync().ConfigureAwait(false);
                //Memory<byte> memory = item.Buffer.AsMemory(0, item.Length);
                //Debug.WriteLine($"Consumed {string.Join(",", memory.ToArray().Select(x => x.ToString()).ToArray())}");
                fileHash.Update(memory.Span);
            }

            ulong chucksFileHash = fileHash.Digest();
            return chucksFileHash;
        }

        private static async ValueTask<ulong> New2HashFileByChuncksAsync(string filePath)
        {
            //const int _chucksBufferSize = 1048576; //50MB
            const int _chucksBufferSize = 4096;

            XXH64 fileHash = new();

            using FileStream fs = File.OpenRead(filePath);
            using BufferedStream bs = new(fs, _chucksBufferSize);

            int bytesRead;
            byte[] buffer = new byte[_chucksBufferSize];
            ConcurrentBag<ulong> hashList = [];
            List<Task> taskList = [];
            while ((bytesRead = await bs.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                Task t = hashAsync(ref buffer, hashList);
                taskList.Add(t);
            }

            Task hashAsync(ref byte[] buffer, ConcurrentBag<ulong> list)
            {
                ulong hash = XXH64.DigestOf(buffer, 0, buffer.Length);
                list.Add(hash);
                return Task.CompletedTask;
            }

            ulong chucksFileHash = fileHash.Digest();
            return chucksFileHash;
        }

        //This is the final version of the hashing method
        //Tested on a maxtor external hdd and the filesystem
        //When on the normal filesystem set the buffer size to 1048576 (1 MB)
        private static async ValueTask<ulong> FinalHashFileByChuncksAsync(string filePath, int chucksBufferSize)
        {
            XXH64 fileHash = new();

            using FileStream fs = File.OpenRead(filePath);

            int bytesRead;
            byte[] buffer = new byte[chucksBufferSize];
            while ((bytesRead = await fs.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                fileHash.Update(buffer, 0, bytesRead);
            }

            ulong chucksFileHash = fileHash.Digest();
            return chucksFileHash;
        }

        static void TestProduce(ITargetBlock<byte[]> target)
        {
            const string filePath = @"C:\temp\prova.dat";
            const int _chucksBufferSize = 1;

            using FileStream fs = File.OpenRead(filePath);
            using BufferedStream bs = new(fs, _chucksBufferSize);

            int bytesRead;
            byte[] buffer = new byte[_chucksBufferSize];
            while ((bytesRead = bs.Read(buffer)) > 0)
            {
                byte[] buffer2 = new byte[bytesRead];
                buffer.CopyTo(buffer2, 0);
                target.Post(buffer2);
                //Debug.WriteLine($"Produced {string.Join(",", buffer.Select(x => x.ToString()).ToArray())}");
            }

            target.Complete();
        }

        static async Task<ulong> TestConsumeAsync(ISourceBlock<byte[]> source)
        {
            XXH64 fileHash = new();

            while (await source.OutputAvailableAsync())
            {
                byte[] data = await source.ReceiveAsync();
                Debug.WriteLine($"Consumed {string.Join(",", data.Select(x => x.ToString()).ToArray())}");
                fileHash.Update(data);
            }

            ulong chucksFileHash = fileHash.Digest();
            return chucksFileHash;
        }

        [Fact]
        public async Task ProducerConsumerTest()
        {
            BufferBlock<byte[]> buffer = new BufferBlock<byte[]>();
            Task<ulong> consumerTask = TestConsumeAsync(buffer);
            TestProduce(buffer);

            ulong bytesProcessed = await consumerTask;

            Console.WriteLine($"Processed {bytesProcessed:#,#} bytes.");
        }

        [Fact]
        public static string ComputeSHA256Hash()
        {
            const string filePath = @"E:\Film\300.avi";
            using FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            //using BufferedStream bufferedStream = new BufferedStream(fileStream, 1024 * 256);
            using SHA256 sha256 = SHA256.Create();
            byte[] hash = sha256.ComputeHash(fileStream);
            return BitConverter.ToString(hash).Replace("-", string.Empty);
        }

        private static async Task<ulong> HashFileOneShotAsync(string filePath)
        {
            byte[] allFile =
                await File
                    .ReadAllBytesAsync(filePath)
                    .ConfigureAwait(false);

            ulong oneShotFileHash = XXH64.DigestOf(allFile, 0, allFile.Length);
            return oneShotFileHash;
        }

        private static bool IsRemovableDrive(string path)
        {
            DriveInfo currentDrive = new(path);
            DriveInfo[] drives = DriveInfo.GetDrives();

            ManagementObjectCollection allPhysicalDisks = new ManagementObjectSearcher("select MediaType, DeviceID from Win32_DiskDrive").Get();

            foreach (ManagementBaseObject? physicalDisk in allPhysicalDisks)
            {
                ManagementObjectCollection allPartitionsOnPhysicalDisk = new ManagementObjectSearcher($"associators of {{Win32_DiskDrive.DeviceID='{physicalDisk["DeviceID"]}'}} where AssocClass = Win32_DiskDriveToDiskPartition").Get();
                foreach (ManagementBaseObject? partition in allPartitionsOnPhysicalDisk)
                {
                    if (partition is null)
                    {
                        continue;
                    }

                    ManagementObjectCollection allLogicalDisksOnPartition = new ManagementObjectSearcher($"associators of {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} where AssocClass = Win32_LogicalDiskToPartition").Get();
                    foreach (ManagementBaseObject? logicalDisk in allLogicalDisksOnPartition)
                    {
                        if (logicalDisk is null)
                        {
                            continue;
                        }

                        DriveInfo? drive = drives.FirstOrDefault(x => x.Name.StartsWith(logicalDisk["Name"] as string, StringComparison.OrdinalIgnoreCase));
                        if (drive is null)
                        {
                            continue;
                        }

                        string mediaType = (physicalDisk["MediaType"] as string) ?? string.Empty;
                        if ((mediaType.Contains("external", StringComparison.OrdinalIgnoreCase) || mediaType.Contains("removable", StringComparison.OrdinalIgnoreCase))
                            && drive.Name == currentDrive.Name)
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}