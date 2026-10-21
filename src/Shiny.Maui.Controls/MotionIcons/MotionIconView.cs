using System.Windows.Input;
using Shiny.Controls.Keyframe;
using Shiny.Controls.Keyframe.Graphics;
using Shiny.Controls.MotionIcons;
using Shiny.Maui.Controls.MotionIcons;
using Shiny.Maui.Controls.Themes;

// The control itself sits in the root namespace alongside the other top-level controls; only its
// internals live under MotionIcons, so nobody has to add a using to place one on a page.
namespace Shiny.Maui.Controls;

/// <summary>
/// An icon that animates — on a loop, on hover, on tap, when it appears, or on command.
/// </summary>
/// <remarks>
/// <para>Drawn rather than composed from views: the whole icon is one <c>GraphicsView</c> hosting a
/// <see cref="KeyframeScene"/>, so a forty-icon screen costs forty layer trees and one timer instead
/// of a few hundred nested layouts fighting over layout passes. Nothing here touches a platform SDK,
/// so the icons work on every head the library supports — including AppKit and GTK4.</para>
/// <para>Playback is the keyframe engine's <see cref="Player"/>, which is where position, rate,
/// baselines and clock subscription already live. The artwork and the motion come from
/// <c>Shiny.Controls.MotionIcons</c>, which the Blazor <c>MotionIcon</c> component compiles into CSS
/// from the very same definitions — so an icon picked here and an icon picked there are the same
/// drawing running the same curves.</para>
/// </remarks>
public class MotionIconView : GraphicsView
{
    /// <summary>Backing store for <see cref="Icon"/>.</summary>
    public static readonly BindableProperty IconProperty =
        BindableProperty.Create(nameof(Icon), typeof(string), typeof(MotionIconView),
            propertyChanged: OnArtworkChanged);

    /// <summary>Backing store for <see cref="Source"/>.</summary>
    public static readonly BindableProperty SourceProperty =
        BindableProperty.Create(nameof(Source), typeof(MotionIconDefinition), typeof(MotionIconView),
            propertyChanged: OnArtworkChanged);

    /// <summary>Backing store for <see cref="PathData"/>.</summary>
    public static readonly BindableProperty PathDataProperty =
        BindableProperty.Create(nameof(PathData), typeof(string), typeof(MotionIconView),
            propertyChanged: OnArtworkChanged);

    /// <summary>Backing store for <see cref="Motion"/>.</summary>
    public static readonly BindableProperty MotionProperty =
        BindableProperty.Create(nameof(Motion), typeof(MotionPreset), typeof(MotionIconView),
            MotionPreset.Default, propertyChanged: OnArtworkChanged);

    /// <summary>Backing store for <see cref="Trigger"/>.</summary>
    public static readonly BindableProperty TriggerProperty =
        BindableProperty.Create(nameof(Trigger), typeof(MotionTrigger), typeof(MotionIconView),
            MotionTrigger.Hover | MotionTrigger.Press, propertyChanged: OnTriggerChanged);

    /// <summary>Backing store for <see cref="Duration"/>.</summary>
    public static readonly BindableProperty DurationProperty =
        BindableProperty.Create(nameof(Duration), typeof(TimeSpan), typeof(MotionIconView),
            TimeSpan.Zero, propertyChanged: OnArtworkChanged);

    /// <summary>Backing store for <see cref="Interval"/>.</summary>
    public static readonly BindableProperty IntervalProperty =
        BindableProperty.Create(nameof(Interval), typeof(TimeSpan), typeof(MotionIconView),
            TimeSpan.Zero, propertyChanged: OnArtworkChanged);

    /// <summary>Backing store for <see cref="Speed"/>.</summary>
    public static readonly BindableProperty SpeedProperty =
        BindableProperty.Create(nameof(Speed), typeof(double), typeof(MotionIconView), 1d,
            propertyChanged: OnSpeedChanged);

    /// <summary>Backing store for <see cref="RepeatCount"/>.</summary>
    public static readonly BindableProperty RepeatCountProperty =
        BindableProperty.Create(nameof(RepeatCount), typeof(int), typeof(MotionIconView), 1);

    /// <summary>Backing store for <see cref="Color"/>.</summary>
    public static readonly BindableProperty ColorProperty =
        BindableProperty.Create(nameof(Color), typeof(Color), typeof(MotionIconView),
            propertyChanged: OnArtworkChanged);

