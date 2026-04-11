using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
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

using WinWhisper.Views.Animations;

namespace WinWhisper.Views.Controls;

/// <summary>
/// Interaction logic for PulseBar.xaml
/// </summary>
public partial class PulseBar : UserControl
{
    public DoubleCrossFade? Animation { get; private set; }

    public static readonly DependencyProperty IndexProperty =
            DependencyProperty.Register(
                nameof(Index),
                typeof(int),
                typeof(PulseBar),
                new FrameworkPropertyMetadata(0));

        public int Index
        {
            get => (int)GetValue(IndexProperty);
            set => SetValue(IndexProperty, value);
        }

        public static readonly DependencyProperty HeightValueProperty =
            DependencyProperty.Register(
                nameof(HeightValue),
                typeof(double),
                typeof(PulseBar),
                new PropertyMetadata(0.0, OnHeightValueChanged));

        public double HeightValue
        {
            get => (double)GetValue(HeightValueProperty);
            set => SetValue(HeightValueProperty, value);
        }

        private static void OnHeightValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PulseBar bar)
            {
                const double MAX_HEIGHT = 20;
                const double MIN_HEIGHT = 6;
                double v = Math.Clamp((double)e.NewValue, 0.0, 1.0);
                bar.Height = v * (MAX_HEIGHT - MIN_HEIGHT) + MIN_HEIGHT;
            }
        }

        public PulseBar()
        {
            InitializeComponent();
        }

        private void PulseBar_Loaded(object? sender, RoutedEventArgs e)
        {
            if (Animation != null) return;

            int index = Index;
            var fadeAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(600),
            };
            fadeAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            fadeAnim.KeyFrames.Add(new EasingDoubleKeyFrame(index % 2 == 0 ? 0.4 : 0.1, KeyTime.FromPercent(0.5), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            fadeAnim.KeyFrames.Add(new EasingDoubleKeyFrame(index % 2 == 0 ? 0.0 : 0.4, KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));

            // Loop animation
            var pulseAnim = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(600),
                RepeatBehavior = RepeatBehavior.Forever,
                BeginTime = TimeSpan.FromMilliseconds(index * 100)
            };
            pulseAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromPercent(0.0), new QuadraticEase { EasingMode = EasingMode.EaseOut }));
            pulseAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(0.5), new QuadraticEase { EasingMode = EasingMode.EaseInOut }));
            pulseAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromPercent(1.0), new QuadraticEase { EasingMode = EasingMode.EaseIn }));

        Animation = new DoubleCrossFade(fadeAnim, pulseAnim, TimeSpan.FromMilliseconds(600), TimeSpan.FromMilliseconds(1000), this, HeightValueProperty);
    }
}