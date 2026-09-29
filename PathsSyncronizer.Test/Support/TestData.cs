using PathsSynchronizer;
using PathsSynchronizer.Hashing;

namespace PathsSyncronizer.Test.Support
{
    public static class TestData
    {
        public static byte[] Bytes(int length, int seed = 1)
        {
            byte[] bytes = new byte[length];
            new Random(seed).NextBytes(bytes);
            return bytes;
        }

        public static byte[] WithByteChanged(byte[] source, int index)
        {
            byte[] copy = (byte[])source.Clone();
            copy[index] ^= 0xFF;
            return copy;
        }

        public static DataHash Hash(params byte[] bytes) => new(bytes);

        public static ServiceOptions Options(
            int sampleCount = 4,
            int sampleBlockSize = 16,
            long fullHashThreshold = 100,
            int channelCapacity = 8,
            int workerCount = 2,
            int ioConcurrency = 2,
            int readBufferSize = 32) =>
            new(sampleCount, sampleBlockSize, fullHashThreshold, channelCapacity, workerCount, ioConcurrency, readBufferSize);
    }
}
