using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls;

/// <summary>
/// A guided tour of a page: dim everything, cut a hole around one control at a time, and say what it
/// does.
/// </summary>
/// <remarks>
/// <para>
/// The steps are declared together, in order, on the walkthrough itself — not scattered across the
/// controls they describe as attached properties. On a real screen (nested layouts, templated cells,
/// a control that is only sometimes there) attached ordering is the thing that breaks: nothing can see
/// the sequence as a whole, so reordering means hunting through the markup and a step whose control
/// is conditionally hidden silently derails the rest. A collection reorders by moving a line.
/// </para>
/// <code>
/// &lt;shiny:Walkthrough x:Name="Tour"
///                    RememberRunKey="home-v1"
///                    AutoStart="True"
///                    UseOverlay="True"
///                    OverlayOpacity="0.8"&gt;
///     &lt;shiny:WalkthroughStep Target="{x:Reference SearchBox}"
///                            Title="Find anything"
///                            Text="Search across every project you can see."
///                            Placement="Bottom" /&gt;
///     &lt;shiny:WalkthroughStep Target="{x:Reference AddButton}"
///                            Text="Start something new here."
///                            Display="Spotlight"
///                            Highlight="Circle" /&gt;
/// &lt;/shiny:Walkthrough&gt;
/// </code>
/// <para>
/// The tour paints into a layer above the page's content, so a target inside a scroll view or a card
/// is highlighted where it actually is rather than being clipped by its container.
/// </para>
/// </remarks>
[ContentProperty(nameof(Steps))]
public partial class Walkthrough : ContentView
{
    const double ZoomFrom = 0.85;
    const double PopOvershoot = 1.06;
    const double SlideDistance = 20;

    /// <summary>
    /// Appended to the walkthrough's <see cref="Element.AutomationId"/> to name the callout card. The
    /// callout lives in the page's overlay layer rather than under the walkthrough, so this is how a test
    /// finds it.
    /// </summary>
    public const string CalloutAutomationIdSuffix = "_Callout";

    /// <summary>Appended to the walkthrough's id to name the callout's title label.</summary>
    public const string TitleAutomationIdSuffix = "_Title";

    /// <summary>Appended to the walkthrough's id to name the callout's text label.</summary>
    public const string TextAutomationIdSuffix = "_Text";

    /// <summary>Appended to the walkthrough's id to name the "1 of 4" step counter.</summary>
    public const string CounterAutomationIdSuffix = "_Counter";

    /// <summary>Appended to the walkthrough's id to name the Skip link.</summary>
    public const string SkipAutomationIdSuffix = "_Skip";

    /// <summary>Appended to the walkthrough's id to name the Back link.</summary>
    public const string BackAutomationIdSuffix = "_Back";

    /// <summary>Appended to the walkthrough's id to name the Next (Finish on the last step) link.</summary>
    public const string NextAutomationIdSuffix = "_Next";

    /// <summary>
    /// Where "has this user seen it" is recorded. Replace during startup to keep the flag with the rest
    /// of your user state instead of on the device.
    /// </summary>
    public static IWalkthroughStore Store { get; set; } = new PreferencesWalkthroughStore();

    /// <summary>Forgets that a walkthrough has run, so it auto-starts again.</summary>
    public static void ClearRun(string key) => Store.SetHasRun(key, false);


    readonly ObservableCollection<WalkthroughStep> steps = new();

    PageOverlay.WalkthroughLayer? layer;
    WalkthroughScrim? scrim;
    TooltipBubble? callout;
    BoxView[]? shields;
    ContentPage? watchedPage;

    // Callout body pieces, rebuilt per step but constructed once.
    VerticalStackLayout? body;
    Label? counterLabel;
    Label? skipLabel;
    Label? backLabel;
    Label? nextLabel;
    HorizontalStackLayout? navRow;
    ContentView? customHost;

    TaskCompletionSource<int>? move;
    int? pendingSignal;
    IDispatcherTimer? dwellTimer;
    IDispatcherTimer? startTimer;
    CancellationTokenSource? runCancel;

    View? tapTarget;
    TapGestureRecognizer? tapTargetGesture;
    Rect currentHole = Rect.Zero;
    bool running;
    bool ending;
    bool suppressIsRunning;
    int? queuedStart;
    bool startScheduled;


