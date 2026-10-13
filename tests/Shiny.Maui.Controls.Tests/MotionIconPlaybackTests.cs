using Microsoft.Maui.Controls;
using Shiny.Controls.MotionIcons;
using Shiny.Maui.Controls.MotionIcons;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// Guards the rule that keeps <see cref="MotionIconView"/> safe to construct: nothing reaches for a
/// dispatcher until the view is loaded.
/// </summary>
/// <remarks>
/// <para>MAUI applies an implicit <c>Style</c> from inside <c>StyleableElement</c>'s constructor, so
/// a style setting <c>IsPlaying</c> lands <b>before the derived constructor body runs</b>. Starting
/// playback there asks for a dispatcher that may not exist yet, which is a deadlock in a headless
/// host and a hang while inflating a page in an app. Playback is therefore deferred to
/// <c>Loaded</c>, and these assert that it really is.</para>
/// <para>What is deliberately not covered here: playback itself. Everything the player does is
/// gated behind <c>Loaded</c>, which only a real handler raises — so the cycle counting, the
/// stop-at-cycle-end boundary and progress reporting are covered by driving the scene and timeline
/// directly in <see cref="MotionIconTests"/> rather than pretended at through a view that can never
/// load in this host.</para>
/// </remarks>
public class MotionIconPlaybackTests
{
    public MotionIconPlaybackTests()
    {
        TestDispatcherProvider.Install();
        TestDispatcherProvider.Instance.Timers.Clear();
    }

    [Fact]
    public void ConstructionNeverAsksForATimer()
    {
        _ = new Application();

        _ = new MotionIconView { Icon = "bell", Trigger = MotionTrigger.Loop };

        // The ticker is the only thing that creates one, and it must not be reached during
        // construction — an unparented view is not rendering anyway.
        TestDispatcherProvider.Instance.Timers.ShouldBeEmpty();
    }

    [Fact]
    public void SettingIsPlayingBeforeLoadRemembersItWithoutStarting()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "bell", IsPlaying = true };

        TestDispatcherProvider.Instance.Timers.ShouldBeEmpty();

        // The intent survives: the view plays when it loads, rather than dropping the request.
        view.IsPlaying.ShouldBeTrue();
    }

    [Fact]
    public void AnImplicitStyleSettingEveryPropertyDoesNotStartPlayback()
    {
        // The exact shape ImplicitStyleConstructionTests builds, and the one that used to hang:
        // every library-declared property set to a non-default from inside the base constructor.
        var app = new Application();
        var style = new Style(typeof(MotionIconView));

        style.Setters.Add(new Setter { Property = MotionIconView.IsPlayingProperty, Value = true });
        style.Setters.Add(new Setter { Property = MotionIconView.IconProperty, Value = "probe" });
        style.Setters.Add(new Setter { Property = MotionIconView.TriggerProperty, Value = MotionTrigger.Loop });
        style.Setters.Add(new Setter { Property = MotionIconView.RepeatCountProperty, Value = 8 });
        style.Setters.Add(new Setter { Property = MotionIconView.DurationProperty, Value = TimeSpan.FromSeconds(7) });
        style.Setters.Add(new Setter { Property = MotionIconView.IntervalProperty, Value = TimeSpan.FromSeconds(7) });

        app.Resources = new ResourceDictionary { style };

        Should.NotThrow(() => new MotionIconView());
        TestDispatcherProvider.Instance.Timers.ShouldBeEmpty();
    }

    [Fact]
    public void StoppingAnUnloadedViewIsHarmless()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "bell", IsPlaying = true };

        Should.NotThrow(view.Stop);
        Should.NotThrow(view.StopAtCycleEnd);
        Should.NotThrow(view.Reset);

        view.IsPlaying.ShouldBeFalse();
        TestDispatcherProvider.Instance.Timers.ShouldBeEmpty();
    }

    [Fact]
    public void UnknownArtworkStillDoesNotStartAnything()
    {
        _ = new Application();

        _ = new MotionIconView { Icon = "definitely-not-an-icon", Trigger = MotionTrigger.Loop, IsPlaying = true };

        TestDispatcherProvider.Instance.Timers.ShouldBeEmpty();
    }

    // The ticker's timer runs only while something is subscribed to it, so IsRunning is exactly
    // "is any icon on this dispatcher still consuming frames". Nothing else in the suite loads an
    // icon, and each test stops its own before returning so the next starts from an idle ticker.

    [Fact]
    public void AHiddenLoopingIconDoesNotConsumeFrames()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop, IsVisible = false };
        view.OnLoadedInternal();

        // Still "playing" as far as bindings are concerned — it is paused, not stopped.
        view.IsPlaying.ShouldBeTrue();
        MotionTicker.For(view)!.IsRunning.ShouldBeFalse();

        view.Stop();
    }

    [Fact]
    public void HidingALoopingIconPausesItAndShowingItResumes()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        view.OnLoadedInternal();

        var ticker = MotionTicker.For(view)!;
        ticker.IsRunning.ShouldBeTrue();

        view.IsVisible = false;
        ticker.IsRunning.ShouldBeFalse();
        view.IsPlaying.ShouldBeTrue();

        view.IsVisible = true;
        ticker.IsRunning.ShouldBeTrue();

        view.Stop();
        ticker.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void StoppingAHiddenIconLeavesItStoppedWhenShown()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop, IsVisible = false };
        view.OnLoadedInternal();
        view.Stop();

        view.IsVisible = true;

        view.IsPlaying.ShouldBeFalse();
        MotionTicker.For(view)!.IsRunning.ShouldBeFalse();
    }
}
