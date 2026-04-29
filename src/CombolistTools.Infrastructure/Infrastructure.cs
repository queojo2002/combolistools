using System.Collections;
using System.IO.Compression;
using System.Text;
using CombolistTools.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CombolistTools.Infrastructure;

public sealed class BloomFilter : IBloomFilter
{
    private readonly BitArray _bits;
    private readonly int _bitSize;
    private readonly int _hashCount;

    public BloomFilter(long expectedItems, double falsePositiveRate)
    {
        _bitSize = (int)Math.Ceiling(-(expectedItems * Math.Log(falsePositiveRate)) / Math.Pow(Math.Log(2), 2));
        _hashCount = Math.Max(2, (int)Math.Ceiling((_bitSize / (double)expectedItems) * Math.Log(2)));
        _bits = new BitArray(_bitSize);
    }

    public void Add(string value)
    {
        foreach (var index in HashIndexes(value))
        {
            _bits[index] = true;
        }
    }

    public bool MightContain(string value) => HashIndexes(value).All(i => _bits[i]);

    private IEnumerable<int> HashIndexes(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var h1 = BitConverter.ToUInt64(System.Security.Cryptography.SHA256.HashData(bytes), 0);
        var h2 = BitConverter.ToUInt64(System.Security.Cryptography.MD5.HashData(bytes), 0);
        for (var i = 0; i < _hashCount; i++)
        {
            yield return (int)((h1 + (ulong)i * h2) % (ulong)_bitSize);
        }
    }
}

public sealed class BloomBackedDuplicateDetector : IDuplicateDetector
{
    private readonly IBloomFilter _bloom;
    private readonly HashSet<string> _set = new(StringComparer.Ordinal);
    private readonly object _gate = new();

    public BloomBackedDuplicateDetector(IBloomFilter bloom) => _bloom = bloom;

    public bool TryAdd(string key)
    {
        lock (_gate)
        {
            if (_bloom.MightContain(key) && _set.Contains(key))
            {
                return false;
            }

            _bloom.Add(key);
            return _set.Add(key);
        }
    }
}

public sealed class CsvDuplicateKeyStrategy : IDuplicateKeyStrategy
{
    private readonly int[] _indexes;
    private readonly char _delimiter;

    public CsvDuplicateKeyStrategy(int[] indexes, char delimiter)
    {
        _indexes = indexes;
        _delimiter = delimiter;
    }

    public string BuildKey(string line)
    {
        if (_indexes.Length == 0)
        {
            return line;
        }

        var parts = line.Split(_delimiter);
        return string.Join('|', _indexes.Select(i => i >= 0 && i < parts.Length ? parts[i] : string.Empty));
    }
}

public sealed class AutoDetectFileReader : IFileReader
{
    public async IAsyncEnumerable<string> ReadLinesAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        Stream effective = stream;
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            effective = new GZipStream(stream, CompressionMode.Decompress);
        }

        using var reader = new StreamReader(effective, Encoding.UTF8, true, 1024 * 64, leaveOpen: false);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            yield return line;
        }
    }
}

public sealed class AutoDetectFileWriter : IFileWriter
{
    public async Task WriteLinesAsync(string path, IAsyncEnumerable<string> lines, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var fs = File.Create(path);
        Stream effective = fs;
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
        {
            effective = new GZipStream(fs, CompressionLevel.Fastest);
        }

        await using var writer = new StreamWriter(effective, Encoding.UTF8, 1024 * 64, leaveOpen: false);
        await foreach (var line in lines.WithCancellation(cancellationToken))
        {
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
    }
}

public sealed class ChunkProcessor : IChunkProcessor
{
    public async IAsyncEnumerable<string> ProcessAsync(
        IAsyncEnumerable<string> lines,
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<string>>> processChunkAsync,
        ProcessingOptions options,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!options.EnableParallel)
        {
            var chunk = new List<string>(options.ChunkSize);
            await foreach (var line in lines.WithCancellation(cancellationToken))
            {
                chunk.Add(line);
                if (chunk.Count >= options.ChunkSize)
                {
                    var result = await processChunkAsync(chunk, cancellationToken);
                    foreach (var r in result) yield return r;
                    chunk = new List<string>(options.ChunkSize);
                }
            }

            if (chunk.Count > 0)
            {
                var result = await processChunkAsync(chunk, cancellationToken);
                foreach (var r in result) yield return r;
            }

            yield break;
        }

        var semaphore = new SemaphoreSlim(Math.Max(1, options.MaxDegreeOfParallelism));
        var tasks = new List<Task<(int Index, IReadOnlyList<string> Data)>>();
        var index = 0;
        var pending = new List<string>(options.ChunkSize);