    // The theme's ink, used while Color is unset. A property of its own, fed by a dynamic resource,
    // because a dynamic resource on Color itself outranks a Style setter, a Binding and an
    // AppThemeBinding — so none of them could ever colour the icon.
    static readonly BindableProperty ThemeColorProperty =
        BindableProperty.Create("ThemeColor", typeof(Color), typeof(MotionIconView),
            propertyChanged: OnThemeColorChanged);

    /// <summary>Backing store for <see cref="AccentColor"/>.</summary>
    public static readonly BindableProperty AccentColorProperty =
        BindableProperty.Create(nameof(AccentColor), typeof(Color), typeof(MotionIconView),
            propertyChanged: OnArtworkChanged);

    /// <summary>Backing store for <see cref="StrokeWidth"/>.</summary>
    public static readonly BindableProperty StrokeWidthProperty =
        BindableProperty.Create(nameof(StrokeWidth), typeof(double), typeof(MotionIconView), 2d,
            propertyChanged: OnArtworkChanged);

    /// <summary>Backing store for <see cref="IsPlaying"/>.</summary>
    public static readonly BindableProperty IsPlayingProperty =
        BindableProperty.Create(nameof(IsPlaying), typeof(bool), typeof(MotionIconView), false,
            BindingMode.TwoWay, propertyChanged: OnIsPlayingChanged, coerceValue: OnIsPlayingWritten);

    /// <summary>Backing store for <see cref="Progress"/>.</summary>
    public static readonly BindableProperty ProgressProperty =
        BindableProperty.Create(nameof(Progress), typeof(double), typeof(MotionIconView), 0d,
            BindingMode.TwoWay, propertyChanged: OnProgressChanged);

