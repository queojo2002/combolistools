using System.Windows.Input;
using CombolistTools.Core;
using CombolistTools.Presentation.Wpf.Commands;
using CombolistTools.Presentation.Wpf.Services;

namespace CombolistTools.Presentation.Wpf.ViewModels;

public sealed class SplitViewModel : ViewModelBase
{
    private readonly IJobExecutionService _jobs;
    private readonly IUiLogSink _log;
    private readonly IPathPickerService _picker;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private string _inputPath = "";
    private string _outputFolder = "";
    private string _threshold = "100000";
    private bool _isBySize;

    public string InputPath { get => _inputPath; set { if (SetProperty(ref _inputPath, value)) RaiseCommands(); } }
    public string OutputFolder { get => _outputFolder; set { if (SetProperty(ref _outputFolder, value)) RaiseCommands(); } }
    public string Threshold { get => _threshold; set => SetProperty(ref _threshold, value); }
    public bool IsBySize { get => _isBySize; set => SetProperty(ref _isBySize, value); }
    public bool IsRunning { get => _isRunning; private set { if (SetProperty(ref _isRunning, value)) RaiseCommands(); } }
    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseInputCommand { get; }
    public ICommand BrowseOutputFolderCommand { get; }

    public SplitViewModel(IJobExecutionService jobs, IUiLogSink log, IPathPickerService picker)
    {
        _jobs = jobs;
        _log = log;
        _picker = picker;
        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsRunning && !string.IsNullOrWhiteSpace(InputPath) && !string.IsNullOrWhiteSpace(OutputFolder));
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsRunning);
        BrowseInputCommand = new RelayCommand(BrowseInput);
        BrowseOutputFolderCommand = new RelayCommand(BrowseOutputFolder);
    }

    private async Task RunAsync()
    {
        try
        {
            IsRunning = true;
            _cts = new CancellationTokenSource();
            await _jobs.SplitAsync(new SplitOptions
            {
                InputPath = InputPath,
                OutputFolder = OutputFolder,
                Mode = IsBySize ? SplitMode.BySizeBytes : SplitMode.ByLineCount,
                Threshold = long.Parse(Threshold)
            }, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _log.Write("split canceled.");
        }
        catch (Exception ex)
        {
            _log.Write($"split failed: {ex.Message}");
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

    private void BrowseInput()
    {
        var selected = _picker.PickInputFile(InputPath, "CSV/TXT/GZip (*.csv;*.txt;*.gz)|*.csv;*.txt;*.gz|All files (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(selected)) InputPath = selected;
    }

    private void BrowseOutputFolder()
    {
        var selected = _picker.PickFolder(OutputFolder);
        if (!string.IsNullOrWhiteSpace(selected)) OutputFolder = selected;
    }
}
