# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Parallel content hashing of directory trees to find duplicates, missing files and differences between folders/drives. A scan produces a `DirectoryHash`, persisted as GZip-compressed JSON (`.dat`). See `README.md` for usage, `ServiceOptions` reference and presets; `EXTERNAL_HDD_TUNING.md` for open (unmeasured) `ExternalHDD` preset tuning.

## Commands

```bash
dotnet build PathsSynchronizer.sln
dotnet test PathsSyncronizer.Test/PathsSyncronizer.Test.csproj
dotnet test PathsSyncronizer.Test/PathsSyncronizer.Test.csproj --filter "FullyQualifiedName~<TestMethodName>"
dotnet run --project PathsSyncronizer.Console/PathsSyncronizer.Console.csproj
```

No `global.json`, no CI, no lint config. All projects target `net8.0`, nullable enabled.

## Layout

- `PathsSynchronizer/` – core library: `HashService` (scan pipeline), `StorageService` (persistence), `DTOs.cs` (`ServiceOptions`, progress), `Hashing/` (`IHashProvider`, `FileHash`/`DataHash`/`DirectoryHash`).
- `PathsSynchronizer.Hashing.XXHash/`, `PathsSynchronizer.Hashing.MurmurHash/` – `IHashProvider` implementations (Murmur is an in-house streaming MurmurHash3 x64_128).
- `PathsSyncronizer.Console/` – CLI; prompts for a path, scans with a hardcoded `ServiceOptions.SSD`, writes `directoryhash_yyyyMMddHHmmss.dat` to the cwd.
- `PathsSyncronizer.Test/` – xUnit + FluentAssertions; helpers in `Support/`.
- `PathsSynchronizer.MurmurHash/` – stale untracked leftover (only `bin/obj`); ignore.

**Naming quirk:** the Console and Test folders/projects are spelled `Syncronizer` (one 'h'), while all namespaces are `PathsSynchronizer`. Do not "fix" it; the `.sln`/`.csproj` references depend on it.

## Architecture

- `HashService` is a producer/consumer pipeline: a single `FileSystemEnumerable` producer (unsorted, filesystem order) feeds a bounded `Channel` (`ProducerChannelCapacity`); `WorkerCount` workers hash, gated by a `SemaphoreSlim` (`IOConcurrency`). Reads are synchronous, so `WorkerCount` is the real read concurrency. The old name-ordered producer is kept commented out below `ProducerAsync` for HDD comparison.
- Files `<= FullHashThreshold` go through `IHashProvider.HashFileAsync` (file length is passed so the trailing read can be skipped); larger files are sampled: `SampleCount` blocks at evenly spaced ascending offsets, read into one reused buffer and hashed via `HashMemoryAsync`.
- `HashService.EnsureThreadPoolCapacity(options)` raises the thread pool minimum to `WorkerCount + 4`; the library never calls it itself, callers must.
- `GZipHelper` is internal; `StorageService` is the public persistence API.

## Equality semantics (easy to get wrong)

| Type | `Equals` compares |
|---|---|
| `DirectoryHash` | path **and** files (ordinal, order-independent hash code) |
| `FileHash` | hashes only, **not** path (enables duplicate detection) |
| `DataHash` | byte sequence |

`[JsonConstructor]` attributes make JSON round-trips work with records; don't remove them.

## Test quirks

- Some tests hardcode absolute local paths (`C:\Users\...`, `E:\Foto`) and fail elsewhere.
- Some tests write artifacts (`scan.dat`, `missing.txt`, `duplicates.json`) to the working directory.
