# PathsSynchronizer

Fast, parallel content hashing of whole directory trees, built to find duplicates, missing files and differences between two folders (or two drives) without comparing every byte.

A scan walks a directory recursively, hashes every file, and produces a `DirectoryHash` that can be saved to a compact `.dat` file (JSON + GZip) and compared later.

## Features

- **Parallel pipeline**: a producer enumerates files into a bounded channel, a pool of workers hashes them.
- **Sampled hashing for large files**: files above a threshold are hashed from evenly spaced blocks instead of being read in full.
- **Pluggable hash algorithms**: XXHash128 and MurmurHash3 x64_128 included; implement `IHashProvider` to add your own.
- **Tunable presets** for SSD and external HDD, or fully custom options.
- **Progress reporting** and cancellation support.
- **Persistence**: `StorageService` saves/loads scans as GZip-compressed JSON.

## Library usage

```csharp
using PathsSynchronizer;
using PathsSynchronizer.Hashing.XXHash;

ServiceOptions options = ServiceOptions.SSD;
HashService.EnsureThreadPoolCapacity(options);   // call once at startup

HashService service = new(options, new XXHashProvider());

DirectoryHash result = await service.ScanDirectoryAndHashAsync(
    @"D:\Photos",
    new Progress<HashProgress>(p => Console.WriteLine($"{p.FilesHashed}/{p.FilesRead} files, {p.BytesHashed} bytes")),
    cancellationToken);

await StorageService.StoreDirectoryHashAsync(result, "photos.dat");
DirectoryHash loaded = await StorageService.ReadStorageFileAsync("photos.dat");
```

Hash a single file with `service.HashFileAsync(path)`.

## Service options

`HashService` takes a `ServiceOptions` record and an `IHashProvider`:

```csharp
new HashService(ServiceOptions options, IHashProvider hashProvider)
```

| Option | Type | What it controls |
|---|---|---|
| `SampleCount` | `int` | Number of blocks sampled from each *large* file. Sample offsets are spread evenly from the start to the end of the file. More samples = fewer missed differences, more I/O. |
| `SampleBlockSize` | `int` (bytes) | Size of each sampled block. The file hash is the list of one hash per block. |
| `FullHashThreshold` | `long` (bytes) | Files **up to** this size are hashed completely; larger files use sampled hashing. |
| `ProducerChannelCapacity` | `int` | Capacity of the bounded queue between the directory enumerator and the workers. The enumerator waits when it is full, which bounds memory use. |
| `WorkerCount` | `int` | Number of concurrent hashing workers. Independent of processor count. |
| `IOConcurrency` | `int` | Maximum number of disk reads in flight at once (a `SemaphoreSlim` shared by all workers). Keep it low on spinning disks. |
| `ReadBufferSize` | `int` (bytes) | Buffer size used to read small/medium files sequentially. |

### Presets

| Option | `ServiceOptions.SSD` | `ServiceOptions.ExternalHDD` |
|---|---|---|
| `SampleCount` | 16 | 16 |
| `SampleBlockSize` | 1 MB | 1 MB |
| `FullHashThreshold` | 100 MB | 100 MB |
| `ProducerChannelCapacity` | 16384 | 4096 |
| `WorkerCount` | 48 | `Environment.ProcessorCount` |
| `IOConcurrency` | 32 | 16 |
| `ReadBufferSize` | 256 KB | 1 MB |

### Custom options

```csharp
ServiceOptions custom = new(
    SampleCount: 8,
    SampleBlockSize: 512 * 1024,
    FullHashThreshold: 50L * 1024 * 1024,
    ProducerChannelCapacity: 8192,
    WorkerCount: 16,
    IOConcurrency: 8,
    ReadBufferSize: 512 * 1024);

// or tweak a preset
ServiceOptions tweaked = ServiceOptions.SSD with { IOConcurrency = 8 };
```

### Thread pool

Workers block on I/O, so `HashService.EnsureThreadPoolCapacity(options)` raises the thread pool minimum to `WorkerCount + 4` (it never lowers it). Call it once at startup; the library never calls it on its own.

### Sampled hashing trade-off

For files larger than `FullHashThreshold`, only `SampleCount × SampleBlockSize` bytes are read (16 MB with the defaults). Two large files that differ only outside the sampled blocks will get the same hash. Raise `FullHashThreshold` for exactness, lower it or the sample settings for speed.

## Hash providers

| Provider | Project | Output |
|---|---|---|
| `XXHashProvider` | `PathsSynchronizer.Hashing.XXHash` | XXHash128 (16 bytes) |
| `MurmurHashProvider` | `PathsSynchronizer.Hashing.MurmurHash` | MurmurHash3 x64_128 (16 bytes) |

Both are non-cryptographic. To add another algorithm, implement:

```csharp
public interface IHashProvider
{
    ValueTask<FileHash> HashFileAsync(string path, long length, MemoryPool<byte> pool, int bufferSize, CancellationToken cancellationToken = default);
    ValueTask<DataHash> HashMemoryAsync(Memory<byte> buffer, CancellationToken cancellationToken = default);
}
```

`HashFileAsync` is used for whole-file hashing (small files), `HashMemoryAsync` for each sampled block of large files.

## Equality semantics

| Type | `Equals` compares |
|---|---|
| `DirectoryHash` | path **and** files (ordinal, order-independent) |
| `FileHash` | hashes only, **not** path |
| `DataHash` | byte sequence |

Because `FileHash` ignores the path, identical content in different locations compares equal, which is what makes duplicate detection possible.

## Storage format

`StorageService.StoreDirectoryHashAsync` writes `DirectoryHash` as JSON compressed with GZip (conventionally `.dat`). `ReadStorageFileAsync` reads it back.