using System.Windows.Input;
using CombolistTools.Core;
using CombolistTools.Presentation.Wpf.Commands;
using CombolistTools.Presentation.Wpf.Services;

namespace CombolistTools.Presentation.Wpf.ViewModels;

public sealed class CapitalizeLineViewModel : ViewModelBase
{
    private readonly IJobExecutionService _jobs;
    private readonly IUiLogSink _log;
    private readonly IPathPickerService _picker;
    private CancellationTokenSource? _cts;
    private bool _isRunning;
    private string _inputPath = "";
    private string _outputPath = "";

    public string InputPath { get => _inputPath; set { if (SetProperty(ref _inputPath, value)) RaiseCommands(); } }
    public string OutputPath { get => _outputPath; set { if (SetProperty(ref _outputPath, value)) RaiseCommands(); } }
    public bool IsRunning { get => _isRunning; private set { if (SetProperty(ref _isRunning, value)) RaiseCommands(); } }
    public ICommand RunCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand BrowseInputCommand { get; }
    public ICommand BrowseOutputCommand { get; }

    public CapitalizeLineViewModel(IJobExecutionService jobs, IUiLogSink log, IPathPickerService picker)
    {
        _jobs = jobs;
        _log = log;
        _picker = picker;
        RunCommand = new AsyncRelayCommand(RunAsync, () => !IsRunning && !string.IsNullOrWhiteSpace(InputPath) && !string.IsNullOrWhiteSpace(OutputPath));
        CancelCommand = new AsyncRelayCommand(CancelAsync, () => IsRunning);
        BrowseInputCommand = new RelayCommand(BrowseInput);
        BrowseOutputCommand = new RelayCommand(BrowseOutput);
    }

    private async Task RunAsync()
    {
        try
        {
            IsRunning = true;
            _cts = new CancellationTokenSource();
            await _jobs.CapitalizeLineAsync(new CapitalizeLineOptions
            {
                InputPath = InputPath,
                OutputPath = OutputPath
            }, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            _log.Write("capitalize first character canceled.");
        }
        catch (Exception ex)
        {
            _log.Write($"capitalize first character failed: {ex.Message}");
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
        var selected = _picker.PickInputFile(InputPath, "Text/CSV/GZip (*.txt;*.csv;*.gz)|*.txt;*.csv;*.gz|All files (*.*)|*.*");
        if (!string.IsNullOrWhiteSpace(selected)) InputPath = selected;
    }

    private void BrowseOutput()
    {
        var selected = _picker.PickOutputFile(OutputPath);
        if (!string.IsNullOrWhiteSpace(selected)) OutputPath = selected;
    }
}