    public Walkthrough()
    {
        // Renders nothing itself: everything it draws goes into the page's overlay layer. Invisible so
        // no layout reserves space for it, wherever in the markup it is declared.
        this.IsVisible = false;
        this.InputTransparent = true;

        this.steps.CollectionChanged += this.OnStepsChanged;

        this.StartCommand = new Command(() => this.Start());
        this.StopCommand = new Command(() => this.Stop());
        this.NextCommand = new Command(() => this.Next());
        this.BackCommand = new Command(() => this.Back());
        this.SkipCommand = new Command(() => this.Skip());
        this.RestartCommand = new Command(() => this.Restart());

        // The chrome is built per run and torn down after, so the parts are named when they are built
        // and renamed from here if the id changes mid-run.
        AutomationIds.Track(this, this.NameChrome);

        // Last line: replays any styled property applied before the fields existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(Walkthrough));
    }


    /// <summary>The tour, in order. The content property, so steps are just children in XAML.</summary>
    public IList<WalkthroughStep> Steps => this.steps;


    /// <summary>Raised when a run starts.</summary>
    public event EventHandler? Started;

    /// <summary>Raised on every move, including the first step.</summary>
    public event EventHandler<WalkthroughStepEventArgs>? StepChanged;

    /// <summary>Raised however the run ended.</summary>
    public event EventHandler<WalkthroughEndedEventArgs>? Ended;


    /// <summary>Starts the tour. Bind a button to it directly.</summary>
    public ICommand StartCommand { get; }

    /// <summary>Ends the tour without recording it as run.</summary>
    public ICommand StopCommand { get; }

    public ICommand NextCommand { get; }

    public ICommand BackCommand { get; }

    /// <summary>Ends the tour as skipped, which by default still counts as having run.</summary>
    public ICommand SkipCommand { get; }

    /// <summary>Clears <see cref="RememberRunKey"/> and starts again — the "show me the tour" menu item.</summary>
    public ICommand RestartCommand { get; }


    // ---------------------------------------------------------------------------------------------
    // Public control
    // ---------------------------------------------------------------------------------------------

    /// <summary>Starts the tour from the first visible step. No-op if it is already running.</summary>
    public void Start(int fromIndex = 0)
    {
        if (this.running)
        {
            // A run that has been told to end is still unwinding (shrinking the spotlight, fading the
            // scrim). A Start in that window runs once the old run is gone. It used to start straight
            // away, and the old run's teardown then dismantled the new one - leaving it "running" with
            // no chrome and IsRunning false, so every later Start was ignored.
            if (this.ending || this.runCancel?.IsCancellationRequested == true)
                this.queuedStart = fromIndex;
            return;
        }

        _ = this.RunAsync(fromIndex);
    }

    /// <summary>Ends the run. Nothing is recorded, so an auto-start will show it again next time.</summary>
    public void Stop() => this.Signal(SignalStop);

    /// <summary>Moves to the next step, ending the run if this was the last.</summary>
    public void Next() => this.Signal(1);

    /// <summary>Moves back a step. No-op on the first.</summary>
    public void Back() => this.Signal(-1);

    /// <summary>Ends the run as skipped.</summary>
    public void Skip() => this.Signal(SignalSkip);

    /// <summary>Forgets the run flag and starts again.</summary>
    public void Restart()
    {
        this.Reset();
        this.Start();
    }

    /// <summary>Forgets that this walkthrough has run, so <see cref="AutoStart"/> shows it again.</summary>
    public void Reset()
    {
        if (!string.IsNullOrWhiteSpace(this.RememberRunKey))
            Store.SetHasRun(this.RememberRunKey!, false);
    }

    /// <summary>Whether this walkthrough's <see cref="RememberRunKey"/> has already been recorded.</summary>
    public bool HasRun =>
        !string.IsNullOrWhiteSpace(this.RememberRunKey) && Store.HasRun(this.RememberRunKey!);

