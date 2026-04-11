using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

using WinForms = System.Windows.Forms;


namespace WinWhisper.Views.Animations;

public class MouseFollower(
        Window source,
        Rect bbox,
        double speed = 0.55,      // seconds to ~95% target
        bool ease = true,         // reproduce expo-out curve
        double stickDelta = 0.15)
{
    private readonly Window _source = source;
    private readonly Rect _bbox = bbox;
    private readonly double _tau = speed;
    private readonly bool _ease = ease;
    private readonly double _stickDelta = stickDelta;

    private Point _pos = new Point(0, 0);
    private Point? _stick = null;

    private readonly Stopwatch _stopwatch = new Stopwatch();
    private TimeSpan _lastElapsed = TimeSpan.Zero;


    public void Start(bool immediate = false)
    {
        this._lastElapsed = TimeSpan.Zero;
        this._stopwatch.Start();

        if (immediate)
        {
            this._pos = CalculateTargetPos();
            this._source.Left = this._pos.X;
            this._source.Top = this._pos.Y;
        }
        else
        {
            this._pos = new Point(_source.Left, _source.Top);
        }

        CompositionTarget.Rendering += Timer_Tick;
    }

    public void Stop()
    {
        this._stopwatch.Stop();
        CompositionTarget.Rendering -= Timer_Tick;
    }

    public void SetStick(Point? rectCenter)
    {
        // Pass a Point (centre of element) or null to disable
        this._stick = rectCenter;
    }

    private Point PhysicalToLogical(System.Drawing.Point physical)
    {
        var ps = PresentationSource.FromVisual(this._source);
        if (ps?.CompositionTarget == null)
            return new Point(physical.X, physical.Y);
        var transform = ps.CompositionTarget.TransformFromDevice;
        return transform.Transform(new Point(physical.X, physical.Y));
    }

    private Point CalculateTargetPos()
    {
        var cursorPos_wf = WinForms.Cursor.Position;
        var currentScreen = WinForms.Screen.FromPoint(cursorPos_wf);
        var lt_wf = currentScreen.WorkingArea.Location;
        var rb_wf = currentScreen.WorkingArea.Location + currentScreen.WorkingArea.Size;

        var cursorPos = PhysicalToLogical(cursorPos_wf);
        var lt = PhysicalToLogical(lt_wf);
        var rb = PhysicalToLogical(rb_wf);

        double x = cursorPos.X + _bbox.Left;
        double y = cursorPos.Y + _bbox.Top;

        x = Math.Clamp(x, lt.X, rb.X - _bbox.Width);
        y = Math.Clamp(y, lt.Y, rb.Y - _bbox.Height);
        return new Point(x, y);
    }

    // ---------------- internal ------------------ //
    private void Timer_Tick(object? sender, EventArgs e)
    {
        var target = this.CalculateTargetPos();

        if (_stick.HasValue)  // JS stick formula
        {
            double cx = _stick.Value.X;
            double cy = _stick.Value.Y;
            double mx = target.X;
            double my = target.Y;

            target.X = cx - (cx - mx) * _stickDelta;
            target.Y = cy - (cy - my) * _stickDelta;
        }

        // Exponential approach factor (≈ 1 - e^{-dt/τ})
        var curElapsed = _stopwatch.Elapsed;
        var deltaT = (curElapsed - _lastElapsed).TotalSeconds;
        _lastElapsed = curElapsed;

        double lam = 1.0 - Math.Exp(-deltaT / _tau);

        // Basic smoothing
        _pos.X += (target.X - _pos.X) * lam;
        _pos.Y += (target.Y - _pos.Y) * lam;

        // Optional "expo-out" ease-out: pre-warp lam for a slow-start / fast-middle / slow-end curve
        if (_ease)
        {
            double prog = 1.0 - Math.Pow(2, -10 * lam);  // expo.out curve for small dt
            _pos.X = target.X + (_pos.X - target.X) * (1 - prog);
            _pos.Y = target.Y + (_pos.Y - target.Y) * (1 - prog);
        }

        _source.Left = _pos.X;
        _source.Top = _pos.Y;
    }
}