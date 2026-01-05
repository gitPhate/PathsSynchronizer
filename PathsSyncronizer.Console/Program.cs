using PathsSynchronizer;
using PathsSynchronizer.Hashing.XXHash;
using System.Diagnostics;
using System.Threading.Channels;

Console.Write("Enter directory path to scan: ");
string? path = Console.ReadLine();

if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
{
    Console.WriteLine("Invalid path.");
    return;
}

Channel<HashProgress> progressChannel = Channel.CreateUnbounded<HashProgress>(new UnboundedChannelOptions { SingleReader = true });
HashService service = new(ServiceOptions.ExternalHDD, new XXHashProvider());

Progress<HashProgress> progress = new(p =>
{
    // Drop old updates if the channel is busy
    progressChannel.Writer.TryWrite(p);
});

int startLine = Console.CursorTop;

Stopwatch stopwatch = Stopwatch.StartNew();

Task renderTask = renderProgressAsync(progressChannel.Reader, startLine, stopwatch);

DirectoryHash result = await service.ScanDirectoryAndHashAsync(path, progress);

try
{
    Console.SetCursorPosition(0, startLine + 5);

    stopwatch.Stop();
    progressChannel.Writer.Complete();
    await renderTask;

    Console.WriteLine("Status           : Completed     ");
}
finally
{
    string fileName = $"directoryhash_{DateTime.Now:yyyyMMddHHmmss}.dat";
    Console.WriteLine($"Saving scan results to file {fileName}");
    await StorageService.StoreDirectoryHashAsync(result, fileName);
}

static string formatBytes(long bytes)
{
    string[] units = ["B", "KB", "MB", "GB", "TB"];
    double size = bytes;
    int unit = 0;

    while (size >= 1024 && unit < units.Length - 1)
    {
        size /= 1024;
        unit++;
    }

    return $"{size:0.##} {units[unit]}";
}

static string formatElapsed(TimeSpan ts)
{
    if (ts.TotalHours >= 1)
        return $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";

    return $"{ts.Minutes:D2}:{ts.Seconds:D2}.{ts.Milliseconds:D3}";
}

static async Task renderProgressAsync(
    ChannelReader<HashProgress> reader,
    int startLine,
    Stopwatch stopwatch,
    CancellationToken cancellationToken = default)
{
    HashProgress last = default;

    while (await reader.WaitToReadAsync(cancellationToken))
    {
        while (reader.TryRead(out var p))
        {
            last = p; // keep latest snapshot
        }

        TimeSpan elapsed = stopwatch.Elapsed;

        double mbPerSecond =
            elapsed.TotalSeconds > 0
                ? last.BytesHashed / 1024d / 1024d / elapsed.TotalSeconds
                : 0;

        Console.SetCursorPosition(0, startLine);

        Console.WriteLine($"Files discovered : {last.FilesRead}      ");
        Console.WriteLine($"Files hashed     : {last.FilesHashed}      ");
        Console.WriteLine($"Bytes hashed     : {formatBytes(last.BytesHashed)}      ");
        Console.WriteLine($"Elapsed time     : {formatElapsed(elapsed)}      ");
        Console.WriteLine($"Speed            : {mbPerSecond:0.00} MB/s      ");
        Console.WriteLine($"Status           : Running...           ");
    }
}