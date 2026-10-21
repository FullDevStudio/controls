#if !ANDROID && !IOS && !MACCATALYST
using System.Diagnostics;

namespace Shiny.Maui.Controls.Desktop.Notch;

/// <summary>
/// Steps the notch window from one size to another, one resize per frame, for the hosts whose window
/// cannot animate its own frame (Windows, Linux). macOS hands the whole morph to AppKit instead.
/// </summary>
/// <remarks>
/// The window <em>is</em> the notch there — neither host can give a MAUI window a transparent
/// background reliably, so the window cannot be left large with the surface morphing inside it.
/// Resizing the window on every frame is what makes the surface, which fills it, follow.
/// </remarks>
sealed class NotchFrameAnimator : IDisposable
{
    const double FrameMs = 16;

    readonly Func<double, double> easing;
    readonly Action<Size> apply;
    readonly Stopwatch clock = new();

    IDispatcherTimer? timer;
    Size from;
    Size to;
    double durationMs;

    public NotchFrameAnimator(Func<double, double> easing, Action<Size> apply)
    {
        this.easing = easing;
        this.apply = apply;
    }

    /// <summary>The size last applied: the window's size, mid-morph or at rest.</summary>
    public Size Current { get; private set; }

    public bool IsRunning => this.timer?.IsRunning == true;

    /// <summary>Goes straight to <paramref name="size"/>, cancelling any morph in flight.</summary>
    public void Jump(Size size)
    {
        this.Stop();
        this.Current = size;
        this.apply(size);
    }

    /// <summary>
    /// Morphs from wherever the window is now — mid-morph included, so a retarget never jumps back to
    /// where the last morph started — to <paramref name="target"/>.
    /// </summary>
    public void AnimateTo(Size target, double duration, IDispatcher? dispatcher)
    {
        if (duration <= 0 || dispatcher == null || this.Current.Width <= 0 || this.Current.Height <= 0)
        {
            this.Jump(target);
            return;
        }

        this.from = this.Current;
        this.to = target;
        this.durationMs = duration;

        if (this.timer == null)
        {
            this.timer = dispatcher.CreateTimer();
            this.timer.Interval = TimeSpan.FromMilliseconds(FrameMs);
            this.timer.IsRepeating = true;
            this.timer.Tick += (_, _) => this.Tick(this.clock.Elapsed.TotalMilliseconds);
        }

        this.clock.Restart();
        if (!this.timer.IsRunning)
            this.timer.Start();
    }

    public void Stop()
    {
        this.timer?.Stop();
        this.clock.Reset();
    }

    /// <summary>Applies the frame for <paramref name="elapsedMs"/> into the morph. The timer's tick; exposed for tests.</summary>
    internal void Tick(double elapsedMs)
    {
        var t = this.durationMs <= 0 ? 1 : Math.Clamp(elapsedMs / this.durationMs, 0, 1);
        var frame = t >= 1 ? this.to : Interpolate(this.from, this.to, this.easing(t));
        if (t >= 1)
            this.Stop();

        this.Current = frame;
        this.apply(frame);
    }

    /// <summary>
    /// The size at eased progress <paramref name="progress"/>. The default ease overshoots a few percent,
    /// which on a shrink would dip below the target — never below a pixel, though: a zero-sized window
    /// is refused by some window managers and hides itself on others.
    /// </summary>
    internal static Size Interpolate(Size from, Size to, double progress) => new(
        Math.Max(1, from.Width + ((to.Width - from.Width) * progress)),
        Math.Max(1, from.Height + ((to.Height - from.Height) * progress))
    );

    public void Dispose() => this.Stop();
}
#endif
