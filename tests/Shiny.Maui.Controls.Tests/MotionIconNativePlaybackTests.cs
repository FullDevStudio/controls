using Microsoft.Maui.Controls;
using Shiny.Controls.MotionIcons;
using Shiny.Maui.Controls.MotionIcons;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// Covers how <see cref="MotionIconView"/> hands a play to a platform compositor through
/// <see cref="NativeMotion"/>, and falls back to the managed ticker when it cannot.
/// </summary>
/// <remarks>
/// The Core Animation player itself needs a real Apple view and render server, so it is stood in
/// for here by <see cref="FakePlayer"/>; what is asserted is the view's side of the contract.
/// <see cref="NativeMotion.Factory"/> is process-wide, so every test puts it back.
/// </remarks>
[Collection(ApplicationResourcesCollection.Name)]
public class MotionIconNativePlaybackTests : IDisposable
{
    readonly List<FakePlayer> created = [];
    readonly List<NativeMotionRequest> requests = [];
    readonly Func<MotionIconView, NativeMotionRequest, INativeMotionPlayer?>? previous;

    public MotionIconNativePlaybackTests()
    {
        TestDispatcherProvider.Install();
        TestDispatcherProvider.Instance.Timers.Clear();

        this.previous = NativeMotion.Factory;
        NativeMotion.Factory = (_, request) =>
        {
            this.requests.Add(request);
            var player = new FakePlayer();
            this.created.Add(player);
            return player;
        };
    }

    public void Dispose() => NativeMotion.Factory = this.previous;

    [Fact]
    public void ALoopGoesToTheCompositorAndLeavesTheTickerIdle()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        view.OnLoadedInternal();

        this.created.Count.ShouldBe(1);
        double.IsPositiveInfinity(this.requests[0].Iterations).ShouldBeTrue();
        view.IsPlaying.ShouldBeTrue();
        MotionTicker.For(view)!.IsRunning.ShouldBeFalse();

        view.Stop();
    }

    [Fact]
    public void StoppingDisposesTheNativePlay()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        view.OnLoadedInternal();

        view.Stop();

        this.created[0].Disposed.ShouldBeTrue();
        view.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void StopAtCycleEndLetsTheCompositorFinishTheCycle()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        view.OnLoadedInternal();
        var completed = 0;
        view.Completed += (_, _) => completed++;

        view.StopAtCycleEnd();
        this.created[0].CycleFinishRequested.ShouldBeTrue();
        view.IsPlaying.ShouldBeTrue();

        this.created[0].RaiseFinished();

        view.IsPlaying.ShouldBeFalse();
        completed.ShouldBe(1);
        this.created[0].Disposed.ShouldBeTrue();
    }

    [Fact]
    public void AFinitePlayPassesItsRepeatCount()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "loader", Trigger = MotionTrigger.Manual, RepeatCount = 3 };
        view.OnLoadedInternal();

        view.Play();

        this.requests.Single().Iterations.ShouldBe(3d);

        view.Stop();
    }

    [Fact]
    public void HidingPausesTheNativePlayAndShowingResumesIt()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        view.OnLoadedInternal();

        view.IsVisible = false;
        this.created[0].Paused.ShouldBeTrue();
        view.IsPlaying.ShouldBeTrue();

        view.IsVisible = true;
        this.created[0].Paused.ShouldBeFalse();

        view.Stop();
    }

    [Fact]
    public void ANegativeSpeedStaysOnTheManagedTicker()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop, Speed = -1d };
        view.OnLoadedInternal();

        this.created.ShouldBeEmpty();
        MotionTicker.For(view)!.IsRunning.ShouldBeTrue();

        view.Stop();
    }

    [Fact]
    public void APlatformThatDeclinesLeavesThePlayOnTheTicker()
    {
        _ = new Application();
        NativeMotion.Factory = (_, _) => null;

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        view.OnLoadedInternal();

        view.IsPlaying.ShouldBeTrue();
        MotionTicker.For(view)!.IsRunning.ShouldBeTrue();

        view.Stop();
    }

    [Fact]
    public void ChangingTheColorRestartsTheNativeLoop()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        view.OnLoadedInternal();

        view.Color = Microsoft.Maui.Graphics.Colors.Red;

        this.created.Count.ShouldBe(2);
        this.created[0].Disposed.ShouldBeTrue();
        this.requests[1].Color.ShouldBe(Microsoft.Maui.Graphics.Colors.Red);
        view.IsPlaying.ShouldBeTrue();

        view.Stop();
    }

    sealed class FakePlayer : INativeMotionPlayer
    {
        public bool Disposed { get; private set; }
        public bool Paused { get; private set; }
        public bool CycleFinishRequested { get; private set; }

        public event EventHandler? Finished;

        public TimeSpan Position => TimeSpan.Zero;

        public void RaiseFinished() => this.Finished?.Invoke(this, EventArgs.Empty);

        public void Pause() => this.Paused = true;

        public void Resume() => this.Paused = false;

        public void FinishCycle() => this.CycleFinishRequested = true;

        public void Relayout()
        {
        }

        public void Dispose() => this.Disposed = true;
    }
}
