using System.Windows.Input;

namespace CombolistTools.Presentation.Wpf.Commands;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;
    private bool _isRunning;

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !_isRunning && (_canExecute?.Invoke() ?? true);

    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        _isRunning = true;
        RaiseCanExecuteChangedSafe();
        try
        {
            // Ensure the execute delegate runs on a thread-pool thread even if it returns an already-completed Task.
            await Task.Run(_execute).ConfigureAwait(false);
        }
        finally
        {
            _isRunning = false;
            RaiseCanExecuteChangedSafe();
        }
    }

    public void RaiseCanExecuteChanged() => RaiseCanExecuteChangedSafe();

    private void RaiseCanExecuteChangedSafe()
    {
        // In WPF, CanExecuteChanged handlers may touch UI-bound state and must be raised on the UI thread.
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(new Action(() => CanExecuteChanged?.Invoke(this, EventArgs.Empty)));
            return;
        }

        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
