using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace WinWhisper.Common.Threading;

public static class DispatcherHelper
{
    [Conditional("DEBUG")]
    public static void AssertMainThread()
    {
        Debug.Assert(
            Application.Current?.Dispatcher.CheckAccess() ?? true,
            "This method must be called from the main thread"
        );
    }

    public static void RunOnMainThread(this Dispatcher dispatcher, Action action)
    {
        if (dispatcher.CheckAccess())
            action();
        else
            dispatcher.InvokeAsync(action);
    }

    public static void RunOnMainThread(this Dispatcher dispatcher, Func<Task> action)
    {
        if (dispatcher.CheckAccess())
            _ = action();
        else
            dispatcher.InvokeAsync(action);
    }
}
