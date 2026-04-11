using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace WinWhisper.Views.Controls;

/// <summary>
/// Interaction logic for ProgressFill.xaml
/// </summary>
public partial class ProgressFill : UserControl
{
    private readonly Storyboard _fadeAnimation;

    public ProgressFill()
    {
        InitializeComponent();
        _fadeAnimation = (Storyboard)this.Resources["FadeAnimation"];
    }

    public void Fade(bool show, bool skipToFill = false)
    {
        var anim = (DoubleAnimation)_fadeAnimation.Children[0];
        anim.From = show ? 0.0 : 1.0;
        anim.To = show ? 1.0 : 0.0;
        var currentValue = this.Opacity;
        _fadeAnimation.Begin(this, true);
        if (skipToFill || currentValue == anim.To)
            _fadeAnimation.SkipToFill(this);
    }
}