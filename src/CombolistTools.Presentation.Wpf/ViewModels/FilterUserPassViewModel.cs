using System.Windows.Input;
using CombolistTools.Core;
using CombolistTools.Presentation.Wpf.Commands;
using CombolistTools.Presentation.Wpf.Services;

namespace CombolistTools.Presentation.Wpf.ViewModels;

public sealed class FilterUserPassViewModel : ViewModelBase
{
    private readonly IJobExecutionService _jobs;
    private readonly IUiLogSink _log;
    private readonly IPathPickerService _picker;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private string _inputFolder = "";
    private string _outputPath = "";

    public string InputFolder
    {
        get => _inputFolder;
        set
        {
            if (SetProperty(ref _inputFolder, value)) RaiseCommands();
        }
    }

    public string OutputPath
    {
        get => _outputPath;
        set
        {
            if (SetProperty(ref _outputPath, value)) RaiseCommands();
        }
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (SetProperty(ref _isRunning, value)) RaiseCommands();
        }
    }

    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseInputFolderCommand { get; }
    public ICommand BrowseOutputCommand { get; }

    public FilterUserPassViewModel(IJobExecutionService jobs, IUiLogSink log, IPathPickerService picker)
    {
        _jobs = jobs;
        _log = log;
        _picker = picker;

        RunCommand = new AsyncRelayCommand(RunAsync,
            () => !IsRunning && !string.IsNullOrWhiteSpace(InputFolder) && !string.IsNullOrWhiteSpace(OutputPath));
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsRunning);

        BrowseInputFolderCommand = new RelayCommand(BrowseInputFolder);
        BrowseOutputCommand = new RelayCommand(BrowseOutputFile);
    }

    private async Task RunAsync()
    {
        try
        {
            IsRunning = true;
            _cts = new CancellationTokenSource();
            await _jobs.FilterUserPassAsync(
                new UserPassFilterOptions
                {
                    InputFolderPath = InputFolder,
                    OutputPath = OutputPath
                },
                _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _log.Write("user:pass filter canceled.");
        }
        catch (Exception ex)
        {
            _log.Write($"user:pass filter failed: {ex.Message}");
        }
        finally
        {
            IsRunning = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private Task CancelAsync()
    {
        _cts?.Cancel();
        return Task.CompletedTask;
    }

    private void RaiseCommands()
    {
        if (RunCommand is AsyncRelayCommand run) run.RaiseCanExecuteChanged();
        if (CancelCommand is AsyncRelayCommand cancel) cancel.RaiseCanExecuteChanged();
    }

    private void BrowseInputFolder()
    {
        var selected = _picker.PickFolder(InputFolder);
        if (!string.IsNullOrWhiteSpace(selected)) InputFolder = selected;
    }

    private void BrowseOutputFile()
    {
        var selected = _picker.PickOutputFile(OutputPath);
        if (!string.IsNullOrWhiteSpace(selected)) OutputPath = selected;
    }
}