        async Task<(int Index, IReadOnlyList<string> Data)> RunChunk(int idx, List<string> data)
        {
            await semaphore.WaitAsync(cancellationToken);
            try { return (idx, await processChunkAsync(data, cancellationToken)); }
            finally { semaphore.Release(); }
        }

        await foreach (var line in lines.WithCancellation(cancellationToken))
        {
            pending.Add(line);
            if (pending.Count >= options.ChunkSize)
            {
                tasks.Add(RunChunk(index++, pending));
                pending = new List<string>(options.ChunkSize);
            }
        }

        if (pending.Count > 0) tasks.Add(RunChunk(index, pending));

        var results = await Task.WhenAll(tasks);
        IEnumerable<(int Index, IReadOnlyList<string> Data)> ordered = results;
        if (options.PreserveOutputOrder)
        {
            ordered = ordered.OrderBy(x => x.Index);
        }

        foreach (var item in ordered)
        {
            foreach (var r in item.Data) yield return r;
        }
    }
}

public sealed class StreamingFileSplitter : IFileSplitter
{
    private readonly IFileReader _reader;
    private readonly IFileWriter _writer;
    public StreamingFileSplitter(IFileReader reader, IFileWriter writer) { _reader = reader; _writer = writer; }

    public Task SplitByLineCountAsync(string inputPath, string outputFolder, int linesPerFile, CancellationToken cancellationToken)
        => SplitInternal(inputPath, outputFolder, line => line.Length * 2 + Environment.NewLine.Length, linesPerFile, null, cancellationToken);

    public Task SplitBySizeAsync(string inputPath, string outputFolder, long maxBytesPerFile, CancellationToken cancellationToken)
        => SplitInternal(inputPath, outputFolder, line => Encoding.UTF8.GetByteCount(line) + Environment.NewLine.Length, null, maxBytesPerFile, cancellationToken);

    private async Task SplitInternal(string inputPath, string outputFolder, Func<string, long> lineSize, int? maxLines, long? maxBytes, CancellationToken ct)
    {
        Directory.CreateDirectory(outputFolder);
        var part = 1;
        var currentLines = 0;
        long currentBytes = 0;
        var bucket = new List<string>();

        async Task FlushIfNeeded(bool force = false)
        {
            if (bucket.Count == 0) return;
            var outPath = Path.Combine(outputFolder, $"part-{part:0000}.txt");
            await _writer.WriteLinesAsync(outPath, AsAsync(bucket), ct);
            bucket.Clear();
            part++;
            currentLines = 0;
            currentBytes = 0;
        }

        await foreach (var line in _reader.ReadLinesAsync(inputPath, ct))
        {
            ct.ThrowIfCancellationRequested();
            var nextBytes = lineSize(line);
            if ((maxLines.HasValue && currentLines >= maxLines.Value) || (maxBytes.HasValue && currentBytes + nextBytes > maxBytes.Value && currentLines > 0))
            {
                await FlushIfNeeded();
            }
            bucket.Add(line);
            currentLines++;
            currentBytes += nextBytes;
        }

        await FlushIfNeeded(force: true);
    }

    private static async IAsyncEnumerable<string> AsAsync(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            yield return line;
            await Task.Yield();
        }
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var processing = configuration.GetSection("Processing").Get<ProcessingOptions>() ?? new ProcessingOptions();
        var bloomOptions = configuration.GetSection("BloomFilter").Get<BloomFilterOptions>() ?? new BloomFilterOptions();

        services.AddSingleton(processing);
        services.AddSingleton(bloomOptions);
        services.AddSingleton<IBloomFilter>(_ => new BloomFilter(bloomOptions.ExpectedItems, bloomOptions.FalsePositiveRate));
        services.AddTransient<IDuplicateDetector, BloomBackedDuplicateDetector>();
        services.AddTransient<Func<IDuplicateDetector>>(sp => () => sp.GetRequiredService<IDuplicateDetector>());
        services.AddTransient<Func<int[], char, IDuplicateKeyStrategy>>(_ => (cols, delimiter) => new CsvDuplicateKeyStrategy(cols, delimiter));
        services.AddSingleton<IFileReader, AutoDetectFileReader>();
        services.AddSingleton<IFileWriter, AutoDetectFileWriter>();
        services.AddSingleton<IChunkProcessor, ChunkProcessor>();
        services.AddSingleton<IFileSplitter, StreamingFileSplitter>();
        return services;
    }
}
