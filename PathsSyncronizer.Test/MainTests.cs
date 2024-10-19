using K4os.Hash.xxHash;
using PathsSynchronizer.Core.Checksum;
using PathsSynchronizer.Core.XXHash;
using System;
using System.Diagnostics;
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

            var table =
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
            const string filePath = @"E:\Film\300.avi";

            //ulong chucksFileHash = await NewHashFileByChuncksAsync(filePath).ConfigureAwait(false);
            ulong originalHash = await OriginalHashFileByChuncksAsync(filePath).ConfigureAwait(false);

            
            //Assert.Equal(chucksFileHash, originalHash);
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

        private static async ValueTask<ulong> OriginalHashFileByChuncksAsync(string filePath)
        {
            const int _chucksBufferSize = 1024; //50MB

            XXH64 fileHash = new();

            using FileStream fs = File.OpenRead(filePath);
            using BufferedStream bs = new(fs, _chucksBufferSize);

            int bytesRead;
            byte[] buffer = new byte[_chucksBufferSize];
            List<ulong> hashList = [];
            while ((bytesRead = await bs.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                ulong hash = XXH64.DigestOf(buffer, 0, buffer.Length);
                hashList.Add(hash);
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
            var buffer = new BufferBlock<byte[]>();
            var consumerTask = TestConsumeAsync(buffer);
            TestProduce(buffer);

            var bytesProcessed = await consumerTask;

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
    }
}