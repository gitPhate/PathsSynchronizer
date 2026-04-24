# AGENTS.md

## Solution structure

```
PathsSynchronizer.sln
├── PathsSynchronizer/               # Core library (namespaces: PathsSynchronizer)
├── PathsSynchronizer.Hashing.XXHash/# XXHash IHashProvider implementation
├── PathsSynchronizer.MurmurHash/    # MurmurHash IHashProvider (incomplete – see below)
├── PathsSyncronizer.Console/        # CLI entrypoint  ← note the typo: Syncronizer
└── PathsSyncronizer.Test/           # xUnit tests     ← same typo
```

**Naming quirk:** folders/projects use `Syncronizer` (one 'h'), but all namespaces are `PathsSynchronizer`. Do not "fix" the folder names — the `.sln` and `.csproj` references depend on them.

All projects target `net8.0` with nullable enabled.

## Developer commands

```bash
# Build everything
dotnet build PathsSynchronizer.sln

# Run all tests
dotnet test PathsSyncronizer.Test/PathsSyncronizer.Test.csproj

# Run a single test by name
dotnet test PathsSyncronizer.Test/PathsSyncronizer.Test.csproj --filter "FullyQualifiedName~<TestMethodName>"

# Run the console app
dotnet run --project PathsSyncronizer.Console/PathsSyncronizer.Console.csproj
```

No `global.json` — SDK version is whatever is installed locally.

## Architecture notes

- **`HashService`** uses a producer/consumer `Channel` with worker tasks and a `SemaphoreSlim` for IO concurrency.
  - Small files are fully hashed; large files use sampled hashing (`SampleCount`, `SampleBlockSize`, `FullHashThreshold`).
  - Built-in `ServiceOptions` presets: `SSD` and `ExternalHDD`.
- **`StorageService`** serializes `DirectoryHash` as JSON, then GZip-compresses it. Output files use `.dat` extension.
- **`GZipHelper`** is internal; use `StorageService` as the public persistence API.
- Console app: prompts for a path, scans it, saves `directoryhash_yyyyMMddHHmmss.dat` in the current working directory.

## Equality semantics (easy to get wrong)

| Type | Equals compares |
|---|---|
| `DirectoryHash` | path **and** file sequence |
| `FileHash` | hashes only, **not** path |
| `DataHash` | byte sequence |
| `Hash` | hex-string view of `DataHash` |

`JsonConstructor` attributes are present so JSON round-trips work with records — don't remove them.

## Known issues / incomplete code

- `MurmurHashProvider.ComputeHash` computes a value but returns `new DataHash([12])` — it is a stub/buggy implementation. Do not rely on it for correctness.

## Test quirks

- Several tests hardcode absolute local paths (`C:\Users\...`, `E:\Foto`, etc.) — they are not portable and will fail on any machine without those exact paths.
- Some tests write artifacts to disk (`scan.dat`, `missing.txt`, `duplicates.json`) in the working directory.
- No CI is configured; tests are run manually.
