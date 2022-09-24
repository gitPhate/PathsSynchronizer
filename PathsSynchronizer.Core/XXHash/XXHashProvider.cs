using K4os.Hash.xxHash;
using PathsSynchronizer.Core.Hashing;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace PathsSynchronizer.Core.XXHash
{
    public class XXHashProvider : IHashProvider<ulong>
    {
        const int _chucksBufferSize = 1024 * 1024 * 50; //50MB

        public async ValueTask<ulong> HashFileAsync(string filePath)
        {
            FileInfo fileInfo = new(filePath);

            ulong hash =
                fileInfo.Length switch
                {
                    < _chucksBufferSize => await HashFileOneShotAsync(filePath).ConfigureAwait(false),
                    _ => await HashFileByChuncksWithProducerConsumerAsync(filePath).ConfigureAwait(false)
                };

            return hash;
        }

        private static async ValueTask<ulong> HashFileByChuncksAsync(string filePath)
        {
            XXH64 fileHash = new();

            using FileStream fs = File.OpenRead(filePath);
            using BufferedStream bs = new(fs, _chucksBufferSize);

            int bytesRead;
            byte[] buffer = new byte[_chucksBufferSize];
            while ((bytesRead = await bs.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                Memory<byte> memory = buffer.AsMemory(0, bytesRead);
                fileHash.Update(memory.Span);
            }

            ulong chucksFileHash = fileHash.Digest();
            return chucksFileHash;
        }

        #region Hash by chunks with producer/consumer
        private static async Task<ulong> HashFileByChuncksWithProducerConsumerAsync(string filePath)
        {
            BufferBlock<Memory<byte>> dataFlowBuffer = new(new DataflowBlockOptions { EnsureOrdered = true });
            Task<ulong> consumerTask = ConsumeAsync(dataFlowBuffer);
            await ProduceFileChunks(filePath, dataFlowBuffer).ConfigureAwait(false);
            ulong chucksFileHash = await consumerTask.ConfigureAwait(false);
            return chucksFileHash;
        }

        private static async ValueTask ProduceFileChunks(string filePath, ITargetBlock<Memory<byte>> dataFlowBuffer)
        {
            const int _chucksBufferSize = 1024 * 256; //0.25MB

            using FileStream fs = File.OpenRead(filePath);
            using BufferedStream bs = new(fs, _chucksBufferSize);

            int bytesRead;
            byte[] buffer = new byte[_chucksBufferSize];
            byte[] buffer2 = new byte[_chucksBufferSize];

            while ((bytesRead = await bs.ReadAsync(buffer).ConfigureAwait(false)) > 0)
            {
                buffer.CopyTo(buffer2, 0);
                Memory<byte> memory = buffer2.AsMemory(0, bytesRead);
                dataFlowBuffer.Post(memory);
            }

            dataFlowBuffer.Complete();
        }

        private static async Task<ulong> ConsumeAsync(ISourceBlock<Memory<byte>> dataFlowBuffer)
        {
            XXH64 fileHash = new();
            while (await dataFlowBuffer.OutputAvailableAsync().ConfigureAwait(false))
            {
                Memory<byte> memory = await dataFlowBuffer.ReceiveAsync().ConfigureAwait(false);
                fileHash.Update(memory.Span);
            }

            ulong chucksFileHash = fileHash.Digest();
            return chucksFileHash;
        }

        #endregion

        private static async Task<ulong> HashFileOneShotAsync(string filePath)
        {
            byte[] allFile =
                await File
                    .ReadAllBytesAsync(filePath)
                    .ConfigureAwait(false);

            ulong oneShotFileHash = XXH64.DigestOf(allFile, 0, allFile.Length);
            return oneShotFileHash;
        }

        public ValueTask<ulong> HashBytesAsync(byte[] bytes)
        {
            ulong oneShotFileHash = XXH64.DigestOf(bytes, 0, bytes.Length);
            return ValueTask.FromResult(oneShotFileHash);
        }
    }
}
