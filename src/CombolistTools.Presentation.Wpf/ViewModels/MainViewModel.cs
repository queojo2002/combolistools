using System.Collections.ObjectModel;
using CombolistTools.Presentation.Wpf.Services;

namespace CombolistTools.Presentation.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    public RemoveDuplicateViewModel RemoveDuplicate { get; }
    public MergeViewModel Merge { get; }
    public SplitViewModel Split { get; }
    public ObservableCollection<string> Logs { get; } = [];

    public MainViewModel(IJobExecutionService jobs, IUiLogSink logSink, IPathPickerService picker)
    {
        RemoveDuplicate = new RemoveDuplicateViewModel(jobs, logSink, picker);
        Merge = new MergeViewModel(jobs, logSink, picker);
        Split = new SplitViewModel(jobs, logSink, picker);

        if (logSink is UiLogSink sink)
        {
            sink.OnMessage += message =>
            {
                App.Current.Dispatcher.Invoke(() =>
                {
                    Logs.Add($"{DateTime.Now:HH:mm:ss}  {message}");
                    if (Logs.Count > 500) Logs.RemoveAt(0);
                });
            };
        }
    }
}
