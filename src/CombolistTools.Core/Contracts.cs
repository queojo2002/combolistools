using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CombolistTools.Core;

public interface IBloomFilter
{
    void Add(string value);
    bool MightContain(string value);
}

public interface IDuplicateDetector
{
    bool TryAdd(string key);
}

public interface IDuplicateKeyStrategy
{
    string BuildKey(string line);
}

public interface IFileReader
{
    IAsyncEnumerable<string> ReadLinesAsync(string path, CancellationToken cancellationToken);
}

public interface IFileWriter
{
    Task WriteLinesAsync(string path, IAsyncEnumerable<string> lines, CancellationToken cancellationToken);
}

public interface IChunkProcessor
{
    IAsyncEnumerable<string> ProcessAsync(
        IAsyncEnumerable<string> lines,
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<string>>> processChunkAsync,
        ProcessingOptions options,
        CancellationToken cancellationToken);
}

public interface IFileSplitter
{
    Task SplitByLineCountAsync(string inputPath, string outputFolder, int linesPerFile, CancellationToken cancellationToken);
    Task SplitBySizeAsync(string inputPath, string outputFolder, long maxBytesPerFile, CancellationToken cancellationToken);
}

public sealed class BloomFilterOptions
{
    public long ExpectedItems { get; set; } = 1_000_000;
    public double FalsePositiveRate { get; set; } = 0.01;
}

public sealed class ProcessingOptions
{
    public bool EnableParallel { get; set; } = false;
    public bool PreserveOutputOrder { get; set; } = true;
    public int ChunkSize { get; set; } = 10_000;
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}

public sealed class DuplicateRemovalOptions
{
    public required string InputPath { get; init; }
    public required string OutputPath { get; init; }
    public int[] ColumnIndexes { get; init; } = [];
    public char Delimiter { get; init; } = ',';
}

public sealed class MergeOptions
{
    public required string InputFolderPath { get; init; }
    public required string OutputPath { get; init; }
    public string SearchPattern { get; init; } = "*.*";
    public int[] ColumnIndexes { get; init; } = [];
    public char Delimiter { get; init; } = ',';
}

public enum SplitMode
{
    ByLineCount,
    BySizeBytes
}

public sealed class SplitOptions
{
    public required string InputPath { get; init; }
    public required string OutputFolder { get; init; }
    public SplitMode Mode { get; init; }
    public long Threshold { get; init; }
}

public sealed class ProgressInfo
{
    public long ProcessedRows { get; init; }
    public double? Percent { get; init; }
}
