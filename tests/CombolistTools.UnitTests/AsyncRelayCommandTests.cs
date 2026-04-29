using System.Threading;

using CombolistTools.Presentation.Wpf.Commands;
using Xunit;

namespace CombolistTools.UnitTests;

public class AsyncRelayCommandTests
{
    [Fact]
    public async Task Execute_ShouldRunDelegateOnThreadPoolThread()
    {
        var delegateIsThreadPoolThreadTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Func<Task> execute = () =>
        {
            delegateIsThreadPoolThreadTcs.TrySetResult(Thread.CurrentThread.IsThreadPoolThread);
            return Task.CompletedTask;
        };

        var command = new AsyncRelayCommand(execute);

        var callerIsThreadPoolThreadTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var callerThread = new Thread(() =>
        {
            callerIsThreadPoolThreadTcs.TrySetResult(Thread.CurrentThread.IsThreadPoolThread);
            command.Execute(null);
        });

        callerThread.Start();

        var callerIsThreadPoolThread = await callerIsThreadPoolThreadTcs.Task;

        var completedTask = await Task.WhenAny(
            delegateIsThreadPoolThreadTcs.Task,
            Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.True(ReferenceEquals(completedTask, delegateIsThreadPoolThreadTcs.Task), "Delegate did not run within timeout.");

        var delegateIsThreadPoolThread = await delegateIsThreadPoolThreadTcs.Task;

        callerThread.Join();

        Assert.False(callerIsThreadPoolThread, "Test caller thread should not be a thread pool thread.");
        Assert.True(delegateIsThreadPoolThread, "Delegate should run on a thread pool thread.");
    }
}

