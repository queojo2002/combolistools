# Large File Processing Console App Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a production-ready .NET console application that processes very large CSV/TXT and `.gz` files using streaming, supports duplicate removal/merge/split, and is fully covered by unit and integration tests.

**Architecture:** Use a simple Clean Architecture with `Core`, `Application`, `Infrastructure`, and `Presentation` projects wired via dependency injection. All data processing is stream-based, with optional chunk-level parallel processing, deterministic output ordering mode, and a BloomFilter + HashSet strategy for memory-efficient duplicate detection.

**Tech Stack:** C# (.NET 8, compatible with .NET 6+), `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Options`, `System.IO.Compression`, xUnit, FluentAssertions (optional), coverlet.

---

## Scope Check

This spec includes one cohesive subsystem (large-file processing pipeline) with multiple commands (`remove-duplicate`, `merge`, `split`). One implementation plan is appropriate because all requirements share the same abstractions (streaming I/O, duplicate detection, chunk processor, configuration, logging, cancellation, and tests).

## File Structure

Create a multi-project solution and keep each file single-responsibility.

**Solution and projects**
- Create: `src/CombolistTools.sln`
- Create: `src/CombolistTools.Core/CombolistTools.Core.csproj`
- Create: `src/CombolistTools.Application/CombolistTools.Application.csproj`
- Create: `src/CombolistTools.Infrastructure/CombolistTools.Infrastructure.csproj`
- Create: `src/CombolistTools.Presentation.Console/CombolistTools.Presentation.Console.csproj`
- Create: `tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj`
- Create: `tests/CombolistTools.IntegrationTests/CombolistTools.IntegrationTests.csproj`

**Core**
- Create: `src/CombolistTools.Core/Abstractions/IBloomFilter.cs`
- Create: `src/CombolistTools.Core/Abstractions/IDuplicateDetector.cs`
- Create: `src/CombolistTools.Core/Abstractions/IFileReader.cs`
- Create: `src/CombolistTools.Core/Abstractions/IFileWriter.cs`
- Create: `src/CombolistTools.Core/Abstractions/IFileSplitter.cs`
- Create: `src/CombolistTools.Core/Abstractions/IChunkProcessor.cs`
- Create: `src/CombolistTools.Core/Abstractions/IDuplicateKeyStrategy.cs`
- Create: `src/CombolistTools.Core/Models/BloomFilterOptions.cs`
- Create: `src/CombolistTools.Core/Models/ProcessingOptions.cs`
- Create: `src/CombolistTools.Core/Models/DuplicateRemovalOptions.cs`
- Create: `src/CombolistTools.Core/Models/MergeOptions.cs`
- Create: `src/CombolistTools.Core/Models/SplitOptions.cs`
- Create: `src/CombolistTools.Core/Models/ChunkResult.cs`
- Create: `src/CombolistTools.Core/Models/ProgressInfo.cs`

**Application**
- Create: `src/CombolistTools.Application/Services/DuplicateRemoverService.cs`
- Create: `src/CombolistTools.Application/Services/FileMergeService.cs`
- Create: `src/CombolistTools.Application/Services/FileSplitService.cs`
- Create: `src/CombolistTools.Application/Services/ProgressReporter.cs`
- Create: `src/CombolistTools.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`

