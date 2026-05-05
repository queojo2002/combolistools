using CombolistTools.Application;
using CombolistTools.Core;

namespace CombolistTools.Presentation.Wpf.Services;

public interface IUiLogSink
{
    void Write(string message);
}

public interface IJobExecutionService
{
    Task RemoveDuplicateAsync(DuplicateRemovalOptions options, CancellationToken cancellationToken);
    Task MergeAsync(MergeOptions options, CancellationToken cancellationToken);
    Task SplitAsync(SplitOptions options, CancellationToken cancellationToken);
    Task FilterUserPassAsync(UserPassFilterOptions options, CancellationToken cancellationToken);
    Task CapitalizeLineAsync(CapitalizeLineOptions options, CancellationToken cancellationToken);
}

public sealed class UiLogSink : IUiLogSink
{
    public event Action<string>? OnMessage;
    public void Write(string message) => OnMessage?.Invoke(message);
}

public sealed class JobExecutionService : IJobExecutionService
{
    private readonly DuplicateRemoverService _duplicateRemover;
    private readonly FileMergeService _mergeService;
    private readonly FileSplitService _splitService;
    private readonly FolderUserPassFilterService _folderUserPassFilterService;
    private readonly CapitalizeLineService _capitalizeLineService;
    private readonly IUiLogSink _logSink;

    public JobExecutionService(
        DuplicateRemoverService duplicateRemover,
        FileMergeService mergeService,
        FileSplitService splitService,
        FolderUserPassFilterService folderUserPassFilterService,
        CapitalizeLineService capitalizeLineService,
        IUiLogSink logSink)
    {
        _duplicateRemover = duplicateRemover;
        _mergeService = mergeService;
        _splitService = splitService;
        _folderUserPassFilterService = folderUserPassFilterService;
        _capitalizeLineService = capitalizeLineService;
        _logSink = logSink;
    }

    public async Task RemoveDuplicateAsync(DuplicateRemovalOptions options, CancellationToken cancellationToken)
    {
        _logSink.Write($"Running remove-duplicate: {options.InputPath} -> {options.OutputPath}");
        await _duplicateRemover.ExecuteAsync(options, cancellationToken);
        _logSink.Write("remove-duplicate completed.");
    }

    public async Task MergeAsync(MergeOptions options, CancellationToken cancellationToken)
    {
        _logSink.Write($"Running merge: {options.InputFolderPath} -> {options.OutputPath}");
        await _mergeService.ExecuteAsync(options, cancellationToken);
        _logSink.Write("merge completed.");
    }

    public async Task SplitAsync(SplitOptions options, CancellationToken cancellationToken)
    {
        _logSink.Write($"Running split: {options.InputPath} -> {options.OutputFolder}");
        await _splitService.ExecuteAsync(options, cancellationToken);
        _logSink.Write("split completed.");
    }

    public async Task FilterUserPassAsync(UserPassFilterOptions options, CancellationToken cancellationToken)
    {
        _logSink.Write($"Running user:pass filter: {options.InputFolderPath} -> {options.OutputPath}");
        await _folderUserPassFilterService.ExecuteAsync(options, cancellationToken);
        _logSink.Write("user:pass filter completed.");
    }

    public async Task CapitalizeLineAsync(CapitalizeLineOptions options, CancellationToken cancellationToken)
    {
        _logSink.Write($"Running capitalize first character: {options.InputPath} -> {options.OutputPath}");
        await _capitalizeLineService.ExecuteAsync(options, cancellationToken);
        _logSink.Write("capitalize first character completed.");
    }
}