    /// <summary>Jumps to a step by <see cref="WalkthroughStep.Name"/>. No-op when nothing matches.</summary>
    public void GoTo(string name)
    {
        var visible = this.VisibleSteps();
        var index = visible.FindIndex(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            this.GoTo(index);
    }

    /// <summary>Jumps to a step by position among the visible steps.</summary>
    public void GoTo(int index)
    {
        if (!this.running || this.ending)
        {
            this.Start(index);
            return;
        }
        this.Signal(index - this.CurrentStepIndex);
    }


    const int SignalStop = int.MinValue;
    const int SignalSkip = int.MinValue + 1;


    /// <summary>
    /// Moves the run on, or ends it.
    /// </summary>
    /// <remarks>
    /// A signal can arrive while the run is between steps — mid-scroll, mid-spotlight-travel — when
    /// there is no waiter to hand it to. Holding it until the next wait is what stops a Stop() during
    /// an animation from being swallowed and the tour carrying on regardless.
    /// </remarks>
    void Signal(int delta)
    {
        if (!this.running)
            return;

        // Already ending: nothing left to move, but a Stop still cancels a Start queued behind it.
        if (this.ending)
        {
            if (delta is SignalStop or SignalSkip)
                this.queuedStart = null;
            return;
        }

        this.dwellTimer?.Stop();
        this.dwellTimer = null;

        if (delta is SignalStop or SignalSkip)
        {
            this.queuedStart = null;
            this.runCancel?.Cancel();
        }

        if (this.move?.TrySetResult(delta) == true)
            return;

        this.pendingSignal = delta;
    }


    void OnIsRunningChanged(bool value)
    {
        if (this.suppressIsRunning)
            return;

        if (value)
            this.Start();
        else
            this.Stop();
    }


    // ---------------------------------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------------------------------

    protected override void OnParentSet()
    {
        base.OnParentSet();

        this.WatchPage();

        if (this.Parent is not null)
            this.ScheduleAutoStart();
        else
            this.Stop();
    }


    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        // Steps are BindableObjects rather than elements, so nothing hands them a binding context.
        // Seeding it is what lets a step bind Text or IsVisible to the page's view-model.
        foreach (var step in this.steps)
            SetInheritedBindingContext(step, this.BindingContext);
    }


    void WatchPage()
    {
        if (this.watchedPage is not null)
        {
            this.watchedPage.Disappearing -= this.OnPageDisappearing;
            this.watchedPage = null;
        }

        // Disappearing rather than Unloaded: this control is invisible, and an invisible element's
        // Unloaded is not something to rely on across heads. The page always announces itself.
        this.watchedPage = PageOverlay.FindPage(this);
        if (this.watchedPage is null)
            return;

        this.watchedPage.Disappearing += this.OnPageDisappearing;

        // Install the overlay wrapper now rather than when the tour starts. Wrapping re-parents the
        // page's content, which tears down and rebuilds every native view under it — harmless while
        // the page is still being set up, but mid-session that would reset scroll positions, drop
        // focus, and hand the tour a layout that has not settled to measure targets against.
        // Dispatched so XAML inflation has finished assigning Content first.
        var page = this.watchedPage;
        this.Dispatcher.Dispatch(() =>
        {
            if (ReferenceEquals(this.watchedPage, page))
                PageOverlay.GetOrCreateRoot(page);
        });
    }


    void OnPageDisappearing(object? sender, EventArgs e) => this.Stop();


