using System.ComponentModel;
using System.Reflection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shiny.Maui.Controls.Themes;
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
[Collection(ApplicationResourcesCollection.Name)]
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

    // A closed Expander hides its content without touching the icons' own IsVisible, so it pauses
    // them itself when it closes and resumes them when it opens. Each test stops its icons in a
    // finally: a failed assertion must not leave the ticker running into the next.

    [Fact]
    public void ClosingAnExpanderPausesALoopingIconInItsContentAndOpeningResumesIt()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        var expander = new Expander { IsExpanded = true, Content = new VerticalStackLayout { Children = { view } } };

        try
        {
            view.OnLoadedInternal();
            var ticker = MotionTicker.For(view)!;
            ticker.IsRunning.ShouldBeTrue();

            expander.Collapse();
            ticker.IsRunning.ShouldBeFalse();
            view.IsPlaying.ShouldBeTrue();

            expander.Expand();
            ticker.IsRunning.ShouldBeTrue();
        }
        finally
        {
            view.Stop();
        }

        MotionTicker.For(view)!.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void ALoopingIconLoadedInAClosedExpanderDoesNotConsumeFrames()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        _ = new Expander { Content = view };

        try
        {
            view.OnLoadedInternal();

            view.IsPlaying.ShouldBeTrue();
            MotionTicker.For(view)!.IsRunning.ShouldBeFalse();
        }
        finally
        {
            view.Stop();
        }
    }

    [Fact]
    public void AnIconInTheHeaderKeepsPlayingWhileTheExpanderIsClosed()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        var expander = new Expander { IsExpanded = true, Header = view, Content = new Label() };

        try
        {
            view.OnLoadedInternal();

            expander.Collapse();
            MotionTicker.For(view)!.IsRunning.ShouldBeTrue();
        }
        finally
        {
            view.Stop();
        }
    }

    [Fact]
    public void OpeningAnOuterExpanderLeavesAnIconInAClosedInnerOnePaused()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        var inner = new Expander { Content = view };
        var outer = new Expander { Content = inner };

        try
        {
            view.OnLoadedInternal();
            var ticker = MotionTicker.For(view)!;

            outer.Expand();
            ticker.IsRunning.ShouldBeFalse();

            inner.Expand();
            ticker.IsRunning.ShouldBeTrue();

            // The outer one closing hides the inner one's content too.
            outer.Collapse();
            ticker.IsRunning.ShouldBeFalse();
        }
        finally
        {
            view.Stop();
        }
    }

    [Fact]
    public void AnIconMovedIntoAClosedExpanderPausesAndMovedOutResumes()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        var row = new HorizontalStackLayout { Children = { view } };
        var open = new VerticalStackLayout { Children = { row } };
        var content = new VerticalStackLayout();
        _ = new ContentPage { Content = new VerticalStackLayout { Children = { open, new Expander { Content = content } } } };

        try
        {
            view.OnLoadedInternal();
            var ticker = MotionTicker.For(view)!;
            ticker.IsRunning.ShouldBeTrue();

            // The row moves, not the icon: the icon has to be found below whatever was added.
            open.Children.Remove(row);
            content.Children.Add(row);
            ticker.IsRunning.ShouldBeFalse();

            content.Children.Remove(row);
            open.Children.Add(row);
            ticker.IsRunning.ShouldBeTrue();
        }
        finally
        {
            view.Stop();
        }
    }

    // Color: the theme ink is a fallback, never a value that outranks the app's own. A dynamic
    // resource on Color itself beat every Style setter, Binding and AppThemeBinding (#25).

    static MotionIconView Themed(MotionIconView view)
    {
        var page = new ContentPage { Content = view };
        page.Resources[ShinyThemeKeys.Color.OnSurface] = Colors.Green;
        return view;
    }

    [Fact]
    public void AnUnsetColorFollowsTheTheme()
    {
        _ = new Application();

        var view = Themed(new MotionIconView { Icon = "heart" });

        view.Color.ShouldBeNull();
        view.ResolveColor().ShouldBe(Colors.Green);
    }

    [Fact]
    public void AStyleColorBeatsTheTheme()
    {
        _ = new Application();

        var style = new Style(typeof(MotionIconView));
        style.Setters.Add(new Setter { Property = MotionIconView.ColorProperty, Value = Colors.Red });

        var view = Themed(new MotionIconView { Icon = "heart", Style = style });

        view.Color.ShouldBe(Colors.Red);
        view.ResolveColor().ShouldBe(Colors.Red);
    }

    [Fact]
    public void ABoundColorBeatsTheTheme()
    {
        _ = new Application();

        var view = Themed(new MotionIconView { Icon = "heart" });
        view.BindingContext = new { Ink = Colors.Red };
        view.SetBinding(MotionIconView.ColorProperty, "Ink");

        view.ResolveColor().ShouldBe(Colors.Red);
    }

    [Fact]
    public void AnAppThemeColorBeatsTheTheme()
    {
        _ = new Application();

        var view = Themed(new MotionIconView { Icon = "heart" });
        view.SetAppThemeColor(MotionIconView.ColorProperty, Colors.Red, Colors.Red);

        view.ResolveColor().ShouldBe(Colors.Red);
    }

    [Fact]
    public void ClearingTheColorFallsBackToTheTheme()
    {
        _ = new Application();

        var view = Themed(new MotionIconView { Icon = "heart", Color = Colors.Red });
        view.ClearValue(MotionIconView.ColorProperty);

        view.ResolveColor().ShouldBe(Colors.Green);
    }

    // Trigger: Loop and Appear used to be read only in Loaded (#25).

    [Fact]
    public void SwitchingToLoopAfterLoadStartsTheLoop()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Manual };
        view.OnLoadedInternal();
        view.IsPlaying.ShouldBeFalse();

        view.Trigger = MotionTrigger.Loop;

        view.IsPlaying.ShouldBeTrue();
        MotionTicker.For(view)!.IsRunning.ShouldBeTrue();

        view.Stop();
    }

    [Fact]
    public void SwitchingAwayFromLoopStopsIt()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        view.OnLoadedInternal();
        view.IsPlaying.ShouldBeTrue();

        view.Trigger = MotionTrigger.Manual;

        view.IsPlaying.ShouldBeFalse();
        MotionTicker.For(view)!.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void ALoopingIconKeepsLoopingWhenItsColorChanges()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "refresh", Trigger = MotionTrigger.Loop };
        view.OnLoadedInternal();

        view.Color = Colors.Red;

        view.IsPlaying.ShouldBeTrue();
        MotionTicker.For(view)!.IsRunning.ShouldBeTrue();

        view.Stop();
    }

    // IsPlaying is a busy flag: it loops until cleared. RepeatCount bounds Play(), not the flag
    // (it used to stop after RepeatCount cycles and drop IsPlaying back to false).

    [Fact]
    public void SettingIsPlayingLoopsRegardlessOfRepeatCount()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "loader", Trigger = MotionTrigger.Manual, RepeatCount = 1 };
        view.OnLoadedInternal();

        view.IsPlaying = true;

        double.IsPositiveInfinity(view.PlayIterations).ShouldBeTrue();
        view.IsPlaying.ShouldBeTrue();

        view.IsPlaying = false;
        MotionTicker.For(view)!.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void IsPlayingSetBeforeLoadLoopsOnceLoaded()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "loader", Trigger = MotionTrigger.Manual, RepeatCount = 2, IsPlaying = true };
        view.OnLoadedInternal();

        double.IsPositiveInfinity(view.PlayIterations).ShouldBeTrue();

        view.Stop();
    }

    [Fact]
    public void PlayStillRunsRepeatCountCycles()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "loader", Trigger = MotionTrigger.Manual, RepeatCount = 2 };
        view.OnLoadedInternal();

        view.Play();

        view.PlayIterations.ShouldBe(2d);
        view.IsPlaying.ShouldBeTrue();

        view.Stop();
    }

    // Unloaded mid-play. IsPlaying used to stay true across the gap, and Loaded reads a bare IsPlaying
    // as the app's busy flag, so a finite play came back looping forever — what a tab bar's icons
    // did, because the bar is moved onto the next page right after it plays the icon. Now only the
    // app's IsPlaying survives an unload; a play the view started itself ends there, at rest.

    [Fact]
    public void AFinitePlayCutOffByAnUnloadEndsAtRestAndIsNotReplayed()
    {
        _ = new Application();

        var view = new MotionIconView
        {
            Icon = "loader",
            Trigger = MotionTrigger.Manual,
            RepeatCount = 1,
            Duration = TimeSpan.FromSeconds(1)
        };
        try
        {
            view.OnLoadedInternal();
            var play = view.PlayAsync();

            var timer = TimerOf(view);
            Frame(timer);
            view.Progress.ShouldBeGreaterThan(0d);

            view.OnUnloadedInternal();

            view.IsPlaying.ShouldBeFalse();
            view.Progress.ShouldBe(0d);
            play.IsCompleted.ShouldBeTrue();

            view.OnLoadedInternal();

            view.IsPlaying.ShouldBeFalse();
            MotionTicker.For(view)!.IsRunning.ShouldBeFalse();

            var writes = 0;
            view.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == MotionIconView.ProgressProperty.PropertyName)
                    writes++;
            };

            Frame(timer);

            writes.ShouldBe(0);
        }
        finally
        {
            // a play left looping would keep the shared ticker running into the next test
            view.Stop();
        }
    }

    [Fact]
    public void AnAppearPlayCutOffByAnUnloadEndsAndAppearsAgainOnTheNextLoad()
    {
        _ = new Application();

        var view = new MotionIconView
        {
            Icon = "bell",
            Trigger = MotionTrigger.Appear,
            RepeatCount = 1,
            Duration = TimeSpan.FromMilliseconds(20)
        };
        try
        {
            view.OnLoadedInternal();
            view.IsPlaying.ShouldBeTrue();

            view.OnUnloadedInternal();
            view.IsPlaying.ShouldBeFalse();

            // Appear plays on every load, as it does after a play that ran to its end — and it
            // is still the RepeatCount play, not a loop.
            view.OnLoadedInternal();
            view.PlayIterations.ShouldBe(1d);

            Frame(TimerOf(view));

            view.IsPlaying.ShouldBeFalse();
        }
        finally
        {
            view.Stop();
        }
    }

    [Fact]
    public void ClearingIsPlayingWhileUnloadedDropsTheAppsLoop()
    {
        _ = new Application();

        var view = new MotionIconView { Icon = "loader", Trigger = MotionTrigger.Manual, RepeatCount = 1 };
        view.OnLoadedInternal();
        view.IsPlaying = true;

        view.OnUnloadedInternal();
        view.IsPlaying = false;
        view.OnLoadedInternal();

        view.IsPlaying.ShouldBeFalse();
        MotionTicker.For(view)!.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public void SettingIsPlayingDuringAFinitePlayHandsItToTheApp()
    {
        _ = new Application();

        var view = new MotionIconView
        {
            Icon = "loader",
            Trigger = MotionTrigger.Manual,
            RepeatCount = 1,
            Duration = TimeSpan.FromMilliseconds(20)
        };
        try
        {
            view.OnLoadedInternal();
            view.Play();

            // Already true, so no property change: the write itself has to count.
            view.IsPlaying = true;

            double.IsPositiveInfinity(view.PlayIterations).ShouldBeTrue();

            Frame(TimerOf(view));
            view.IsPlaying.ShouldBeTrue();

            view.OnUnloadedInternal();
            view.IsPlaying.ShouldBeTrue();

            view.OnLoadedInternal();
            double.IsPositiveInfinity(view.PlayIterations).ShouldBeTrue();
        }
        finally
        {
            view.Stop();
        }
    }

    [Fact]
    public void ABindingClearingIsPlayingStopsAPlayTheViewStarted()
    {
        _ = new Application();

        var busy = new BusyFlag();
        var view = new MotionIconView
        {
            Icon = "loader",
            Trigger = MotionTrigger.Manual,
            RepeatCount = 1,
            Duration = TimeSpan.FromSeconds(1),
            BindingContext = busy
        };
        view.SetBinding(MotionIconView.IsPlayingProperty, nameof(BusyFlag.IsBusy));

        try
        {
            view.OnLoadedInternal();
            view.Play();

            // IsPlaying binds two-way by default, so the view's own play shows on the source.
            busy.IsBusy.ShouldBeTrue();

            busy.IsBusy = false;

            view.IsPlaying.ShouldBeFalse();
            view.Progress.ShouldBe(0d);
            MotionTicker.For(view)!.IsRunning.ShouldBeFalse();
        }
        finally
        {
            view.Stop();
        }
    }

    sealed class BusyFlag : INotifyPropertyChanged
    {
        bool isBusy;

        public event PropertyChangedEventHandler? PropertyChanged;

        public bool IsBusy
        {
            get => this.isBusy;
            set
            {
                if (this.isBusy == value)
                    return;

                this.isBusy = value;
                this.PropertyChanged?.Invoke(this, new(nameof(this.IsBusy)));
            }
        }
    }

    [Fact]
    public void IsPlayingKeepsLoopingAcrossAReload()
    {
        _ = new Application();

        var view = new MotionIconView
        {
            Icon = "loader",
            Trigger = MotionTrigger.Manual,
            RepeatCount = 1,
            Duration = TimeSpan.FromMilliseconds(20)
        };
        view.OnLoadedInternal();
        view.IsPlaying = true;

        view.OnUnloadedInternal();
        view.IsPlaying.ShouldBeTrue();

        view.OnLoadedInternal();

        double.IsPositiveInfinity(view.PlayIterations).ShouldBeTrue();

        Frame(TimerOf(view));

        view.IsPlaying.ShouldBeTrue();

        view.IsPlaying = false;
    }

    [Fact]
    public void ALoopTriggerKeepsLoopingAcrossAReload()
    {
        _ = new Application();

        var view = new MotionIconView
        {
            Icon = "refresh",
            Trigger = MotionTrigger.Loop,
            RepeatCount = 1,
            Duration = TimeSpan.FromMilliseconds(20)
        };
        view.OnLoadedInternal();

        view.OnUnloadedInternal();
        view.OnLoadedInternal();

        double.IsPositiveInfinity(view.PlayIterations).ShouldBeTrue();

        Frame(TimerOf(view));

        view.IsPlaying.ShouldBeTrue();

        view.Stop();
    }

    // The ticker is shared per dispatcher and created once per process, so the constructor's
    // Timers.Clear() has usually dropped it from the list by the time a test runs.
    internal static TestDispatcher.ControllableTimer TimerOf(MotionIconView view)
    {
        var ticker = MotionTicker.For(view)!;
        var field = typeof(MotionTicker).GetField("timer", BindingFlags.Instance | BindingFlags.NonPublic)!;

        return (TestDispatcher.ControllableTimer)field.GetValue(ticker)!;
    }

    // The ticker advances by real elapsed time (clamped to 250 ms), so one frame after a short wait
    // carries a 20 ms cycle past its end.
    internal static void Frame(TestDispatcher.ControllableTimer timer)
    {
        Thread.Sleep(60);
        timer.Fire();
    }
}
