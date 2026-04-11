using System.Windows;
using System.Windows.Controls;

namespace WinWhisper.Views.Controls;

/// <summary>
/// Interaction logic for ToggleSwitch.xaml
/// </summary>
public partial class ToggleSwitch : UserControl
{
    public static readonly DependencyProperty IsCheckedProperty =
            DependencyProperty.Register(
                nameof(IsChecked),
                typeof(bool),
                typeof(ToggleSwitch),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnIsCheckedChanged));

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public ToggleSwitch()
    {
        InitializeComponent();
    }

    private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ToggleSwitch toggleSwitch)
        {
            toggleSwitch.InternalToggleButton.IsChecked = (bool)e.NewValue;
        }
    }

    private void InternalToggleButton_Loaded(object sender, RoutedEventArgs e)
    {
        InternalToggleButton.IsChecked = IsChecked;
    }

    private void InternalToggleButton_Checked(object sender, RoutedEventArgs e)
    {
        IsChecked = true;
    }

    private void InternalToggleButton_Unchecked(object sender, RoutedEventArgs e)
    {
        IsChecked = false;
    }
}
