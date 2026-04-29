using CombolistTools.Core;
using Microsoft.Extensions.Logging;

namespace CombolistTools.Application;

public sealed class DuplicateRemoverService
{
    private readonly IFileReader _reader;
    private readonly IFileWriter _writer;
    private readonly IChunkProcessor _chunkProcessor;
    private readonly Func<int[], char, IDuplicateKeyStrategy> _keyStrategyFactory;
    private readonly Func<IDuplicateDetector> _detectorFactory;
    private readonly ProcessingOptions _processingOptions;
    private readonly ILogger<DuplicateRemoverService> _logger;

    public DuplicateRemoverService(
        IFileReader reader,
        IFileWriter writer,
        IChunkProcessor chunkProcessor,
        Func<int[], char, IDuplicateKeyStrategy> keyStrategyFactory,
        Func<IDuplicateDetector> detectorFactory,
        ProcessingOptions processingOptions,
        ILogger<DuplicateRemoverService> logger)
    {
        _reader = reader;
        _writer = writer;
        _chunkProcessor = chunkProcessor;
        _keyStrategyFactory = keyStrategyFactory;
        _detectorFactory = detectorFactory;
        _processingOptions = processingOptions;
        _logger = logger;
    }

    public async Task ExecuteAsync(DuplicateRemovalOptions options, CancellationToken cancellationToken)
    {
        var strategy = _keyStrategyFactory(options.ColumnIndexes, options.Delimiter);
        var detector = _detectorFactory();
        long processed = 0;
        long written = 0;

        async Task<IReadOnlyList<string>> ProcessChunk(IReadOnlyList<string> chunk, CancellationToken ct)
        {
            var result = new List<string>(chunk.Count);
            foreach (var line in chunk)
            {
                ct.ThrowIfCancellationRequested();
                processed++;
                if (detector.TryAdd(strategy.BuildKey(line)))
                {
                    written++;
                    result.Add(line);
                }
            }

            _logger.LogInformation("Processed {Processed} rows, written {Written}", processed, written);
            return await Task.FromResult(result);
        }

        var output = _chunkProcessor.ProcessAsync(
            _reader.ReadLinesAsync(options.InputPath, cancellationToken),
            ProcessChunk,
            _processingOptions,
            cancellationToken);

        await _writer.WriteLinesAsync(options.OutputPath, output, cancellationToken);
    }
}

public sealed class FileMergeService
{
    private readonly IFileReader _reader;
    private readonly IFileWriter _writer;
    private readonly Func<int[], char, IDuplicateKeyStrategy> _keyStrategyFactory;
    private readonly Func<IDuplicateDetector> _detectorFactory;

    public FileMergeService(
        IFileReader reader,
        IFileWriter writer,
        Func<int[], char, IDuplicateKeyStrategy> keyStrategyFactory,
        Func<IDuplicateDetector> detectorFactory)
    {
        _reader = reader;
        _writer = writer;
        _keyStrategyFactory = keyStrategyFactory;
        _detectorFactory = detectorFactory;
    }

    public Task ExecuteAsync(MergeOptions options, CancellationToken cancellationToken)
    {
        var outputFullPath = Path.GetFullPath(options.OutputPath);
        var files = Directory.EnumerateFiles(options.InputFolderPath, options.SearchPattern, SearchOption.TopDirectoryOnly)
            .Where(path => !string.Equals(Path.GetFullPath(path), outputFullPath, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var strategy = _keyStrategyFactory(options.ColumnIndexes, options.Delimiter);
        var detector = _detectorFactory();

        return _writer.WriteLinesAsync(options.OutputPath, Enumerate(), cancellationToken);

        async IAsyncEnumerable<string> Enumerate()
        {
            foreach (var file in files)
            {
                await foreach (var line in _reader.ReadLinesAsync(file, cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (detector.TryAdd(strategy.BuildKey(line)))
                    {
                        yield return line;
                    }
                }
            }
        }
    }
}

public sealed class FileSplitService
{
    private readonly IFileSplitter _splitter;
    public FileSplitService(IFileSplitter splitter) => _splitter = splitter;

    public Task ExecuteAsync(SplitOptions options, CancellationToken cancellationToken)
    {
        return options.Mode switch
        {
            SplitMode.ByLineCount => _splitter.SplitByLineCountAsync(options.InputPath, options.OutputFolder, (int)options.Threshold, cancellationToken),
            SplitMode.BySizeBytes => _splitter.SplitBySizeAsync(options.InputPath, options.OutputFolder, options.Threshold, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(options.Mode))
        };
    }
}
