using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WinWhisper.Views.Animations;

public sealed class DoubleCrossFade : DependencyObject
{
    private readonly Storyboard _storyboard;
    private readonly FrameworkElement _target;
    private readonly DependencyProperty _targetProperty;

    public static readonly DependencyProperty ValueAProperty =
            DependencyProperty.Register(
                nameof(ValueA),
                typeof(double),
                typeof(DoubleCrossFade),
                new PropertyMetadata(0.0, OnBlending));

    public double ValueA
    {
        get => (double)GetValue(ValueAProperty);
        set => SetValue(ValueAProperty, value);
    }

    public static readonly DependencyProperty ValueBProperty =
        DependencyProperty.Register(
            nameof(ValueB),
            typeof(double),
            typeof(DoubleCrossFade),
            new PropertyMetadata(0.0, OnBlending));

    public double ValueB
    {
        get => (double)GetValue(ValueBProperty);
        set => SetValue(ValueBProperty, value);
    }

    public static readonly DependencyProperty AlphaProperty =
        DependencyProperty.Register(
            nameof(Alpha),
            typeof(double),
            typeof(DoubleCrossFade),
            new PropertyMetadata(0.0, OnBlending));

    public double Alpha
    {
        get => (double)GetValue(AlphaProperty);
        set => SetValue(AlphaProperty, value);
    }

    private static void OnBlending(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DoubleCrossFade self)
        {
            var value = self.ValueA * (1.0 - self.Alpha) + self.ValueB * self.Alpha;
            self._target.SetValue(self._targetProperty, value);
        }
    }

    public DoubleCrossFade(
        DoubleAnimationBase animA,
        DoubleAnimationBase animB,
        TimeSpan blendStart,
        TimeSpan blendEnd,
        FrameworkElement target,
        DependencyProperty targetProperty)
    {
        var animAlpha = new DoubleAnimationUsingKeyFrames()
        {
            Duration = blendEnd,
        };
        animAlpha.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, TimeSpan.Zero));
        animAlpha.KeyFrames.Add(new LinearDoubleKeyFrame(0.0, blendStart));
        animAlpha.KeyFrames.Add(new LinearDoubleKeyFrame(1.0, blendEnd));

        _storyboard = new Storyboard();
        _storyboard.Children.Add(animA);
        _storyboard.Children.Add(animB);
        _storyboard.Children.Add(animAlpha);
        Storyboard.SetTarget(animA, this);
        Storyboard.SetTargetProperty(animA, new PropertyPath("ValueA"));
        Storyboard.SetTarget(animB, this);
        Storyboard.SetTargetProperty(animB, new PropertyPath("ValueB"));
        Storyboard.SetTarget(animAlpha, this);
        Storyboard.SetTargetProperty(animAlpha, new PropertyPath("Alpha"));

        _target = target;
        _targetProperty = targetProperty;
    }

    public void Begin()
    {
        ValueA = GetInitialValue(_storyboard.Children[0], ValueA);
        ValueB = GetInitialValue(_storyboard.Children[1], ValueB);
        Alpha = 0.0;
        _storyboard.Begin(_target, true);
    }

    public void Stop()
    {
        _storyboard.SafeStop(_target);
    }

    public ClockState GetCurrentState()
    {
        return _storyboard.GetCurrentState(_target);
    }

    public TimeSpan GetCurrentTime()
    {
        return _storyboard.GetCurrentTime(_target) ?? TimeSpan.Zero;
    }

    private double GetInitialValue(Timeline anim, double defaultValue)
    {
        switch (anim)
        {
            case DoubleAnimation da:
                return da.From ?? defaultValue;

            case DoubleAnimationUsingKeyFrames dakf:
                if (dakf.KeyFrames != null && dakf.KeyFrames.Count > 0)
                    return dakf.KeyFrames[0].Value;
                return defaultValue;
        }
        return defaultValue;
    }
}