using CombolistTools.Application;
using CombolistTools.Core;
using CombolistTools.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace CombolistTools.IntegrationTests;

public class EndToEndTests
{
    [Fact]
    public async Task Input_To_RemoveDuplicate_To_Output_IsCorrect()
    {
        var dir = CreateTempDir();
        var input = Path.Combine(dir, "input.csv");
        var output = Path.Combine(dir, "output.csv");
        await File.WriteAllLinesAsync(input, ["A", "A", "B", "C", "B"]);

        var svc = BuildDuplicateRemover(parallel: true);
        await svc.ExecuteAsync(new DuplicateRemovalOptions { InputPath = input, OutputPath = output }, CancellationToken.None);

        var lines = await File.ReadAllLinesAsync(output);
        Assert.Equal(["A", "B", "C"], lines);
    }

    [Fact]
    public async Task Merge_And_Split_EndToEnd_Works()
    {
        var dir = CreateTempDir();
        var folder = Path.Combine(dir, "folder");
        Directory.CreateDirectory(folder);
        await File.WriteAllLinesAsync(Path.Combine(folder, "1.csv"), ["1", "2", "3"]);
        await File.WriteAllLinesAsync(Path.Combine(folder, "2.csv"), ["3", "4", "5"]);
        var merged = Path.Combine(dir, "merged.csv");

        var mergeService = new FileMergeService(
            new AutoDetectFileReader(),
            new AutoDetectFileWriter(),
            (idx, delimiter) => new CsvDuplicateKeyStrategy(idx, delimiter),
            () => new BloomBackedDuplicateDetector(new BloomFilter(1000, 0.01)));

        await mergeService.ExecuteAsync(new MergeOptions { InputFolderPath = folder, OutputPath = merged, SearchPattern = "*.csv" }, CancellationToken.None);

        var splitService = new FileSplitService(new StreamingFileSplitter(new AutoDetectFileReader(), new AutoDetectFileWriter()));
        var parts = Path.Combine(dir, "parts");
        await splitService.ExecuteAsync(new SplitOptions { InputPath = merged, OutputFolder = parts, Mode = SplitMode.ByLineCount, Threshold = 2 }, CancellationToken.None);
        var files = Directory.EnumerateFiles(parts).OrderBy(x => x).ToArray();
        Assert.Equal(3, files.Length);
    }

    [Fact]
    public async Task Gzip_Input_Output_EndToEnd_Works()
    {
        var dir = CreateTempDir();
        var input = Path.Combine(dir, "input.csv.gz");
        var output = Path.Combine(dir, "output.csv.gz");
        var writer = new AutoDetectFileWriter();
        await writer.WriteLinesAsync(input, AsAsync(["1,a", "1,b", "2,c"]), CancellationToken.None);

        var svc = BuildDuplicateRemover(parallel: false);
        await svc.ExecuteAsync(new DuplicateRemovalOptions { InputPath = input, OutputPath = output, ColumnIndexes = [0] }, CancellationToken.None);

        var reader = new AutoDetectFileReader();
        var lines = new List<string>();
        await foreach (var l in reader.ReadLinesAsync(output, CancellationToken.None))
        {
            lines.Add(l);
        }
        Assert.Equal(["1,a", "2,c"], lines);
    }

    [Fact]
    public async Task CapitalizeLineService_TransformsFirstCharacterPerLine()
    {
        var dir = CreateTempDir();
        var input = Path.Combine(dir, "input.txt");
        var output = Path.Combine(dir, "output.txt");
        await File.WriteAllLinesAsync(input, ["abc", "xyz", ""]);

        var svc = new CapitalizeLineService(new AutoDetectFileReader(), new AutoDetectFileWriter());
        await svc.ExecuteAsync(new CapitalizeLineOptions { InputPath = input, OutputPath = output }, CancellationToken.None);

        var lines = await File.ReadAllLinesAsync(output);
        Assert.Equal(["Abc", "Xyz", ""], lines);
    }

    private static DuplicateRemoverService BuildDuplicateRemover(bool parallel) =>
        new(
            new AutoDetectFileReader(),
            new AutoDetectFileWriter(),
            new ChunkProcessor(),
            (idx, delimiter) => new CsvDuplicateKeyStrategy(idx, delimiter),
            () => new BloomBackedDuplicateDetector(new BloomFilter(10000, 0.01)),
            new ProcessingOptions { EnableParallel = parallel, ChunkSize = 10, MaxDegreeOfParallelism = 4, PreserveOutputOrder = true },
            NullLogger<DuplicateRemoverService>.Instance);

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"comb-it-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        return dir;
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
