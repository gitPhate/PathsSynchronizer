using System;
using System.IO;

namespace PathsSynchronizer.Core.Support.XXHash
{
    public enum XXHashPlatform { x86, x64 }
    public static class XXHashHelper
    {
        public static ulong Hash(Stream stream, XXHashPlatform platform, uint seed = 0) => 
            platform switch
            {
                XXHashPlatform.x86 => HashDepot.XXHash.Hash32(stream, seed),
                XXHashPlatform.x64 => HashDepot.XXHash.Hash64(stream, seed),
                _ => 0,
            };

        public static Func<Stream, ulong> GetHashFx(XXHashPlatform platform) =>
            platform switch
            {
                XXHashPlatform.x86 => s => HashDepot.XXHash.Hash32(s),
                XXHashPlatform.x64 => s => HashDepot.XXHash.Hash64(s),
                _ => throw new ArgumentException(),
            };
    }
}
