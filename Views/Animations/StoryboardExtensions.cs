using System.Windows;
using System.Windows.Media.Animation;

namespace WinWhisper.Views.Animations;

public static class StoryboardExtensions
{
    public static ClockState? TryGetCurrentState(this Storyboard storyboard, FrameworkElement target)
    {
        try
        {
            return storyboard.GetCurrentState(target);
        }
        catch (InvalidOperationException)
        {
            // Storyboard is not begun on the target
            return null;
        }
    }

    public static void SafeStop(this Storyboard storyboard, FrameworkElement target)
    {
        if (storyboard == null) return;
        var state = storyboard.TryGetCurrentState(target);
        if (state is not null && state != ClockState.Stopped)
        {
            storyboard.Stop(target);
        }
    }

    public static void SafeSkipToFill(this Storyboard storyboard, FrameworkElement target)
    {
        if (storyboard == null) return;
        var state = storyboard.TryGetCurrentState(target);
        if (state == ClockState.Active)
        {
            storyboard.SkipToFill(target);
        }
    }
}