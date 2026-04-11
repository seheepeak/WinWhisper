using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace WinWhisper.Views.Controls;

/// <summary>
/// Interaction logic for VoiceActivityIndicator.xaml
/// </summary>
public partial class VoiceActivityIndicator : UserControl
{
    private readonly PulseBar[] _pbars;
    private readonly DispatcherTimer _lingerTimer;

    public VoiceActivityIndicator()
    {
        InitializeComponent();

        _pbars = Root.Children.OfType<PulseBar>().ToArray();
        _lingerTimer = new DispatcherTimer();
        _lingerTimer.Tick += LingerTimeout;

        // Handle visibility changed
        IsVisibleChanged += OnVisibleChanged;
    }

    private void LingerTimeout(object? sender, EventArgs e)
    {
        _lingerTimer.Stop();
        Visibility = Visibility.Collapsed;
    }

    private TimeSpan HideInterval
    {
        get
        {
            var elapsed = _pbars[0].Animation?.GetCurrentTime() ?? TimeSpan.Zero;
            var duration = TimeSpan.FromMilliseconds(600);
            return (int)Math.Max(elapsed / duration + 3, 4) * duration - elapsed;
        }
    }

    public void ShowLinger()
    {
        Visibility = Visibility.Visible;
        _lingerTimer.Stop();
        _lingerTimer.Interval = HideInterval;
        _lingerTimer.Start();
    }

    private void OnVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (Visibility == Visibility.Visible)
        {
            foreach (var pbar in _pbars)
            {
                pbar.Animation?.Begin();
            }
        }
        else
        {
            _lingerTimer.Stop();
            foreach (var pbar in _pbars)
            {
                pbar.Animation?.Stop();
            }
        }
    }
}