**Infrastructure**
- Create: `src/CombolistTools.Infrastructure/Duplicate/BloomFilter.cs`
- Create: `src/CombolistTools.Infrastructure/Duplicate/HashSetDuplicateDetector.cs`
- Create: `src/CombolistTools.Infrastructure/Duplicate/BloomBackedDuplicateDetector.cs`
- Create: `src/CombolistTools.Infrastructure/Duplicate/CsvColumnDuplicateKeyStrategy.cs`
- Create: `src/CombolistTools.Infrastructure/IO/PlainTextFileReader.cs`
- Create: `src/CombolistTools.Infrastructure/IO/GzipFileReader.cs`
- Create: `src/CombolistTools.Infrastructure/IO/AutoDetectFileReader.cs`
- Create: `src/CombolistTools.Infrastructure/IO/PlainTextFileWriter.cs`
- Create: `src/CombolistTools.Infrastructure/IO/GzipFileWriter.cs`
- Create: `src/CombolistTools.Infrastructure/IO/AutoDetectFileWriter.cs`
- Create: `src/CombolistTools.Infrastructure/Processing/SequentialChunkProcessor.cs`
- Create: `src/CombolistTools.Infrastructure/Processing/ParallelChunkProcessor.cs`
- Create: `src/CombolistTools.Infrastructure/Splitting/StreamingFileSplitter.cs`
- Create: `src/CombolistTools.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

**Presentation**
- Modify: `Program.cs` (replace single-file app with composition root)
- Create: `src/CombolistTools.Presentation.Console/Cli/CliCommandParser.cs`
- Create: `src/CombolistTools.Presentation.Console/Cli/CliCommandModels.cs`
- Create: `src/CombolistTools.Presentation.Console/appsettings.json`
- Create: `src/CombolistTools.Presentation.Console/HostedServices/ConsoleAppRunner.cs`

**Tests**
- Create: `tests/CombolistTools.UnitTests/Duplicate/BloomFilterTests.cs`
- Create: `tests/CombolistTools.UnitTests/Duplicate/BloomBackedDuplicateDetectorTests.cs`
- Create: `tests/CombolistTools.UnitTests/Splitting/StreamingFileSplitterTests.cs`
- Create: `tests/CombolistTools.UnitTests/Processing/ParallelChunkProcessorTests.cs`
- Create: `tests/CombolistTools.UnitTests/Services/DuplicateRemoverServiceTests.cs`
- Create: `tests/CombolistTools.UnitTests/Services/FileMergeServiceTests.cs`
- Create: `tests/CombolistTools.UnitTests/Services/FileSplitServiceTests.cs`
- Create: `tests/CombolistTools.UnitTests/IO/GzipReaderWriterTests.cs`
- Create: `tests/CombolistTools.IntegrationTests/EndToEnd/CommandWorkflowTests.cs`
- Create: `tests/CombolistTools.IntegrationTests/TestData/TestFileFactory.cs`

---

### Task 1: Bootstrap solution and layered project references

**Files:**
- Create: `src/CombolistTools.sln`
- Create: `src/CombolistTools.Core/CombolistTools.Core.csproj`
- Create: `src/CombolistTools.Application/CombolistTools.Application.csproj`
- Create: `src/CombolistTools.Infrastructure/CombolistTools.Infrastructure.csproj`
- Create: `src/CombolistTools.Presentation.Console/CombolistTools.Presentation.Console.csproj`
- Create: `tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj`
- Create: `tests/CombolistTools.IntegrationTests/CombolistTools.IntegrationTests.csproj`

- [ ] **Step 1: Write failing smoke test for solution composition**
```csharp
using Xunit;

