using System.Diagnostics;

namespace Shiny.Maui.Controls.Camera.Video;

/// <summary>
/// Thins a camera's frames down to a target rate, deciding from a timestamp alone so it can run in
/// <see cref="IFrameAnalyzer.WantsFrame"/>, before the platform has built anything.
/// </summary>
/// <remarks>
/// <para>
/// ⚠️ <b>A strict "at least one interval since the last frame" gate gets the rate wrong.</b> A camera's frames
/// jitter around their period, so at 30 fps asking for 15 the frame two periods on lands a hair early about
/// half the time, is refused, and the next one is taken a whole period late: 10 fps instead of 15. So the next
/// slot is scheduled from the ideal timeline (last slot plus one interval, not last frame plus one interval),
/// and a frame up to a quarter of an interval early still counts as on time.
/// </para>
/// <para>
/// After a stall (the camera paused, the app was backgrounded) the timeline restarts from now rather than
/// letting a backlog of missed slots pass every frame that follows.
/// </para>
/// <para>
/// Lock-free and allocation-free: one writer (the capture thread) and a rate that can be changed from any
/// thread.
/// </para>
/// </remarks>
sealed class FrameRateGate
{
    readonly long ticksPerSecond;
    long intervalTicks;
    long next = long.MinValue;

    /// <param name="framesPerSecond">The target rate; zero or less lets every frame through.</param>
    /// <param name="ticksPerSecond">
    /// The resolution of the timestamps <see cref="ShouldPass"/> is given: <see cref="Stopwatch.Frequency"/>, or a
    /// <see cref="TimeProvider.TimestampFrequency"/>.
    /// </param>
    public FrameRateGate(double framesPerSecond, long ticksPerSecond = 0)
    {
        this.ticksPerSecond = ticksPerSecond > 0 ? ticksPerSecond : Stopwatch.Frequency;
        this.SetRate(framesPerSecond);
    }

    /// <summary>Change the target rate. Takes effect from the next frame; the current slot is kept.</summary>
    public void SetRate(double framesPerSecond)
    {
        var ticks = framesPerSecond > 0 ? (long)(this.ticksPerSecond / framesPerSecond) : 0;
        Volatile.Write(ref this.intervalTicks, ticks);
    }

    /// <summary>Forget the timeline, so the next frame passes whenever it arrives.</summary>
    public void Reset() => Volatile.Write(ref this.next, long.MinValue);

    /// <summary>
    /// Whether a frame arriving at <paramref name="timestamp"/> (in the ticks the gate was created with) should be
    /// taken. Taking it books the next slot.
    /// </summary>
    public bool ShouldPass(long timestamp)
    {
        var interval = Volatile.Read(ref this.intervalTicks);
        if (interval <= 0)
            return true;

        var slot = Volatile.Read(ref this.next);
        if (slot != long.MinValue && timestamp < slot - interval / 4)
            return false;

        // on the timeline: book the slot after this one. Late by a whole interval or more (a stall): restart
        // the timeline from this frame, so a burst of overdue slots does not follow.
        var booked = slot == long.MinValue || timestamp - slot >= interval
            ? timestamp + interval
            : slot + interval;

        Volatile.Write(ref this.next, booked);
        return true;
    }
}
