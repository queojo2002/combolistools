using CombolistTools.Application;
using CombolistTools.Core;
using CombolistTools.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace CombolistTools.UnitTests;

public class ProcessingTests
{
    [Fact]
    public void BloomFilter_AddThenContains_ReturnsTrue()
    {
        var filter = new BloomFilter(1000, 0.01);
        filter.Add("abc");
        Assert.True(filter.MightContain("abc"));
    }

    [Fact]
    public async Task DuplicateRemover_FullRow_RemovesDuplicates()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"comb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var input = Path.Combine(dir, "input.csv");
        var output = Path.Combine(dir, "output.csv");
        await File.WriteAllLinesAsync(input, ["A", "B", "A", "C"]);

        var svc = new DuplicateRemoverService(
            new AutoDetectFileReader(),
            new AutoDetectFileWriter(),
            new ChunkProcessor(),
            (idx, delimiter) => new CsvDuplicateKeyStrategy(idx, delimiter),
            () => new BloomBackedDuplicateDetector(new BloomFilter(1000, 0.01)),
            new ProcessingOptions { EnableParallel = false, ChunkSize = 2 },
            NullLogger<DuplicateRemoverService>.Instance);

        await svc.ExecuteAsync(new DuplicateRemovalOptions { InputPath = input, OutputPath = output }, CancellationToken.None);
        var lines = await File.ReadAllLinesAsync(output);
        Assert.Equal(["A", "B", "C"], lines);
    }

    [Fact]
    public async Task DuplicateRemover_ByColumns_RemovesDuplicatesByConfiguredColumns()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"comb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var input = Path.Combine(dir, "input.csv");
        var output = Path.Combine(dir, "output.csv");
        await File.WriteAllLinesAsync(input, ["1,a,9", "1,b,9", "2,b,8"]);

        var svc = new DuplicateRemoverService(
            new AutoDetectFileReader(),
            new AutoDetectFileWriter(),
            new ChunkProcessor(),
            (idx, delimiter) => new CsvDuplicateKeyStrategy(idx, delimiter),
            () => new BloomBackedDuplicateDetector(new BloomFilter(1000, 0.01)),
            new ProcessingOptions { EnableParallel = false, ChunkSize = 2 },
            NullLogger<DuplicateRemoverService>.Instance);

        await svc.ExecuteAsync(new DuplicateRemovalOptions { InputPath = input, OutputPath = output, ColumnIndexes = [0, 2] }, CancellationToken.None);
        var lines = await File.ReadAllLinesAsync(output);
        Assert.Equal(["1,a,9", "2,b,8"], lines);
    }

    [Fact]
    public async Task MergeService_MergesAndDeDupesGlobally()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"comb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        await File.WriteAllLinesAsync(Path.Combine(dir, "a.csv"), ["A", "B"]);
        await File.WriteAllLinesAsync(Path.Combine(dir, "b.csv"), ["B", "C"]);
        var output = Path.Combine(dir, "merged.csv");

        var svc = new FileMergeService(
            new AutoDetectFileReader(),
            new AutoDetectFileWriter(),
            (idx, delimiter) => new CsvDuplicateKeyStrategy(idx, delimiter),
            () => new BloomBackedDuplicateDetector(new BloomFilter(1000, 0.01)));

        await svc.ExecuteAsync(new MergeOptions { InputFolderPath = dir, OutputPath = output, SearchPattern = "*.csv" }, CancellationToken.None);
        var lines = await File.ReadAllLinesAsync(output);
        Assert.Equal(["A", "B", "C"], lines);
    }

    [Fact]
    public async Task MergeService_WhenOutputInsideInputFolder_ShouldIgnoreOutputFileAsInput()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"comb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        await File.WriteAllLinesAsync(Path.Combine(dir, "a.csv"), ["A", "B"]);
        await File.WriteAllLinesAsync(Path.Combine(dir, "b.csv"), ["B", "C"]);
        var output = Path.Combine(dir, "merged.csv");
        await File.WriteAllLinesAsync(output, ["OLD", "DATA"]);

        var svc = new FileMergeService(
            new AutoDetectFileReader(),
            new AutoDetectFileWriter(),
            (idx, delimiter) => new CsvDuplicateKeyStrategy(idx, delimiter),
            () => new BloomBackedDuplicateDetector(new BloomFilter(1000, 0.01)));

        await svc.ExecuteAsync(new MergeOptions { InputFolderPath = dir, OutputPath = output, SearchPattern = "*.csv" }, CancellationToken.None);
        var lines = await File.ReadAllLinesAsync(output);
        Assert.Equal(["A", "B", "C"], lines);
    }

    [Fact]
    public async Task Splitter_ByLineCount_CreatesExpectedParts()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"comb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var input = Path.Combine(dir, "big.txt");
        await File.WriteAllLinesAsync(input, Enumerable.Range(1, 10).Select(x => x.ToString()));
        var outDir = Path.Combine(dir, "parts");

        var splitter = new StreamingFileSplitter(new AutoDetectFileReader(), new AutoDetectFileWriter());
        await splitter.SplitByLineCountAsync(input, outDir, 3, CancellationToken.None);

        var files = Directory.EnumerateFiles(outDir).OrderBy(x => x).ToArray();
        Assert.Equal(4, files.Length);
    }

    [Fact]
    public async Task GzipReaderWriter_Roundtrip_Works()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"comb-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var gz = Path.Combine(dir, "data.csv.gz");
        var writer = new AutoDetectFileWriter();
        await writer.WriteLinesAsync(gz, ToAsync(["x,y", "1,2"]), CancellationToken.None);
        var reader = new AutoDetectFileReader();
        var lines = new List<string>();
        await foreach (var line in reader.ReadLinesAsync(gz, CancellationToken.None))
        {
            lines.Add(line);
        }
        Assert.Equal(["x,y", "1,2"], lines);
    }

    [Fact]
    public async Task ParallelChunkProcessor_PreserveOrder_ProducesOrderedOutput()
    {
        var processor = new ChunkProcessor();
        var input = ToAsync(Enumerable.Range(1, 20).Select(x => x.ToString()).ToArray());
        var output = new List<string>();
        await foreach (var line in processor.ProcessAsync(input, (chunk, _) => Task.FromResult((IReadOnlyList<string>)chunk.ToList()), new ProcessingOptions
        {
            EnableParallel = true,
            PreserveOutputOrder = true,
            ChunkSize = 5,
            MaxDegreeOfParallelism = 4
        }, CancellationToken.None))
        {
            output.Add(line);
        }

        Assert.Equal(Enumerable.Range(1, 20).Select(x => x.ToString()), output);
    }

    private static async IAsyncEnumerable<string> ToAsync(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            yield return line;
            await Task.Yield();
        }
    }
}
