using Microsoft.Maui.Dispatching;
using Shiny.Maui.Controls.Desktop.Notch;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Desktop.Tests;

/// <summary>
/// The parts of the notch window that Windows and Linux share: the frame-by-frame morph (neither can
/// animate a window resize natively, so a wrong step shows up as a notch that jumps or never lands),
/// and the outline Windows cuts its window region to.
/// </summary>
public class NotchWindowTests
{
    static readonly Func<double, double> Linear = t => t;

    [Fact]
    public void A_jump_applies_the_size_once_and_lands_there()
    {
        var applied = new List<Size>();
        using var frames = new NotchFrameAnimator(Linear, applied.Add);

        frames.Jump(new Size(120, 36));

        applied.ShouldBe([new Size(120, 36)]);
        frames.Current.ShouldBe(new Size(120, 36));
    }

    [Fact]
    public void A_morph_steps_through_the_eased_sizes_and_lands_exactly_on_the_target()
    {
        var applied = new List<Size>();
        var dispatcher = new ManualDispatcher();
        using var frames = new NotchFrameAnimator(Linear, applied.Add);
        frames.Jump(new Size(100, 40));
        applied.Clear();

        frames.AnimateTo(new Size(300, 140), 200, dispatcher);
        dispatcher.Timer!.IsRunning.ShouldBeTrue();

        frames.Tick(100);
        applied[^1].ShouldBe(new Size(200, 90));

        frames.Tick(250);
        applied[^1].ShouldBe(new Size(300, 140));
        frames.Current.ShouldBe(new Size(300, 140));
        dispatcher.Timer.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void Retargeting_mid_morph_starts_from_where_the_window_is_not_where_it_started()
    {
        var applied = new List<Size>();
        var dispatcher = new ManualDispatcher();
        using var frames = new NotchFrameAnimator(Linear, applied.Add);
        frames.Jump(new Size(100, 40));

        frames.AnimateTo(new Size(300, 140), 200, dispatcher);
        frames.Tick(100);

        frames.AnimateTo(new Size(100, 40), 200, dispatcher);
        frames.Tick(0);

        applied[^1].ShouldBe(new Size(200, 90));
    }

    [Fact]
    public void Without_a_dispatcher_or_a_duration_the_morph_snaps()
    {
        var applied = new List<Size>();
        using var frames = new NotchFrameAnimator(Linear, applied.Add);
        frames.Jump(new Size(100, 40));

        frames.AnimateTo(new Size(300, 140), 200, null);
        frames.Current.ShouldBe(new Size(300, 140));

        frames.AnimateTo(new Size(120, 36), 0, new ManualDispatcher());
        frames.Current.ShouldBe(new Size(120, 36));
    }

    [Fact]
    public void An_overshooting_ease_never_shrinks_the_window_to_nothing()
    {
        // The default ease dips past the target on the way in; on a shrink to a tiny size that would go
        // negative, and a zero-sized window is refused or hidden by some window managers.
        var size = NotchFrameAnimator.Interpolate(new Size(300, 140), new Size(4, 4), 1.2);

        size.Width.ShouldBe(1);
        size.Height.ShouldBe(1);
    }

    [Fact]
    public void The_default_ease_settles_on_the_target()
        => NotchView.DefaultEasing.Ease(1).ShouldBe(1, 1e-9);

    [Theory]
    [InlineData(120, 36, 18)]   // a pill while short
    [InlineData(320, 200, 28)]  // capped once it grows tall
    public void An_island_window_rounds_every_corner_like_the_surface(double width, double height, double radius)
        => DesktopNotchWindowService.CornersFor(new Size(width, height), -1, NotchShape.Island).ShouldBe(new CornerRadius(radius));

    [Fact]
    public void A_notch_shaped_window_keeps_its_top_corners_square()
        => DesktopNotchWindowService.CornersFor(new Size(320, 200), -1, NotchShape.Notch).ShouldBe(new CornerRadius(0, 0, 28, 28));

    [Fact]
    public void An_explicit_radius_is_still_clamped_to_half_the_height()
        => DesktopNotchWindowService.CornersFor(new Size(200, 20), 40, NotchShape.Island).ShouldBe(new CornerRadius(10));

    sealed class ManualDispatcher : IDispatcher
    {
        public ManualTimer? Timer { get; private set; }

        public bool IsDispatchRequired => false;

        public bool Dispatch(Action action)
        {
            action();
            return true;
        }

        public bool DispatchDelayed(TimeSpan delay, Action action) => this.Dispatch(action);

        public IDispatcherTimer CreateTimer() => this.Timer = new ManualTimer();
    }

    sealed class ManualTimer : IDispatcherTimer
    {
        public TimeSpan Interval { get; set; }

        public bool IsRepeating { get; set; }

        public bool IsRunning { get; private set; }

#pragma warning disable CS0067 // the tests drive NotchFrameAnimator.Tick directly
        public event EventHandler? Tick;
#pragma warning restore CS0067

        public void Start() => this.IsRunning = true;

        public void Stop() => this.IsRunning = false;
    }
}