    /// <summary>Backing store for <see cref="Command"/>.</summary>
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(MotionIconView));

    /// <summary>Backing store for <see cref="CommandParameter"/>.</summary>
    public static readonly BindableProperty CommandParameterProperty =
        BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(MotionIconView));

    MotionIconDefinition? icon;
    MotionSpec? spec;
    KeyframeScene? scene;
    Timeline? timeline;
    Player? player;
    INativeMotionPlayer? native;
    MotionTicker? ticker;
    TaskCompletionSource? completion;

    bool loaded;
    bool ticking;
    bool pendingAutoPlay;

    // True while the current (or pending) play is the app's: IsPlaying set from outside — a local
    // value, a Binding, a Style. False when the view started it itself (Play, Loop, Press, Hover,
    // Appear, the Loop trigger). IsPlaying alone cannot tell the two apart, and only the app's loop
    // may survive the view leaving the tree.
    bool playRequested;
    bool stopAtCycleEnd;
    bool suppressPlaybackFeedback;
    bool suppressProgressFeedback;

    /// <summary>Creates the view.</summary>
    public MotionIconView()
    {
        WidthRequest = 24d;
        HeightRequest = 24d;

        // Follows the theme pack rather than a hardcoded pair, so an icon sits correctly on whatever
        // surface the app is using — until Color is given a value by any means (local, Style,
        // Binding, AppThemeBinding), which wins.
        this.SetDynamicResource(ThemeColorProperty, ShinyThemeKeys.Color.OnSurface);

        Loaded += (_, _) => OnLoadedInternal();
        Unloaded += (_, _) => OnUnloadedInternal();
        SizeChanged += (_, _) => native?.Relayout();

        ApplyTriggerGestures();
    }

    /// <summary>Raised when a play finishes.</summary>
    public event EventHandler? Completed;

    /// <summary>The name of a built-in icon, looked up in <see cref="MotionIconLibrary"/>.</summary>
    public string? Icon
    {
        get => (string?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Explicit artwork. Takes precedence over <see cref="Icon"/> and <see cref="PathData"/>.</summary>
    public MotionIconDefinition? Source
    {
        get => (MotionIconDefinition?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Raw SVG path data, drawn in a 24x24 box. The quickest way to animate your own glyph.</summary>
    public string? PathData
    {
        get => (string?)GetValue(PathDataProperty);
        set => SetValue(PathDataProperty, value);
    }

    /// <summary>Which motion to play. Defaults to whatever was authored for the icon.</summary>
    public MotionPreset Motion
    {
        get => (MotionPreset)GetValue(MotionProperty);
        set => SetValue(MotionProperty, value);
    }

    /// <summary>What starts the animation.</summary>
    public MotionTrigger Trigger
    {
        get => (MotionTrigger)GetValue(TriggerProperty);
        set => SetValue(TriggerProperty, value);
    }

    /// <summary>Overrides the length of one cycle. Zero uses the motion's own duration.</summary>
    public TimeSpan Duration
    {
        get => (TimeSpan)GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>How long the icon rests between cycles while looping.</summary>
    public TimeSpan Interval
    {
        get => (TimeSpan)GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    /// <summary>Playback rate. Negative values run the animation backwards.</summary>
    public double Speed
    {
        get => (double)GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    /// <summary>
    /// How many cycles a triggered play (<see cref="Play"/>, Press, Appear) runs for. Zero or less repeats
    /// forever. Setting <see cref="IsPlaying"/> to true is not bounded by it.
    /// </summary>
    public int RepeatCount
    {
        get => (int)GetValue(RepeatCountProperty);
        set => SetValue(RepeatCountProperty, value);
    }

    /// <summary>The primary icon colour. Unset follows the theme's on-surface colour.</summary>
    public Color? Color
    {
        get => (Color?)GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    /// <summary>The secondary colour, used by two-tone artwork. Falls back to <see cref="Color"/>.</summary>
    public Color? AccentColor
    {
        get => (Color?)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    /// <summary>Stroke width in the icon's own 24-unit space. The set is drawn for 2.</summary>
    public double StrokeWidth
    {
        get => (double)GetValue(StrokeWidthProperty);
        set => SetValue(StrokeWidthProperty, value);
    }

    /// <summary>
    /// Whether the icon is animating. Settable: true loops (ignoring <see cref="RepeatCount"/>) until it
    /// is set false, which stops and returns to rest — the property to bind to a busy flag. A finite
    /// <see cref="Play"/> also raises it, and drops it again when its cycles run out. Only a true set by
    /// the app survives the view being unloaded and loaded again; a play the view started itself
    /// (<see cref="Play"/>, <see cref="Loop"/>, Press, Hover, Appear) ends at rest on unload.
    /// </summary>
    public bool IsPlaying
    {
        get => (bool)GetValue(IsPlayingProperty);
        set => SetValue(IsPlayingProperty, value);
    }

    /// <summary>
    /// Position within the current cycle, 0 to 1. Reports while playing and seeks when written,
    /// so a slider can scrub the icon.
    /// </summary>
    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    /// <summary>Invoked when the icon is tapped.</summary>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Passed to <see cref="Command"/>.</summary>
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>Plays the animation for <see cref="RepeatCount"/> cycles.</summary>
    public void Play() => Start(RepeatCount > 0 ? RepeatCount : double.PositiveInfinity);

    /// <summary>Plays the animation continuously until something stops it.</summary>
    public void Loop() => Start(double.PositiveInfinity);

    /// <summary>Plays until the animation finishes.</summary>
    /// <remarks>Never completes for a looping play — await this only after a finite <see cref="RepeatCount"/>.</remarks>
    public Task PlayAsync()
    {
        completion?.TrySetResult();
        completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        Play();
        return completion.Task;
    }

    /// <summary>Stops immediately and returns the icon to its resting pose.</summary>
    public void Stop()
    {
        stopAtCycleEnd = false;
        pendingAutoPlay = false;
        playRequested = false;

        // Restores the baselines captured when this play began, which is what puts every layer back
        // to the artwork as drawn.
        player?.Stop();
        DetachPlayer();

        WriteProgress(0d);
        SetPlaybackFlag(false);
        Invalidate();

        completion?.TrySetResult();
        completion = null;
    }

    /// <summary>
    /// Lets the cycle in progress finish, then stops. This is what a pointer leaving the icon does,
    /// so a half-swung bell settles rather than snapping upright.
    /// </summary>
    public void StopAtCycleEnd()
    {
        if (player is not null)
            stopAtCycleEnd = true;
        else
            native?.FinishCycle();
    }

    /// <summary>Returns to the resting pose without changing whether it is playing.</summary>
    public void Reset()
    {
        timeline?.RestoreBaselines();
        WriteProgress(0d);
        Invalidate();
    }

    // Internal so tests can stand in for Loaded, which only a real handler raises.
    internal void OnLoadedInternal()
    {
        loaded = true;
        Rebuild();

        // IsPlaying can only still be true here when the app set it: a play the view started itself
        // ends at rest on Unloaded, so a bare IsPlaying is the busy flag and resumes looping.
        if (Trigger.HasFlag(MotionTrigger.Loop))
            Start(double.PositiveInfinity);
        else if (pendingAutoPlay)
            Start(pendingIterations, playRequested);
        else if (IsPlaying)
            Start(double.PositiveInfinity, requested: true);
        else if (Trigger.HasFlag(MotionTrigger.Appear))
            Start(RepeatCount);

        pendingAutoPlay = false;
    }

    double PlaybackIterations() => RepeatCount > 0 ? RepeatCount : double.PositiveInfinity;

    double pendingIterations = double.PositiveInfinity;

    /// <summary>Cycles the current (or last) play was started for; infinite while looping. Test seam.</summary>
    internal double PlayIterations { get; private set; }

    void Start(double iterations, bool requested = false)
    {
        // Whoever starts a play owns it: a Play() on top of the app's loop hands it to the view, and
        // the app setting IsPlaying takes it back.
        playRequested = requested;

        // Nothing starts before the view is loaded. An unparented view is not rendering anyway, and
        // reaching for a dispatcher from inside a constructor — which an implicit Style setting
        // IsPlaying will do — is how a headless host ends up deadlocked.
        if (!loaded)
        {
            pendingAutoPlay = true;
            pendingIterations = iterations;
            return;
        }

        if (scene is null || spec is null)
            return;

        // Always stop first: Play captures the baselines it will restore later, so starting on top
        // of a half-finished animation would bake a mid-pose in as the resting one.
        player?.Stop();
        DetachPlayer();

        if (TryStartNative(iterations))
            return;

        ticker ??= MotionTicker.For(this);

        if (ticker is null)
            return;

        timeline = MotionSceneBuilder.BuildTimeline(
            icon!, spec, scene, ResolveColor(), (float)StrokeWidth, iterations);

        if (timeline is null)
            return;

        stopAtCycleEnd = false;
        PlayIterations = iterations;

        player = new Player(timeline, ticker) { RestoreOnStop = true, Rate = Speed };
        player.Finished += OnPlayerFinished;

        player.Play();

        // A hidden icon starts paused: IsPlaying still says it is playing, and it picks up from
        // the first frame once it is shown.
        SyncVisibility();

        SetPlaybackFlag(true);
        Invalidate();
    }

    /// <summary>
    /// Hands the play to the platform compositor when there is one, so a looping icon costs the app
    /// nothing per frame. See <see cref="INativeMotionPlayer"/>.
    /// </summary>
    bool TryStartNative(double iterations)
    {
        // A negative rate runs the timeline backwards, which only the managed player does.
        if (NativeMotion.Factory is not { } factory || Speed <= 0d)
            return false;

        var request = new NativeMotionRequest(
            icon!, spec!, ResolveColor(), AccentColor, (float)StrokeWidth, iterations, Speed);

        if (factory(this, request) is not { } created)
            return false;

        native = created;
        native.Finished += OnNativeFinished;
        stopAtCycleEnd = false;
        PlayIterations = iterations;

        // The compositor draws the moving icon; the scene would draw the resting one underneath it.
        Drawable = NothingToDraw.Instance;

        SyncVisibility();
        SetPlaybackFlag(true);
        Invalidate();
        return true;
    }

    void DetachPlayer()
    {
        if (player is not null)
        {
            player.Finished -= OnPlayerFinished;
            player.Dispose();
            player = null;
        }

        if (native is not null)
        {
            native.Finished -= OnNativeFinished;
            native.Dispose();
            native = null;
            Drawable = scene;
        }

        StopTicking();
    }

    /// <summary>
    /// Pauses a hidden icon and resumes a visible one.
    /// </summary>
    /// <remarks>
    /// MAUI raises no <c>Unloaded</c> for <c>IsVisible = false</c>, so without this a looping icon
    /// that is merely hidden keeps redrawing and writing <see cref="Progress"/> every frame — and
    /// keeps the shared ticker awake for nobody. Pausing rather than stopping keeps the position,
    /// so showing the icon again carries on mid-cycle instead of restarting. Only the view's own
    /// <see cref="VisualElement.IsVisible"/> is watched; a hidden ancestor does not change it.
    /// </remarks>
    void SyncVisibility()
    {
        if (native is not null)
        {
            if (IsVisible)
                native.Resume();
            else
                native.Pause();
            return;
        }

        if (player is null)
            return;

        if (IsVisible)
        {
            if (player.State is PlaybackState.Paused)
                player.Resume();

            // Subscribed after the player so the redraw sees the state the player just wrote.
            if (!ticking && ticker is not null)
            {
                ticker.Tick += OnTick;
                ticking = true;
            }

            Invalidate();
        }
        else
        {
            player.Pause();
            StopTicking();
        }
    }

    void StopTicking()
    {
        if (ticking && ticker is not null)
            ticker.Tick -= OnTick;

        ticking = false;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        if (propertyName == IsVisibleProperty.PropertyName)
            SyncVisibility();
    }

    // Internal so tests can stand in for Unloaded, which only a real handler raises.
    internal void OnUnloadedInternal()
    {
        loaded = false;

        if ((player is not null || native is not null) && !playRequested)
        {
            // A play the view started itself ends here, at rest and with IsPlaying dropped, as if its
            // cycles had run out. Left raised, the next Loaded would read IsPlaying as the app's busy
            // flag and loop forever; replayed, an icon that was mid-animation when its page went away
            // would animate again when the user comes back. Whoever wants it played on the new
            // parent plays it there.
            Stop();
        }
        else
        {
            // The app's loop keeps IsPlaying true across the gap and resumes at the next Loaded.
            player?.Stop();
            DetachPlayer();
        }

        // The ticker is shared with every other icon on the window, so it is not ours to dispose —
        // dropping the subscription is what releases it.
        ticker = null;
    }

    void OnTick(TimeSpan delta)
    {
        var duration = spec?.Duration ?? TimeSpan.Zero;

        if (player is null || duration <= TimeSpan.Zero)
            return;

        Invalidate();

        var cycles = player.Position / duration;

        if (stopAtCycleEnd && cycles >= 1d)
        {
            Stop();
            Completed?.Invoke(this, EventArgs.Empty);
            return;
        }

        WriteProgress(cycles - Math.Floor(cycles));
    }

    void OnPlayerFinished(object? sender, EventArgs e)
    {
        Stop();
        Completed?.Invoke(this, EventArgs.Empty);
    }

    void OnNativeFinished(object? sender, EventArgs e)
    {
        Stop();
        Completed?.Invoke(this, EventArgs.Empty);
    }

    void Rebuild()
    {
        icon = MotionResolver.ResolveIcon(Source, Icon, PathData);

        spec = MotionResolver.ResolveMotion(
            icon,
            Motion,
            Duration > TimeSpan.Zero ? Duration : null,
            Interval > TimeSpan.Zero ? Interval : null);

        if (icon is null)
        {
            scene = null;
            timeline = null;
            Drawable = null;
            Invalidate();
            return;
        }

        scene = MotionSceneBuilder.BuildScene(icon, spec, ResolveColor(), AccentColor, (float)StrokeWidth);
        timeline = spec is null
            ? null
            : MotionSceneBuilder.BuildTimeline(
                icon, spec, scene, ResolveColor(), (float)StrokeWidth, double.PositiveInfinity);

        Drawable = scene;
        Invalidate();
    }

    internal Color ResolveColor() => Color ?? (Color?)GetValue(ThemeColorProperty) ?? Colors.Black;

    void RebuildAndResume()
    {
        if (!loaded)
            return;

        var wasPlaying = player is not null || native is not null;

        player?.Stop();
        DetachPlayer();

        Rebuild();

        // A looping icon (Loop trigger, or a bound IsPlaying) keeps looping; anything else picks up a
        // fresh play of RepeatCount.
        if (wasPlaying)
            Start(Trigger.HasFlag(MotionTrigger.Loop) || double.IsPositiveInfinity(PlayIterations)
                ? double.PositiveInfinity
                : PlaybackIterations(), playRequested);
        else
            Invalidate();
    }

    void ApplyTriggerGestures()
    {
        GestureRecognizers.Clear();

        var trigger = Trigger;

        if (trigger.HasFlag(MotionTrigger.Hover))
        {
            var pointer = new PointerGestureRecognizer();

            pointer.PointerEntered += (_, _) => Loop();
            pointer.PointerExited += (_, _) => StopAtCycleEnd();

            GestureRecognizers.Add(pointer);
        }

        if (!trigger.HasFlag(MotionTrigger.Press) && Command is null)
            return;

        var tap = new TapGestureRecognizer();

        tap.Tapped += (_, _) =>
        {
            if (Trigger.HasFlag(MotionTrigger.Press))
                Play();

            if (Command?.CanExecute(CommandParameter) == true)
                Command.Execute(CommandParameter);
        };

        GestureRecognizers.Add(tap);
    }

    void SetPlaybackFlag(bool value)
    {
        if (IsPlaying == value)
            return;

        suppressPlaybackFeedback = true;

        try
        {
            IsPlaying = value;
        }
        finally
        {
            suppressPlaybackFeedback = false;
        }
    }

    void WriteProgress(double value)
    {
        suppressProgressFeedback = true;

        try
        {
            Progress = Math.Clamp(value, 0d, 1d);
        }
        finally
        {
            suppressProgressFeedback = false;
        }
    }

    static void OnArtworkChanged(BindableObject bindable, object oldValue, object newValue)
        => ((MotionIconView)bindable).RebuildAndResume();

    static void OnThemeColorChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (MotionIconView)bindable;
        if (view.Color is null)
            view.RebuildAndResume();
    }

    static void OnTriggerChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (MotionIconView)bindable;
        view.ApplyTriggerGestures();

        // Loop and Appear are otherwise read only in Loaded, so a Trigger that arrives later — a late
        // BindingContext, a runtime switch — would never start, and leaving Loop would never stop.
        if (!view.loaded)
            return;

        var was = (MotionTrigger)oldValue;
        var now = (MotionTrigger)newValue;

        if (now.HasFlag(MotionTrigger.Loop) && !was.HasFlag(MotionTrigger.Loop))
            view.Start(double.PositiveInfinity);
        else if (was.HasFlag(MotionTrigger.Loop) && !now.HasFlag(MotionTrigger.Loop))
            view.Stop();
        else if (now.HasFlag(MotionTrigger.Appear) && !was.HasFlag(MotionTrigger.Appear))
            view.Start(view.RepeatCount);
    }

    static void OnSpeedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is MotionIconView { player: not null } view)
            view.player.Rate = (double)newValue;
        // A compositor play is built for one rate: start it again at the new one, or on the managed
        // player for a rate only it can run.
        else if (bindable is MotionIconView { native: not null } nativeView)
            nativeView.Start(nativeView.PlayIterations);
    }

    static void OnIsPlayingChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (MotionIconView)bindable;

        if (view.suppressPlaybackFeedback)
            return;

        // Set from outside, IsPlaying is a busy flag: it loops until it is cleared, matching Blazor.
        // RepeatCount bounds a *triggered* play (Play, Press, Appear), never the flag.
        if ((bool)newValue)
            view.Start(double.PositiveInfinity, requested: true);
        else
            view.Stop();
    }

    static object OnIsPlayingWritten(BindableObject bindable, object value)
    {
        var view = (MotionIconView)bindable;

        // The app writing true while the view's own play already holds IsPlaying true is no change as
        // far as the property is concerned, so propertyChanged never runs — the play would still end
        // after its cycles and drop the flag the app had just set, and an unload would not keep it.
        // Coercion sees every write, equal or not, so the app's loop takes over here instead.
        if (!view.suppressPlaybackFeedback && (bool)value && view.IsPlaying && !view.playRequested)
            view.Start(double.PositiveInfinity, requested: true);

        return value;
    }

    static void OnProgressChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (MotionIconView)bindable;

        // Ignore the write we made ourselves while reporting position, or a two-way bound slider
        // would fight the animation for control.
        if (view.suppressProgressFeedback || view.timeline is null || view.spec is null || view.player is not null || view.native is not null)
            return;

        // Held just short of the end: with FillMode.None — which matches the Blazor side — landing
        // exactly on the end reverts every track to its baseline, so a scrubber dragged fully right
        // would snap back to the resting pose instead of showing the final frame.
        var progress = Math.Clamp((double)newValue, 0d, 0.9999d);

        view.timeline.Evaluate(view.spec.Duration * progress);
        view.Invalidate();
    }

    /// <summary>Stands in for the scene while the compositor draws the icon.</summary>
    sealed class NothingToDraw : IDrawable
    {
        public static readonly NothingToDraw Instance = new();

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
        }
    }
}