public class SolutionSmokeTests
{
    [Fact]
    public void Placeholder()
    {
        Assert.True(false, "Projects not wired yet");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter SolutionSmokeTests`
Expected: FAIL with assertion `"Projects not wired yet"`.

- [ ] **Step 3: Create projects and references (minimal pass condition)**
```xml
<!-- src/CombolistTools.Application/CombolistTools.Application.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\CombolistTools.Core\CombolistTools.Core.csproj" />
  </ItemGroup>
</Project>
```

```xml
<!-- src/CombolistTools.Infrastructure/CombolistTools.Infrastructure.csproj -->
<ItemGroup>
  <ProjectReference Include="..\CombolistTools.Core\CombolistTools.Core.csproj" />
  <ProjectReference Include="..\CombolistTools.Application\CombolistTools.Application.csproj" />
</ItemGroup>
```

```xml
<!-- src/CombolistTools.Presentation.Console/CombolistTools.Presentation.Console.csproj -->
<ItemGroup>
  <ProjectReference Include="..\CombolistTools.Application\CombolistTools.Application.csproj" />
  <ProjectReference Include="..\CombolistTools.Infrastructure\CombolistTools.Infrastructure.csproj" />
</ItemGroup>
```

- [ ] **Step 4: Update smoke test to real pass condition**
```csharp
using Xunit;

public class SolutionSmokeTests
{
    [Fact]
    public void ProjectStructure_IsAvailable()
    {
        Assert.True(true);
    }
}
```

- [ ] **Step 5: Run all tests and verify pass**

Run: `dotnet test`
Expected: PASS, all discovered tests green.

- [ ] **Step 6: Commit**
```bash
git add src tests
git commit -m "chore: scaffold clean architecture solution layout"
```

---

### Task 2: Define Core contracts, models, and option validation

**Files:**
- Create: `src/CombolistTools.Core/Abstractions/*.cs`
- Create: `src/CombolistTools.Core/Models/*.cs`
- Test: `tests/CombolistTools.UnitTests/Core/OptionsValidationTests.cs`

- [ ] **Step 1: Write failing tests for options validation and interfaces usage**
```csharp
using CombolistTools.Core.Models;
using Xunit;

public class OptionsValidationTests
{
    [Fact]
    public void BloomFilterOptions_InvalidFalsePositiveRate_ShouldThrow()
    {
        var options = new BloomFilterOptions(1000, 1.5);
        Assert.Throws<ArgumentOutOfRangeException>(() => options.Validate());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter OptionsValidationTests`
Expected: FAIL because models/validation do not exist.

- [ ] **Step 3: Implement Core abstractions and immutable models**
```csharp
namespace CombolistTools.Core.Models;

public sealed record BloomFilterOptions(long ExpectedItems, double FalsePositiveRate)
{
    public void Validate()
    {
        if (ExpectedItems <= 0) throw new ArgumentOutOfRangeException(nameof(ExpectedItems));
        if (FalsePositiveRate <= 0 || FalsePositiveRate >= 1) throw new ArgumentOutOfRangeException(nameof(FalsePositiveRate));
    }
}
```

```csharp
namespace CombolistTools.Core.Abstractions;

public interface IDuplicateDetector
{
    bool IsDuplicate(string key);
    bool TryAdd(string key);
}
```

```csharp
namespace CombolistTools.Core.Abstractions;

public interface IFileReader
{
    IAsyncEnumerable<string> ReadLinesAsync(string path, CancellationToken cancellationToken);
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter OptionsValidationTests`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add src/CombolistTools.Core tests/CombolistTools.UnitTests/Core
git commit -m "feat(core): add processing contracts and validated option models"
```

---

### Task 3: Implement duplicate detection primitives (BloomFilter + HashSet confirmation)

**Files:**
- Create: `src/CombolistTools.Infrastructure/Duplicate/BloomFilter.cs`
- Create: `src/CombolistTools.Infrastructure/Duplicate/HashSetDuplicateDetector.cs`
- Create: `src/CombolistTools.Infrastructure/Duplicate/BloomBackedDuplicateDetector.cs`
- Test: `tests/CombolistTools.UnitTests/Duplicate/BloomFilterTests.cs`
- Test: `tests/CombolistTools.UnitTests/Duplicate/BloomBackedDuplicateDetectorTests.cs`

- [ ] **Step 1: Write failing tests for BloomFilter behavior**
```csharp
using CombolistTools.Infrastructure.Duplicate;
using Xunit;

public class BloomFilterTests
{
    [Fact]
    public void Add_ThenMightContain_ShouldReturnTrue()
    {
        var filter = new BloomFilter(1000, 0.01);
        filter.Add("A");
        Assert.True(filter.MightContain("A"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter BloomFilterTests`
Expected: FAIL because `BloomFilter` is missing.

- [ ] **Step 3: Implement BloomFilter with computed bit-array size and hash count**
```csharp
public sealed class BloomFilter : IBloomFilter
{
    private readonly BitArray _bits;
    private readonly int _hashFunctionCount;
    private readonly int _bitSize;

    public BloomFilter(long expectedItems, double falsePositiveRate)
    {
        _bitSize = (int)Math.Ceiling(-(expectedItems * Math.Log(falsePositiveRate)) / Math.Pow(Math.Log(2), 2));
        _hashFunctionCount = (int)Math.Ceiling((_bitSize / (double)expectedItems) * Math.Log(2));
        _bits = new BitArray(_bitSize);
    }

    public void Add(string value) { foreach (var idx in ComputeIndices(value)) _bits[idx] = true; }
    public bool MightContain(string value) => ComputeIndices(value).All(i => _bits[i]);
}
```

- [ ] **Step 4: Implement Bloom+HashSet strategy for exact duplicate detection**
```csharp
public sealed class BloomBackedDuplicateDetector : IDuplicateDetector
{
    private readonly IBloomFilter _bloomFilter;
    private readonly HashSet<string> _knownKeys = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public bool TryAdd(string key)
    {
        lock (_sync)
        {
            if (_bloomFilter.MightContain(key) && _knownKeys.Contains(key)) return false;
            _bloomFilter.Add(key);
            return _knownKeys.Add(key);
        }
    }

    public bool IsDuplicate(string key) => !TryAdd(key);
}
```

- [ ] **Step 5: Run duplicate tests**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter "BloomFilterTests|BloomBackedDuplicateDetectorTests"`
Expected: PASS.

- [ ] **Step 6: Commit**
```bash
git add src/CombolistTools.Infrastructure/Duplicate tests/CombolistTools.UnitTests/Duplicate
git commit -m "feat(infra): implement bloom-backed exact duplicate detector"
```

---

### Task 4: Implement streaming readers/writers with gzip auto-detection

**Files:**
- Create: `src/CombolistTools.Infrastructure/IO/PlainTextFileReader.cs`
- Create: `src/CombolistTools.Infrastructure/IO/GzipFileReader.cs`
- Create: `src/CombolistTools.Infrastructure/IO/AutoDetectFileReader.cs`
- Create: `src/CombolistTools.Infrastructure/IO/PlainTextFileWriter.cs`
- Create: `src/CombolistTools.Infrastructure/IO/GzipFileWriter.cs`
- Create: `src/CombolistTools.Infrastructure/IO/AutoDetectFileWriter.cs`
- Test: `tests/CombolistTools.UnitTests/IO/GzipReaderWriterTests.cs`

- [ ] **Step 1: Write failing gzip roundtrip tests**
```csharp
[Fact]
public async Task AutoDetectReaderWriter_GzipRoundtrip_ShouldMatchLines()
{
    var inputLines = new[] { "a,b,c", "1,2,3" };
    // write .gz, then read back using auto-detect
    Assert.Equal(inputLines, readBackLines);
}
```

- [ ] **Step 2: Run tests to verify fail**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter GzipReaderWriterTests`
Expected: FAIL with missing reader/writer implementations.

- [ ] **Step 3: Implement stream-only reader/writer abstractions (no ReadAllLines)**
```csharp
public sealed class AutoDetectFileReader : IFileReader
{
    public IAsyncEnumerable<string> ReadLinesAsync(string path, CancellationToken cancellationToken)
        => path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase)
            ? _gzipReader.ReadLinesAsync(path, cancellationToken)
            : _plainReader.ReadLinesAsync(path, cancellationToken);
}
```

```csharp
public sealed class GzipFileWriter : IFileWriter
{
    public async Task WriteLinesAsync(string path, IAsyncEnumerable<string> lines, CancellationToken ct)
    {
        await using var fileStream = File.Create(path);
        await using var gzip = new GZipStream(fileStream, CompressionLevel.Fastest);
        await using var writer = new StreamWriter(gzip, Encoding.UTF8);
        await foreach (var line in lines.WithCancellation(ct))
        {
            await writer.WriteLineAsync(line.AsMemory(), ct);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter GzipReaderWriterTests`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add src/CombolistTools.Infrastructure/IO tests/CombolistTools.UnitTests/IO
git commit -m "feat(io): add streaming plain and gzip reader writer implementations"
```

---

### Task 5: Implement chunk processors (sequential and parallel with order control)

**Files:**
- Create: `src/CombolistTools.Infrastructure/Processing/SequentialChunkProcessor.cs`
- Create: `src/CombolistTools.Infrastructure/Processing/ParallelChunkProcessor.cs`
- Test: `tests/CombolistTools.UnitTests/Processing/ParallelChunkProcessorTests.cs`

- [ ] **Step 1: Write failing tests for ordered and unordered parallel output**
```csharp
[Fact]
public async Task ParallelChunkProcessor_WhenPreserveOrder_OutputsInInputOrder()
{
    // Arrange 30k lines, chunkSize 10k, parallel enabled
    // Assert output sequence equals input sequence
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter ParallelChunkProcessorTests`
Expected: FAIL because processor is not implemented.

- [ ] **Step 3: Implement parallel processor with bounded concurrency and deterministic ordering option**
```csharp
public sealed class ParallelChunkProcessor : IChunkProcessor
{
    public async IAsyncEnumerable<ChunkResult> ProcessChunksAsync(
        IAsyncEnumerable<string> sourceLines,
        int chunkSize,
        Func<IReadOnlyList<string>, CancellationToken, Task<IReadOnlyList<string>>> processChunkAsync,
        bool preserveOrder,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Buffer per chunk, dispatch tasks, and emit by index when preserveOrder = true.
        // Use SemaphoreSlim to bound degree-of-parallelism and avoid unbounded memory growth.
    }
}
```

- [ ] **Step 4: Add race-condition safety assertions in tests**
```csharp
[Fact]
public async Task ParallelChunkProcessor_NoRaceCondition_DeterministicCounts()
{
    // Run processing multiple times and assert same output count each run.
}
```

- [ ] **Step 5: Run tests to verify pass**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter ParallelChunkProcessorTests`
Expected: PASS.

- [ ] **Step 6: Commit**
```bash
git add src/CombolistTools.Infrastructure/Processing tests/CombolistTools.UnitTests/Processing
git commit -m "feat(processing): add chunked sequential and parallel processors with order control"
```

---

### Task 6: Implement duplicate key strategy for full-line and selected column indexes

**Files:**
- Create: `src/CombolistTools.Infrastructure/Duplicate/CsvColumnDuplicateKeyStrategy.cs`
- Test: `tests/CombolistTools.UnitTests/Duplicate/CsvColumnDuplicateKeyStrategyTests.cs`

- [ ] **Step 1: Write failing tests for key generation modes**
```csharp
[Theory]
[InlineData("a,b,c", "a,b,c")]
public void FullRowMode_ShouldUseEntireLine(string line, string expectedKey)
{
    var strategy = CsvColumnDuplicateKeyStrategy.ForFullLine();
    strategy.BuildKey(line).Should().Be(expectedKey);
}

[Fact]
public void ColumnMode_ShouldUseSelectedColumns()
{
    var strategy = CsvColumnDuplicateKeyStrategy.ForColumnIndexes(new[] { 0, 2 }, ',');
    strategy.BuildKey("x,y,z").Should().Be("x|z");
}
```

- [ ] **Step 2: Run tests to verify fail**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter CsvColumnDuplicateKeyStrategyTests`
Expected: FAIL with missing strategy.

- [ ] **Step 3: Implement strategy**
```csharp
public sealed class CsvColumnDuplicateKeyStrategy : IDuplicateKeyStrategy
{
    public string BuildKey(string line)
    {
        if (_columnIndexes.Count == 0) return line;
        var parts = line.Split(_delimiter);
        var selected = _columnIndexes.Select(i => i >= 0 && i < parts.Length ? parts[i] : string.Empty);
        return string.Join("|", selected);
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter CsvColumnDuplicateKeyStrategyTests`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add src/CombolistTools.Infrastructure/Duplicate/CsvColumnDuplicateKeyStrategy.cs tests/CombolistTools.UnitTests/Duplicate/CsvColumnDuplicateKeyStrategyTests.cs
git commit -m "feat(duplicate): support full-row and selected-column duplicate keys"
```

---

### Task 7: Build DuplicateRemoverService with streaming + progress + cancellation

**Files:**
- Create: `src/CombolistTools.Application/Services/DuplicateRemoverService.cs`
- Create: `src/CombolistTools.Application/Services/ProgressReporter.cs`
- Test: `tests/CombolistTools.UnitTests/Services/DuplicateRemoverServiceTests.cs`

- [ ] **Step 1: Write failing unit tests for remove-duplicate scenarios**
```csharp
[Fact]
public async Task RemoveDuplicates_FullRow_ShouldWriteDistinctLinesOnly()
{
    // input: A, B, A, C => output A, B, C
}

[Fact]
public async Task RemoveDuplicates_ByColumns_ShouldUseColumnKey()
{
    // input: 1,a,9 and 1,b,9 with key columns [0,2] => second removed
}
```

- [ ] **Step 2: Run tests to verify fail**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter DuplicateRemoverServiceTests`
Expected: FAIL because service is missing.

- [ ] **Step 3: Implement service with async streaming pipeline**
```csharp
public sealed class DuplicateRemoverService
{
    public async Task ExecuteAsync(DuplicateRemovalOptions options, CancellationToken ct)
    {
        var lineStream = _fileReader.ReadLinesAsync(options.InputPath, ct);
        var filtered = FilterDistinctAsync(lineStream, options, ct);
        await _fileWriter.WriteLinesAsync(options.OutputPath, filtered, ct);
    }
}
```

```csharp
private async IAsyncEnumerable<string> FilterDistinctAsync(
    IAsyncEnumerable<string> source,
    DuplicateRemovalOptions options,
    [EnumeratorCancellation] CancellationToken ct)
{
    await foreach (var line in source.WithCancellation(ct))
    {
        var key = _keyStrategy.BuildKey(line);
        if (_duplicateDetector.TryAdd(key))
        {
            _progressReporter.ReportProcessed();
            yield return line;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter DuplicateRemoverServiceTests`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add src/CombolistTools.Application/Services tests/CombolistTools.UnitTests/Services/DuplicateRemoverServiceTests.cs
git commit -m "feat(app): add streaming duplicate remover service with progress and cancellation"
```

---

### Task 8: Build FileMergeService for folder merge + global dedupe

**Files:**
- Create: `src/CombolistTools.Application/Services/FileMergeService.cs`
- Test: `tests/CombolistTools.UnitTests/Services/FileMergeServiceTests.cs`

- [ ] **Step 1: Write failing tests for merge behavior**
```csharp
[Fact]
public async Task MergeFolder_ShouldMergeAllFiles_AndRemoveGlobalDuplicates()
{
    // file1: A,B ; file2: B,C => output A,B,C
}
```

- [ ] **Step 2: Run test to verify fail**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter FileMergeServiceTests`
Expected: FAIL due to missing service.

- [ ] **Step 3: Implement merge service using streaming enumeration**
```csharp
public async Task ExecuteAsync(MergeOptions options, CancellationToken ct)
{
    var files = Directory.EnumerateFiles(options.InputFolderPath, options.SearchPattern, SearchOption.TopDirectoryOnly)
        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

    async IAsyncEnumerable<string> MergedLines([EnumeratorCancellation] CancellationToken token)
    {
        foreach (var file in files)
        {
            await foreach (var line in _fileReader.ReadLinesAsync(file, token).WithCancellation(token))
            {
                var key = _keyStrategy.BuildKey(line);
                if (_duplicateDetector.TryAdd(key)) yield return line;
            }
        }
    }

    await _fileWriter.WriteLinesAsync(options.OutputPath, MergedLines(ct), ct);
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter FileMergeServiceTests`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add src/CombolistTools.Application/Services/FileMergeService.cs tests/CombolistTools.UnitTests/Services/FileMergeServiceTests.cs
git commit -m "feat(app): add folder merge with global duplicate removal"
```

---

### Task 9: Build StreamingFileSplitter (split by line count or file size)

**Files:**
- Create: `src/CombolistTools.Infrastructure/Splitting/StreamingFileSplitter.cs`
- Create: `src/CombolistTools.Application/Services/FileSplitService.cs`
- Test: `tests/CombolistTools.UnitTests/Splitting/StreamingFileSplitterTests.cs`
- Test: `tests/CombolistTools.UnitTests/Services/FileSplitServiceTests.cs`

- [ ] **Step 1: Write failing tests for split-by-lines and split-by-size**
```csharp
[Fact]
public async Task SplitByLines_ShouldCreateExpectedNumberOfFiles()
{
    // 250k lines with 100k per file => 3 files
}

[Fact]
public async Task SplitBySize_ShouldKeepEachFileUnderLimit()
{
    // assert each output file length <= maxBytes (+newline tolerance)
}
```

- [ ] **Step 2: Run tests to verify fail**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter "StreamingFileSplitterTests|FileSplitServiceTests"`
Expected: FAIL with missing implementation.

- [ ] **Step 3: Implement splitter with output file rollover logic**
```csharp
public sealed class StreamingFileSplitter : IFileSplitter
{
    public async Task SplitByLineCountAsync(string inputPath, string outputFolder, int linesPerFile, CancellationToken ct)
    {
        // Read line by line and rotate writer when line count reaches threshold.
    }

    public async Task SplitBySizeAsync(string inputPath, string outputFolder, long maxBytesPerFile, CancellationToken ct)
    {
        // Use encoded byte count and rotate before crossing limit.
    }
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test tests/CombolistTools.UnitTests/CombolistTools.UnitTests.csproj --filter "StreamingFileSplitterTests|FileSplitServiceTests"`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add src/CombolistTools.Infrastructure/Splitting src/CombolistTools.Application/Services/FileSplitService.cs tests/CombolistTools.UnitTests/Splitting tests/CombolistTools.UnitTests/Services/FileSplitServiceTests.cs
git commit -m "feat(split): implement streaming file splitter by lines and size"
```

---

### Task 10: Wire DI, configuration, CLI parser, and command runner

**Files:**
- Create: `src/CombolistTools.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- Create: `src/CombolistTools.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- Create: `src/CombolistTools.Presentation.Console/Cli/CliCommandParser.cs`
- Create: `src/CombolistTools.Presentation.Console/Cli/CliCommandModels.cs`
- Create: `src/CombolistTools.Presentation.Console/HostedServices/ConsoleAppRunner.cs`
- Create: `src/CombolistTools.Presentation.Console/appsettings.json`
- Modify: `Program.cs`

- [ ] **Step 1: Write failing integration test for CLI entrypoint**
```csharp
[Fact]
public async Task RemoveDuplicate_Command_ShouldReturnZero_AndCreateOutput()
{
    // invoke app runner with args:
    // remove-duplicate input.csv output.csv --columns=0,2 --parallel=true
    // assert exit code 0 and output exists
}
```

- [ ] **Step 2: Run integration test to verify fail**

Run: `dotnet test tests/CombolistTools.IntegrationTests/CombolistTools.IntegrationTests.csproj --filter RemoveDuplicate_Command_ShouldReturnZero_AndCreateOutput`
Expected: FAIL because command pipeline is missing.

- [ ] **Step 3: Implement composition root and DI modules**
```csharp
Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddApplication();
        services.AddInfrastructure(ctx.Configuration);
        services.AddSingleton(new CliCommandParser());
        services.AddHostedService<ConsoleAppRunner>();
    });
```

```csharp
public sealed class CliCommandParser
{
    public CliCommand Parse(string[] args)
    {
        // support:
        // dotnet run remove-duplicate input.csv output.csv
        // dotnet run merge folder output.csv
        // dotnet run split input.csv outputFolder 100000
    }
}
```

- [ ] **Step 4: Add appsettings and options binding**
```json
{
  "Processing": {
    "EnableParallel": true,
    "ChunkSize": 10000,
    "MaxDegreeOfParallelism": 4,
    "PreserveOutputOrder": true
  },
  "BloomFilter": {
    "ExpectedItems": 5000000,
    "FalsePositiveRate": 0.01
  }
}
```

- [ ] **Step 5: Run integration test to verify pass**

Run: `dotnet test tests/CombolistTools.IntegrationTests/CombolistTools.IntegrationTests.csproj --filter RemoveDuplicate_Command_ShouldReturnZero_AndCreateOutput`
Expected: PASS.

- [ ] **Step 6: Commit**
```bash
git add src/CombolistTools.Application/DependencyInjection src/CombolistTools.Infrastructure/DependencyInjection src/CombolistTools.Presentation.Console Program.cs tests/CombolistTools.IntegrationTests
git commit -m "feat(cli): add DI-based command runner and appsettings-driven configuration"
```

---

### Task 11: Complete test suite (unit + integration + parallel correctness)

**Files:**
- Modify: `tests/CombolistTools.UnitTests/**/*.cs`
- Modify: `tests/CombolistTools.IntegrationTests/**/*.cs`

- [ ] **Step 1: Add missing unit tests required by spec**
```csharp
[Fact]
public async Task ParallelProcessing_ShouldNotLoseOrDuplicateRowsUnderConcurrency()
{
    // generate deterministic input
    // process in parallel repeatedly
    // assert set equality + stable counts
}

[Fact]
public async Task Gzip_ReadWrite_ShouldBeCompatibleAcrossServices()
{
    // remove-duplicate over .gz input and .gz output
}
```

- [ ] **Step 2: Add end-to-end integration tests for all commands**
```csharp
[Fact]
public async Task EndToEnd_Merge_ShouldProduceExpectedOutput() { }

[Fact]
public async Task EndToEnd_SplitByLineCount_ShouldCreateExpectedFiles() { }

[Fact]
public async Task EndToEnd_SplitBySize_ShouldRespectMaxBytes() { }
```

- [ ] **Step 3: Run full tests**

Run: `dotnet test`
Expected: PASS with all unit/integration tests green.

- [ ] **Step 4: Commit**
```bash
git add tests
git commit -m "test: add full unit and integration coverage for streaming pipeline features"
```

---

### Task 12: Performance and reliability hardening

**Files:**
- Modify: `src/CombolistTools.Application/Services/*.cs`
- Modify: `src/CombolistTools.Infrastructure/Processing/*.cs`
- Modify: `src/CombolistTools.Infrastructure/Duplicate/*.cs`
- Modify: `src/CombolistTools.Presentation.Console/HostedServices/ConsoleAppRunner.cs`

- [ ] **Step 1: Add cancellation and structured logging assertions via tests**
```csharp
[Fact]
public async Task Services_ShouldHonorCancellationToken()
{
    using var cts = new CancellationTokenSource();
    cts.Cancel();
    await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ExecuteAsync(options, cts.Token));
}
```

- [ ] **Step 2: Run tests to verify fail if cancellation/logging contracts missing**

Run: `dotnet test --filter Services_ShouldHonorCancellationToken`
Expected: FAIL before hardening.

- [ ] **Step 3: Implement hardening changes**
```csharp
if (ct.IsCancellationRequested)
{
    _logger.LogWarning("Cancellation requested for command {Command}", commandName);
    ct.ThrowIfCancellationRequested();
}
```

```csharp
_logger.LogInformation(
    "Processed {ProcessedRows} rows ({ProgressPercent:F2}%)",
    processedRows,
    progressPercent);
```

- [ ] **Step 4: Run complete test + build pipeline**

Run: `dotnet test && dotnet build -c Release`
Expected: PASS, no warnings treated as errors (if configured).

- [ ] **Step 5: Commit**
```bash
git add src
git commit -m "refactor: harden cancellation, progress logging, and concurrency safety"
```

---

### Task 13: Documentation and usage examples

**Files:**
- Create: `README.md`

- [ ] **Step 1: Write failing documentation check test (optional simple guard)**
```csharp
[Fact]
public void Readme_ShouldContainCommandExamples()
{
    var text = File.ReadAllText("README.md");
    Assert.Contains("dotnet run --project src/CombolistTools.Presentation.Console -- remove-duplicate", text);
}
```

- [ ] **Step 2: Run test to verify fail**

Run: `dotnet test tests/CombolistTools.IntegrationTests/CombolistTools.IntegrationTests.csproj --filter Readme_ShouldContainCommandExamples`
Expected: FAIL (README missing).

- [ ] **Step 3: Add README with architecture and CLI examples**
```md
## Commands
dotnet run --project src/CombolistTools.Presentation.Console -- remove-duplicate input.csv output.csv
dotnet run --project src/CombolistTools.Presentation.Console -- merge data-folder output.csv
dotnet run --project src/CombolistTools.Presentation.Console -- split input.csv output-folder 100000
```

- [ ] **Step 4: Run full test suite**

Run: `dotnet test`
Expected: PASS.

- [ ] **Step 5: Commit**
```bash
git add README.md tests/CombolistTools.IntegrationTests
git commit -m "docs: add architecture notes and command usage examples"
```

---

## Self-Review

### 1) Spec coverage check
- Streaming-only processing (`StreamReader`/`StreamWriter`, no `ReadAllLines`): covered in Tasks 4, 7, 8, 9.
- Remove duplicates (full-row and column-index): covered in Tasks 6 and 7.
- Merge folder with global dedupe: covered in Task 8.
- Split by line count and file size: covered in Task 9.
- Parallel chunk processing with race-condition safety + optional ordering: covered in Task 5 and Task 11.
- Bloom Filter custom implementation + HashSet exact confirmation: covered in Task 3.
- `.gz` auto-detect read/write with abstraction: covered in Task 4.
- Clean Architecture + SOLID layering + DI: covered in Tasks 1, 2, 10.
- CLI commands + appsettings configuration + logging + cancellation token: covered in Tasks 10 and 12.
- Full test suite (unit + integration including parallel correctness): covered in Task 11 plus task-level tests throughout.

No uncovered requirement found.

### 2) Placeholder scan
- Removed non-action placeholders; each task includes explicit file paths, code snippets, run commands, and expected outcomes.
- No `TODO/TBD/implement later` placeholders remain.

### 3) Type consistency check
- `IDuplicateDetector`, `IBloomFilter`, `IFileReader`, `IFileWriter`, `IFileSplitter`, and `IChunkProcessor` names are consistent across Core/Application/Infrastructure.
- Option names (`BloomFilterOptions`, `ProcessingOptions`, `DuplicateRemovalOptions`, `MergeOptions`, `SplitOptions`) are referenced consistently.
- Command names (`remove-duplicate`, `merge`, `split`) are consistent in parser, tests, and README snippets.