    void ScheduleAutoStart()
    {
        if (!this.AutoStart || this.startScheduled || this.running)
            return;

        if (this.HasRun)
            return;

        this.startScheduled = true;
        this.startTimer = this.Dispatcher.CreateTimer();
        // Never zero: the page still has to lay out, and measuring a target mid-entrance-animation
        // highlights where it was rather than where it lands.
        this.startTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, this.AutoStartDelay));
        this.startTimer.IsRepeating = false;
        this.startTimer.Tick += (_, _) =>
        {
            if (!this.running && !this.HasRun)
                this.Start();
        };
        this.startTimer.Start();
    }


    void OnStepsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var step in this.steps)
            SetInheritedBindingContext(step, this.BindingContext);

        if (e.OldItems is not null)
        {
            foreach (WalkthroughStep step in e.OldItems)
                step.Changed -= this.OnStepChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (WalkthroughStep step in e.NewItems)
                step.Changed += this.OnStepChanged;
        }

        this.StepCount = this.VisibleSteps().Count;
    }


    void OnStepChanged(object? sender, EventArgs e)
    {
        this.StepCount = this.VisibleSteps().Count;

        if (this.running && !this.ending)
            this.ApplyChrome();
    }


    List<WalkthroughStep> VisibleSteps() => this.steps.Where(s => s.IsVisible).ToList();


    // ---------------------------------------------------------------------------------------------
    // The run
    // ---------------------------------------------------------------------------------------------

    async Task RunAsync(int fromIndex)
    {
        var visible = this.VisibleSteps();
        if (visible.Count == 0)
            return;

        var root = PageOverlay.GetOrCreateRoot(this);
        if (root is null)
            return;

        this.running = true;
        this.SetIsRunning(true);
        this.runCancel = new CancellationTokenSource();
        var token = this.runCancel.Token;

        this.layer = PageOverlay.GetOrCreateLayer<PageOverlay.WalkthroughLayer>(root, PageOverlay.Layers.Walkthrough);
        this.BuildChrome();

        // Everything the tour draws is placed against the root's size. Starting before it has one puts
        // the first callout in the corner and the first spotlight over nothing.
        await WaitForLayoutAsync(root);

        this.Started?.Invoke(this, EventArgs.Empty);
        if (this.StartedCommand?.CanExecute(null) == true)
            this.StartedCommand.Execute(null);

        if (!token.IsCancellationRequested)
            await this.FadeScrimAsync(true);

        var reason = WalkthroughEndReason.Completed;
        var index = Math.Clamp(fromIndex, 0, visible.Count - 1);

        try
        {
            while (true)
            {
                // Ended before (or between) steps - e.g. a Stop while the run was still starting up. The
                // signal says how; falling out of the loop as Completed recorded a Stop as a finished
                // tour, which then never auto-started again.
                if (token.IsCancellationRequested)
                {
                    reason = this.pendingSignal == SignalSkip ? WalkthroughEndReason.Skipped : WalkthroughEndReason.Stopped;
                    break;
                }

                // Re-read every iteration: a step's IsVisible can be bound, and an EnteredCommand on
                // the previous step is exactly where an app flips one.
                visible = this.VisibleSteps();
                if (visible.Count == 0)
                    break;

                index = Math.Clamp(index, 0, visible.Count - 1);
                var step = visible[index];

                await this.EnterStepAsync(step, index, visible.Count, token);
                if (token.IsCancellationRequested && this.pendingSignal is null)
                {
                    reason = WalkthroughEndReason.Stopped;
                    break;
                }

                var delta = await this.WaitForMoveAsync();
                this.move = null;
                await this.LeaveStepAsync(step, token);

                if (delta == SignalStop)
                {
                    reason = WalkthroughEndReason.Stopped;
                    break;
                }

                if (delta == SignalSkip)
                {
                    reason = WalkthroughEndReason.Skipped;
                    break;
                }

                var next = index + delta;
                if (next < 0)
                {
                    // Back from the first step stays put rather than ending the run — dropping a user
                    // out of a tour because they pressed Back once is never what they meant.
                    index = 0;
                    continue;
                }

                if (next >= this.VisibleSteps().Count)
                {
                    reason = WalkthroughEndReason.Completed;
                    break;
                }

                index = next;
            }
        }
        finally
        {
            await this.EndRunAsync(reason);
        }
    }


    /// <summary>Waits until a view has been measured, with a ceiling so a head that never lays it out cannot hang the run.</summary>
    static async Task WaitForLayoutAsync(VisualElement view)
    {
        if (view.Width > 0 && view.Height > 0)
            return;

        var tcs = new TaskCompletionSource();

        void OnSized(object? sender, EventArgs e)
        {
            if (view.Width > 0 && view.Height > 0)
                tcs.TrySetResult();
        }

        view.SizeChanged += OnSized;
        try
        {
            await Task.WhenAny(tcs.Task, Task.Delay(1_000));
        }
        finally
        {
            view.SizeChanged -= OnSized;
        }
    }


    async Task EnterStepAsync(WalkthroughStep step, int index, int count, CancellationToken token)
    {
        this.CurrentStepIndex = index;
        this.CurrentStep = step.Name;
        this.StepNumber = index + 1;
        this.StepCount = count;

        if (step.EnteredCommand?.CanExecute(step.CommandParameter) == true)
            step.EnteredCommand.Execute(step.CommandParameter);

        var target = this.ResolveTarget(step);

        if (target is not null && (step.ScrollToTarget ?? this.ScrollToTarget))
            await this.ScrollIntoViewAsync(target, token);

        if (token.IsCancellationRequested)
            return;

        var root = this.layer?.Parent as Layout;
        var container = root is null ? Size.Zero : new Size(root.Width, root.Height);
        var targetRect = target is not null && root is not null ? ViewGeometry.BoundsIn(target, root) : null;

        var hole = this.HoleFor(step, targetRect);
        await this.MoveHoleAsync(hole, step);

        this.ConfigureCallout(step, index, count);
        await this.SettleCalloutAsync();

        this.WireTargetTap(step, target);
        this.UpdateShields(step, hole, container);

        var placement = this.PlaceCallout(step, hole, targetRect, container);
        await this.AnimateCalloutInAsync(step, placement);

        this.StepChanged?.Invoke(this, new WalkthroughStepEventArgs(step, index, count));
        if (this.StepChangedCommand?.CanExecute(step.Name) == true)
            this.StepChangedCommand.Execute(step.Name);

        this.StartDwell(step);
    }


    async Task LeaveStepAsync(WalkthroughStep step, CancellationToken token)
    {
        this.dwellTimer?.Stop();
        this.dwellTimer = null;
        this.UnwireTargetTap();

        if (step.LeftCommand?.CanExecute(step.CommandParameter) == true)
            step.LeftCommand.Execute(step.CommandParameter);

        if (!token.IsCancellationRequested)
            await this.AnimateCalloutOutAsync(step);
    }


    Task<int> WaitForMoveAsync()
    {
        // A signal that arrived while the step was arriving is taken here rather than dropped.
        if (this.pendingSignal is { } pending)
        {
            this.pendingSignal = null;
            return Task.FromResult(pending);
        }

        this.move = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        return this.move.Task;
    }


    void StartDwell(WalkthroughStep step)
    {
        if (step.Duration <= 0)
            return;

        this.dwellTimer = this.Dispatcher.CreateTimer();
        this.dwellTimer.Interval = TimeSpan.FromMilliseconds(step.Duration);
        this.dwellTimer.IsRepeating = false;
        this.dwellTimer.Tick += (_, _) => this.Next();
        this.dwellTimer.Start();
    }


    async Task EndRunAsync(WalkthroughEndReason reason)
    {
        // Still "running" until the teardown below has finished, so a Start in the meantime is queued
        // rather than racing it.
        this.ending = true;
        this.dwellTimer?.Stop();
        this.dwellTimer = null;
        this.UnwireTargetTap();
        this.move = null;
        this.pendingSignal = null;

        // The spotlight shrinking away as the dim lifts is what makes the end read as "you are back in
        // the app" rather than a layer blinking out.
        await this.ShrinkHoleAsync();
        await this.FadeScrimAsync(false);
        this.TeardownChrome();

        this.CurrentStepIndex = -1;
        this.CurrentStep = null;
        this.StepNumber = 0;

        var remembered = reason == WalkthroughEndReason.Completed
            || (reason == WalkthroughEndReason.Skipped && this.RememberOnSkip);

        if (remembered && !string.IsNullOrWhiteSpace(this.RememberRunKey))
            Store.SetHasRun(this.RememberRunKey!, true);

        this.SetIsRunning(false);

        this.Ended?.Invoke(this, new WalkthroughEndedEventArgs(reason));
        if (this.EndedCommand?.CanExecute(reason) == true)
            this.EndedCommand.Execute(reason);

        switch (reason)
        {
            case WalkthroughEndReason.Completed when this.CompletedCommand?.CanExecute(null) == true:
                this.CompletedCommand.Execute(null);
                break;

            case WalkthroughEndReason.Skipped when this.SkippedCommand?.CanExecute(null) == true:
                this.SkippedCommand.Execute(null);
                break;
        }

        this.runCancel?.Dispose();
        this.runCancel = null;
        this.running = false;
        this.ending = false;

        if (this.queuedStart is { } from)
        {
            this.queuedStart = null;
            this.Start(from);
        }
    }


    /// <summary>Writes IsRunning without the setter turning round and calling Start/Stop again.</summary>
    void SetIsRunning(bool value)
    {
        this.suppressIsRunning = true;
        try
        {
            this.IsRunning = value;
        }
        finally
        {
            this.suppressIsRunning = false;
        }
    }


    // ---------------------------------------------------------------------------------------------
    // Targets
    // ---------------------------------------------------------------------------------------------

    View? ResolveTarget(WalkthroughStep step)
    {
        if (step.Target is not null)
            return step.Target;

        if (string.IsNullOrWhiteSpace(step.TargetName))
            return null;

        Element? current = this;
        while (current is not null)
        {
            if (current.FindByName(step.TargetName) is View found)
                return found;

            current = current.Parent;
        }
        return null;
    }


    async Task ScrollIntoViewAsync(View target, CancellationToken token)
    {
        var scroll = ViewGeometry.EnclosingScrollView(target);
        if (scroll is null)
            return;

        try
        {
            await scroll.ScrollToAsync(target, ScrollToPosition.Center, true);

            // ScrollToAsync returns when the animation is handed off, not when the layout has settled,
            // so a frame is given back before anything measures against the new position.
            await Task.Delay(60, token);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            // A target detached mid-scroll, or a head whose scroll view rejects the request. Neither is
            // worth ending the tour over — the step just highlights wherever the target currently is.
        }
    }


    /// <summary>
    /// Wires "use the control to continue" for a step.
    /// </summary>
    /// <remarks>
    /// A button is not a tap target. <see cref="Button"/>, <see cref="ImageButton"/> and
    /// <see cref="ShinyButton"/> handle the press natively and never run their gesture recognizers, so
    /// adding a <see cref="TapGestureRecognizer"/> to one produces a step that looks wired and simply
    /// never advances — and "tap Save to continue" is the single most likely thing anyone asks this
    /// for. Their <c>Clicked</c> event is hooked instead, which also means the tour advances on the
    /// same gesture that runs the button's own command rather than racing it.
    /// </remarks>
    void WireTargetTap(WalkthroughStep step, View? target)
    {
        this.UnwireTargetTap();

        if (!step.AdvanceOnTargetTap || target is null)
            return;

        this.tapTarget = target;

        switch (target)
        {
            case Button button:
                button.Clicked += this.OnTargetActivated;
                break;

            case ImageButton imageButton:
                imageButton.Clicked += this.OnTargetActivated;
                break;

            case ShinyButton shinyButton:
                shinyButton.Clicked += this.OnTargetActivated;
                break;

            default:
                this.tapTargetGesture = new TapGestureRecognizer();
                this.tapTargetGesture.Tapped += this.OnTargetTapped;
                target.GestureRecognizers.Add(this.tapTargetGesture);
                break;
        }
    }


    void UnwireTargetTap()
    {
        switch (this.tapTarget)
        {
            case Button button:
                button.Clicked -= this.OnTargetActivated;
                break;

            case ImageButton imageButton:
                imageButton.Clicked -= this.OnTargetActivated;
                break;

            case ShinyButton shinyButton:
                shinyButton.Clicked -= this.OnTargetActivated;
                break;
        }

        if (this.tapTarget is not null && this.tapTargetGesture is not null)
        {
            this.tapTargetGesture.Tapped -= this.OnTargetTapped;
            this.tapTarget.GestureRecognizers.Remove(this.tapTargetGesture);
        }

        this.tapTarget = null;
        this.tapTargetGesture = null;
    }


    void OnTargetTapped(object? sender, TappedEventArgs e) => this.Next();

    void OnTargetActivated(object? sender, EventArgs e) => this.Next();


    /// <summary>The cut-out for a step: the target's rect grown by the padding, or nothing at all.</summary>
    Rect HoleFor(WalkthroughStep step, Rect? targetRect)
    {
        if (!this.UseOverlay || targetRect is null)
            return Rect.Zero;

        var shape = step.Highlight ?? this.Highlight;
        if (shape == WalkthroughHighlight.None)
            return Rect.Zero;

        return ViewGeometry.Inflate(targetRect.Value, step.HighlightPadding ?? this.HighlightPadding);
    }
}
