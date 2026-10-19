using Microsoft.Maui.Layouts;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.Flyout;

/// <summary>
/// Hosts up to two <see cref="FlyoutPanel"/>s — one on each edge — around a single
/// <see cref="Content"/> view, and owns everything about where they are: the transition, the scrim,
/// the swipe gestures, and whether an expanded panel pushes the content or floats over it.
/// </summary>
/// <remarks>
/// <para>
/// It is a custom <see cref="Layout"/> rather than a <see cref="Grid"/> with animated column widths
/// because the two transitions are not the same shape. Sliding a hidden panel in is pure
/// translation — the panel is already at its final size, so no layout pass is needed and the drawer
/// stays smooth on a phone. Growing a rail into a full panel really is a resize, and the content
/// beside it has to be re-laid out each frame. One arrange pass that knows the difference beats a
/// grid that re-lays out for both.
/// </para>
/// <para>
/// Place it in a page's content (or use <see cref="ShinyFlyoutPage"/>, or install it over every page
/// with <see cref="ShinyFlyout"/>). The panels span the full height of the view, so a flyout that
/// should sit under an app bar goes below the app bar rather than around it.
/// </para>
/// </remarks>
[ContentProperty(nameof(Content))]
public partial class FlyoutView : Layout
{
    /// <summary>
    /// Appended to the view's own <see cref="Element.AutomationId"/> to name the scrim, which closes an
    /// overlaid panel when tapped. The panels are yours, so name them directly.
    /// </summary>
    public const string ScrimAutomationIdSuffix = "_Scrim";

    const string AnimationName = "ShinyFlyoutTransition";
    const double OpenThreshold = 0.4;

    readonly ContentView contentHost;
    readonly BoxView scrim;
    readonly SideRuntime start = new(FlyoutSide.Start);
    readonly SideRuntime end = new(FlyoutSide.End);

    double hostWidth;
    double hostHeight;
    bool measuredOnce;
    bool retargetQueued;
    bool applyingResponsive;
    double scrimProgress;

    public FlyoutView()
    {
        // A hidden panel is parked outside the view's bounds; without clipping it paints over
        // whatever is beside the flyout.
        this.IsClippedToBounds = true;

        this.contentHost = new ContentView { ZIndex = ZOrder.Content };
        this.Children.Add(this.contentHost);

        this.scrim = new BoxView
        {
            Opacity = 0,
            IsVisible = false,
            InputTransparent = true,
            ZIndex = ZOrder.Scrim
        };
        Tint(this.scrim, BoxView.ColorProperty, null, ShinyThemeKeys.Color.Scrim);

        var scrimTap = new TapGestureRecognizer();
        scrimTap.Tapped += (_, _) => this.OnScrimTapped();
        this.scrim.GestureRecognizers.Add(scrimTap);

        var scrimPan = new PanGestureRecognizer();
        scrimPan.PanUpdated += (_, e) => this.OnScrimPan(e);
        this.scrim.GestureRecognizers.Add(scrimPan);
        this.Children.Add(this.scrim);
        AutomationIds.Link(this, this.scrim, ScrimAutomationIdSuffix);

        this.InitSide(this.start);
        this.InitSide(this.end);

        FlyoutRegistry.Register(this);

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(FlyoutView));
    }


    static class ZOrder
    {
        public const int Content = 0;
        public const int EdgeStrip = 10;
        public const int Scrim = 20;
        public const int Panel = 30;
        public const int Grip = 40;
    }


    /// <summary>Everything the host tracks for one side. Widths are live values, not targets.</summary>
    sealed class SideRuntime(FlyoutSide side)
    {
        public FlyoutSide Side { get; } = side;
        public FlyoutPanel? Panel;
        public BoxView Edge = null!;
        public BoxView Grip = null!;

        /// <summary>The width the panel is arranged at.</summary>
        public double PanelWidth;

        /// <summary>How much of that width is actually on screen — the rest is translated out.</summary>
        public double Visible;

        /// <summary>What the content beside the panel is inset by.</summary>
        public double Inset;

        /// <summary>
        /// The inset the panel had the last time it was not expanded. An expanded floating panel keeps
        /// it, which is what stops the content from jumping when a rail is expanded over it.
        /// </summary>
        public double LastRestingInset;

        public double TargetPanelWidth;
        public double TargetVisible;
        public double TargetInset;

        /// <summary>Set while <see cref="FlyoutPanel.CollapseBelow"/> is holding the panel down, so it can be put back.</summary>
        public FlyoutPanelState? CompactedFrom;

        public FlyoutPanelState AppliedState = FlyoutPanelState.Hidden;
        public bool IsDragging;
        public double DragStartVisible;
        public double DragStartWidth;
        public TaskCompletionSource? Transition;
    }


    /// <summary>Where every animated value stood when the current transition started.</summary>
    readonly record struct TransitionFrame(
        double StartPanelWidth, double StartVisible, double StartInset,
        double EndPanelWidth, double EndVisible, double EndInset,
        double Scrim);


    /// <summary>
    /// Kept off the control itself: <see cref="ILayoutManager.Measure"/> would hide
    /// <see cref="VisualElement.Measure"/>, and a caller asking a view for its size would silently get
    /// the layout pass instead.
    /// </summary>
    sealed class FlyoutLayoutManager(FlyoutView view) : ILayoutManager
    {
        public Size Measure(double widthConstraint, double heightConstraint)
            => view.MeasureLayout(widthConstraint, heightConstraint);

        public Size ArrangeChildren(Rect bounds) => view.ArrangeLayout(bounds);
    }


    IEnumerable<SideRuntime> Sides
    {
        get
        {
            yield return this.start;
            yield return this.end;
        }
    }


    void InitSide(SideRuntime side)
    {
        side.Edge = new BoxView
        {
            Color = Colors.Transparent,
            IsVisible = false,
            ZIndex = ZOrder.EdgeStrip
        };
        var edgePan = new PanGestureRecognizer();
        edgePan.PanUpdated += (_, e) => this.OnEdgePan(side, e);
        side.Edge.GestureRecognizers.Add(edgePan);
        this.Children.Add(side.Edge);

        side.Grip = new BoxView
        {
            Color = Colors.Transparent,
            IsVisible = false,
            ZIndex = ZOrder.Grip
        };
        var gripPan = new PanGestureRecognizer();
        gripPan.PanUpdated += (_, e) => this.OnGripPan(side, e);
        side.Grip.GestureRecognizers.Add(gripPan);
        this.Children.Add(side.Grip);
    }


    #region public surface

    /// <summary>Raised once a panel has settled into a new state.</summary>
    public event EventHandler<FlyoutStateChangedEventArgs>? StateChanged;

    public FlyoutPanel? GetPanel(FlyoutSide side) => this.Runtime(side).Panel;

    public FlyoutPanelState GetState(FlyoutSide side) => this.Runtime(side).Panel?.State ?? FlyoutPanelState.Hidden;

    /// <summary>The panel's width right now, mid-transition included.</summary>
    public double GetCurrentWidth(FlyoutSide side) => this.Runtime(side).Visible;

    /// <summary>What the content beside a panel is currently inset by.</summary>
    public double GetContentInset(FlyoutSide side) => this.Runtime(side).Inset;

    /// <summary>How far the scrim has come in, 0 to 1, before <see cref="ScrimOpacity"/> is applied.</summary>
    internal double ScrimProgress => this.scrimProgress;

    /// <summary>The bounds the content was last arranged into.</summary>
    internal Rect ContentBounds => this.contentHost.Frame;

    /// <summary>What <see cref="FlyoutPresentation.Auto"/> currently resolves to for that side.</summary>
    public FlyoutPresentation GetEffectivePresentation(FlyoutSide side)
    {
        var panel = this.Runtime(side).Panel;
        return panel is null ? FlyoutPresentation.Overlay : this.ResolvePresentation(panel);
    }

    /// <summary>Expands the panel, or returns it to its <see cref="FlyoutPanel.CollapsedState"/>.</summary>
    public Task ToggleAsync(FlyoutSide side = FlyoutSide.Start)
    {
        var panel = this.Runtime(side).Panel;
        return panel is null ? Task.CompletedTask : panel.ToggleAsync();
    }

    public Task SetStateAsync(FlyoutSide side, FlyoutPanelState state)
    {
        var panel = this.Runtime(side).Panel;
        return panel is null ? Task.CompletedTask : panel.SetStateAsync(state);
    }

    /// <summary>Completes when the side's current transition has finished, immediately if none is running.</summary>
    public Task WaitForTransitionAsync(FlyoutSide side) => this.Runtime(side).Transition?.Task ?? Task.CompletedTask;

    #endregion


    #region host callbacks from the panels

    internal void OnPanelStateChanged(FlyoutPanel panel, FlyoutPanelState oldState, FlyoutPanelState newState)
    {
        var side = this.RuntimeFor(panel);
        if (side is null || this.applyingResponsive)
            return;

        // A deliberate state change outranks a width-driven one: once the user has said what they
        // want, growing the host back must not overwrite it.
        side.CompactedFrom = null;
        this.BeginTransition(side);
        this.Retarget(animate: true);
    }


    internal void OnPanelInvalidated(FlyoutPanel panel, bool animate)
    {
        if (this.RuntimeFor(panel) is null)
            return;

        this.Retarget(animate);
    }

    #endregion


    #region layout

    protected override ILayoutManager CreateLayoutManager() => new FlyoutLayoutManager(this);

    internal Size MeasureLayout(double widthConstraint, double heightConstraint)
    {
        var padding = this.Padding;
        var availableWidth = widthConstraint - padding.HorizontalThickness;
        var availableHeight = heightConstraint - padding.VerticalThickness;

        // Only the first measure learns the width (see SyncHostSize); after that the arrange owns it.
        if (!this.measuredOnce)
            this.SyncHostSize(availableWidth, availableHeight);

        var (leftInset, rightInset) = this.PhysicalInsets();

        // Shift measures at the FULL width. That is the whole difference: the content is asked for
        // the same size it would take with no panel open, so nothing inside it reflows as the panel
        // moves - it is the measure pass, not the arrange, that rewraps text and collapses columns.
        var contentWidth = this.PushMode == FlyoutPushMode.Shift
            ? Math.Max(0, availableWidth)
            : Math.Max(0, availableWidth - leftInset - rightInset);

        var contentSize = this.contentHost.Measure(contentWidth, availableHeight);

        var tallest = contentSize.Height;
        foreach (var side in this.Sides)
        {
            if (side.Panel is null)
                continue;

            var size = side.Panel.Measure(side.PanelWidth, availableHeight);
            tallest = Math.Max(tallest, size.Height);
        }

        var width = double.IsInfinity(availableWidth)
            ? contentSize.Width + leftInset + rightInset
            : availableWidth;

        var height = double.IsInfinity(availableHeight) ? tallest : availableHeight;

        return new Size(width + padding.HorizontalThickness, height + padding.VerticalThickness);
    }


    internal Size ArrangeLayout(Rect bounds)
    {
        var padding = this.Padding;
        var x = bounds.X + padding.Left;
        var y = bounds.Y + padding.Top;
        var width = Math.Max(0, bounds.Width - padding.HorizontalThickness);
        var height = Math.Max(0, bounds.Height - padding.VerticalThickness);

        this.SyncHostSize(width, height);

        var (left, right) = this.PhysicalSides();
        var leftInset = left?.Inset ?? 0;
        var rightInset = right?.Inset ?? 0;

        // Shift keeps the content its full width and slides it by the net of the two insets, so a
        // panel on each side cancels out rather than crushing what is between them. The far edge
        // travels out of the view's bounds, which is why the view clips.
        var contentBounds = this.PushMode == FlyoutPushMode.Shift
            ? new Rect(x + leftInset - rightInset, y, width, height)
            : new Rect(x + leftInset, y, Math.Max(0, width - leftInset - rightInset), height);

        this.contentHost.Arrange(contentBounds);

        this.scrim.Arrange(new Rect(x, y, width, height));

        if (left?.Panel is { } leftPanel)
            leftPanel.Arrange(new Rect(x, y, left.PanelWidth, height));

        if (right?.Panel is { } rightPanel)
            rightPanel.Arrange(new Rect(x + width - right.PanelWidth, y, right.PanelWidth, height));

        this.ArrangeStrips(left, right, x, y, width, height);
        this.UpdateVisuals();

        return new Size(width + padding.HorizontalThickness, height + padding.VerticalThickness);
    }


    void ArrangeStrips(SideRuntime? left, SideRuntime? right, double x, double y, double width, double height)
    {
        if (left is not null)
        {
            var catchWidth = left.Panel?.EdgeSwipeWidth ?? 0;
            left.Edge.Arrange(new Rect(x + left.Inset, y, catchWidth, height));
            left.Grip.Arrange(new Rect(x + left.Visible - GripWidth, y, GripWidth, height));
        }

        if (right is not null)
        {
            var catchWidth = right.Panel?.EdgeSwipeWidth ?? 0;
            right.Edge.Arrange(new Rect(x + width - right.Inset - catchWidth, y, catchWidth, height));
            right.Grip.Arrange(new Rect(x + width - right.Visible, y, GripWidth, height));
        }
    }


    /// <summary>
    /// The host's width is what the responsive rules are measured against, and it is learned from the
    /// layout pass rather than from <c>OnSizeAllocated</c> so that the first arrange already knows it —
    /// a panel that only found out afterwards would paint once at the wrong width and then correct
    /// itself, which reads as a flash on every page that opens with a flyout already expanded.
    /// <para>
    /// After that first pass only the arrange reports the width. WinUI hands the measure and the arrange
    /// widths a fraction of a pixel apart (894.7 and 895.3), so taking it from both made every pass a
    /// width change, and every width change re-targets and invalidates the measure: a layout cycle that
    /// WinUI ends by killing the app. A change found during the arrange is also re-targeted once the pass
    /// is over rather than inside it, so it cannot invalidate the layout that is running.
    /// </para>
    /// </summary>
    /// <summary>The width the responsive rules are judged against. Test seam.</summary>
    internal double HostWidth => this.hostWidth;


    void SyncHostSize(double width, double height)
    {
        if (double.IsInfinity(width) || double.IsNaN(width) || width <= 0)
            return;

        var widthChanged = Math.Abs(width - this.hostWidth) > 0.5;
        this.hostWidth = width;
        this.hostHeight = height;

        if (!this.measuredOnce)
        {
            this.measuredOnce = true;
            this.ApplyResponsive();
            this.Retarget(animate: false);
        }
        else if (widthChanged)
        {
            this.ApplyResponsive();
            this.RetargetAfterLayout();
        }
    }


    void RetargetAfterLayout()
    {
        if (this.Dispatcher is not { } dispatcher)
        {
            this.Retarget(animate: true);
            return;
        }

        if (this.retargetQueued)
            return;

        this.retargetQueued = true;
        dispatcher.Dispatch(() =>
        {
            this.retargetQueued = false;
            this.Retarget(animate: true);
        });
    }


    /// <summary>
    /// Width-driven state changes: drop an expanded panel to its collapsed state under
    /// <see cref="FlyoutPanel.CollapseBelow"/>, and put it back when there is room again.
    /// </summary>
    void ApplyResponsive()
    {
        if (this.hostWidth <= 0)
            return;

        this.applyingResponsive = true;
        try
        {
            foreach (var side in this.Sides)
            {
                var panel = side.Panel;
                if (panel is null || panel.CollapseBelow <= 0)
                    continue;

                if (this.hostWidth < panel.CollapseBelow)
                {
                    if (panel.State == FlyoutPanelState.Expanded)
                    {
                        side.CompactedFrom = FlyoutPanelState.Expanded;
                        panel.State = panel.CollapsedState;
                    }
                }
                else if (side.CompactedFrom is { } previous)
                {
                    side.CompactedFrom = null;
                    panel.State = previous;
                }
            }
        }
        finally
        {
            this.applyingResponsive = false;
        }
    }

    #endregion


    #region transition

    void Retarget(bool animate)
    {
        foreach (var side in this.Sides)
            this.ComputeTargets(side);

        var scrimTarget = this.ComputeScrimTarget();
        var duration = this.TransitionDuration();

        if (!this.measuredOnce || !animate || !this.IsAnimationEnabled || duration <= 0)
        {
            this.AbortAnimation(AnimationName);
            this.ApplyFrame(1, null, scrimTarget);
            this.CompleteTransitions();
            return;
        }

        var from = new TransitionFrame(
            this.start.PanelWidth, this.start.Visible, this.start.Inset,
            this.end.PanelWidth, this.end.Visible, this.end.Inset,
            this.scrimProgress
        );

        this.AbortAnimation(AnimationName);
        var animation = new Animation(t => this.ApplyFrame(t, from, scrimTarget), 0, 1);
        animation.Commit(
            this,
            AnimationName,
            length: (uint)duration,
            easing: Easing.CubicOut,
            finished: (_, _) =>
            {
                this.ApplyFrame(1, from, scrimTarget);
                this.CompleteTransitions();
            }
        );
    }


    void ComputeTargets(SideRuntime side)
    {
        var panel = side.Panel;
        if (panel is null)
        {
            side.TargetPanelWidth = side.TargetVisible = side.TargetInset = 0;
            return;
        }

        var state = panel.State;
        var resting = this.ClampWidth(panel.RestingWidth(state));
        var presentation = this.ResolvePresentation(panel);
        var floating = presentation == FlyoutPresentation.Overlay && state == FlyoutPanelState.Expanded;
        panel.ApplyEffectivePresentation(presentation, floating);

        side.TargetVisible = resting;
        side.TargetPanelWidth = resting > 0
            ? resting
            : side.PanelWidth > 0
                ? side.PanelWidth
                : this.ClampWidth(panel.RestingWidth(FlyoutPanelState.Expanded));

        if (presentation == FlyoutPresentation.Push || state != FlyoutPanelState.Expanded)
        {
            side.TargetInset = resting;

            if (state != FlyoutPanelState.Expanded)
                side.LastRestingInset = resting;
        }
        else
        {
            // Floating: the content keeps the inset it had before the panel was expanded, so
            // expanding a rail slides the panel over the content instead of shoving it sideways.
            side.TargetInset = side.LastRestingInset;
        }

        // Coming in from nothing: take the destination width up front so the panel translates in at
        // its full size rather than growing out of the edge.
        if (side.Visible <= 0.5 && !side.IsDragging)
            side.PanelWidth = side.TargetPanelWidth;
    }


    double ComputeScrimTarget()
    {
        foreach (var side in this.Sides)
        {
            if (side.Panel is not { } panel || !panel.HasScrim)
                continue;

            if (panel.State == FlyoutPanelState.Expanded && this.ResolvePresentation(panel) == FlyoutPresentation.Overlay)
                return 1;
        }
        return 0;
    }


    double TransitionDuration()
    {
        double duration = 0;
        foreach (var side in this.Sides)
        {
            if (side.Panel is { } panel)
                duration = Math.Max(duration, panel.AnimationDuration);
        }
        return duration;
    }


    /// <summary>
    /// One frame of the transition. Returns having invalidated layout only if something that layout
    /// depends on actually moved — a pure slide is translation and costs no layout pass at all.
    /// </summary>
    void ApplyFrame(double t, TransitionFrame? from, double scrimTarget)
    {
        var needsLayout = false;

        needsLayout |= Apply(
            this.start,
            from is { } f1 ? (f1.StartPanelWidth, f1.StartVisible, f1.StartInset) : null,
            t
        );
        needsLayout |= Apply(
            this.end,
            from is { } f2 ? (f2.EndPanelWidth, f2.EndVisible, f2.EndInset) : null,
            t
        );

        this.scrimProgress = from is { } f3 ? Lerp(f3.Scrim, scrimTarget, t) : scrimTarget;

        this.UpdateVisuals();

        if (needsLayout)
            this.InvalidateMeasure();

        static bool Apply(SideRuntime side, (double Width, double Visible, double Inset)? from, double t)
        {
            var width = from is { } f ? Lerp(f.Width, side.TargetPanelWidth, t) : side.TargetPanelWidth;
            var inset = from is { } g ? Lerp(g.Inset, side.TargetInset, t) : side.TargetInset;
            var visible = from is { } h ? Lerp(h.Visible, side.TargetVisible, t) : side.TargetVisible;

            var changed = Math.Abs(width - side.PanelWidth) > 0.01 || Math.Abs(inset - side.Inset) > 0.01;

            side.PanelWidth = width;
            side.Inset = inset;
            side.Visible = visible;
            return changed;
        }
    }


    static double Lerp(double from, double to, double t) => from + ((to - from) * t);


    /// <summary>
    /// Everything that can be set without a layout pass: how far each panel is translated out, what
    /// the scrim looks like, and which of the strips are live.
    /// </summary>
    void UpdateVisuals()
    {
        foreach (var side in this.Sides)
        {
            if (side.Panel is not { } panel)
                continue;

            var offset = Math.Max(0, side.PanelWidth - side.Visible);
            panel.TranslationX = this.IsPhysicallyLeft(side.Side) ? -offset : offset;
            panel.IsVisible = side.Visible > 0.5 || side.IsDragging;

            var presentation = this.ResolvePresentation(panel);
            var floating = presentation == FlyoutPresentation.Overlay;

            side.Edge.IsVisible = floating
                && panel.IsSwipeEnabled
                && panel.EdgeSwipeWidth > 0
                && panel.State != FlyoutPanelState.Expanded;

            side.Grip.IsVisible = !floating
                && panel.IsResizable
                && panel.State == FlyoutPanelState.Expanded;
        }

        this.scrim.Opacity = this.scrimProgress * this.ScrimOpacity;
        this.scrim.IsVisible = this.scrimProgress > 0.001;
        this.scrim.InputTransparent = !this.scrim.IsVisible;
    }


    void BeginTransition(SideRuntime side)
    {
        side.Transition ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }


    void CompleteTransitions()
    {
        foreach (var side in this.Sides)
        {
            var state = side.Panel?.State ?? FlyoutPanelState.Hidden;
            if (state != side.AppliedState)
            {
                var args = new FlyoutStateChangedEventArgs(side.Side, side.AppliedState, state);
                side.AppliedState = state;
                side.Panel?.RaiseStateChanged(args);
                this.StateChanged?.Invoke(this, args);
                FlyoutRegistry.RaiseStateChanged(this, args);
            }

            var transition = side.Transition;
            side.Transition = null;
            transition?.TrySetResult();
        }
    }

    #endregion


    #region geometry helpers

    const double GripWidth = 10;

    SideRuntime Runtime(FlyoutSide side) => side == FlyoutSide.Start ? this.start : this.end;

    SideRuntime? RuntimeFor(FlyoutPanel panel)
    {
        if (ReferenceEquals(this.start.Panel, panel))
            return this.start;

        return ReferenceEquals(this.end.Panel, panel) ? this.end : null;
    }

    /// <summary>
    /// Start is the left edge unless the flow direction says otherwise. Read through
    /// <see cref="IView.FlowDirection"/> rather than <see cref="VisualElement.FlowDirection"/>: the
    /// latter can be <c>MatchParent</c>, which is not an answer, and the explicit implementation is
    /// the one that has already resolved it against the tree.
    /// </summary>
    bool IsLeftToRight() => ((IView)this).FlowDirection != FlowDirection.RightToLeft;

    bool IsPhysicallyLeft(FlyoutSide side)
    {
        var leftToRight = this.IsLeftToRight();
        return side == FlyoutSide.Start ? leftToRight : !leftToRight;
    }

    (SideRuntime? Left, SideRuntime? Right) PhysicalSides()
        => this.IsLeftToRight() ? (this.start, this.end) : (this.end, this.start);

    (double Left, double Right) PhysicalInsets()
    {
        var (left, right) = this.PhysicalSides();
        return (left?.Inset ?? 0, right?.Inset ?? 0);
    }

    /// <summary>A panel can never be wider than the flyout it lives in.</summary>
    double ClampWidth(double width) => this.hostWidth > 0 ? Math.Min(width, this.hostWidth) : width;

    FlyoutPresentation ResolvePresentation(FlyoutPanel panel) => panel.Presentation switch
    {
        FlyoutPresentation.Auto => this.hostWidth > 0 && this.hostWidth >= panel.CompactWidth
            ? FlyoutPresentation.Push
            : FlyoutPresentation.Overlay,
        var explicitly => explicitly
    };

    static void Tint(Element target, BindableProperty property, Color? explicitColor, string themeKey)
    {
        if (explicitColor is null)
        {
            target.SetDynamicResource(property, themeKey);
        }
        else
        {
            target.RemoveDynamicResource(property);
            target.SetValue(property, explicitColor);
        }
    }

    #endregion


    #region gestures

    void OnScrimTapped()
    {
        foreach (var side in this.Sides)
        {
            if (side.Panel is not { } panel)
                continue;

            if (panel.State == FlyoutPanelState.Expanded && panel.CloseOnScrimTap
                && this.ResolvePresentation(panel) == FlyoutPresentation.Overlay)
            {
                panel.State = panel.CollapsedState;
            }
        }
    }


    /// <summary>Dragging the scrim toward the panel's edge closes it, tracking the finger.</summary>
    void OnScrimPan(PanUpdatedEventArgs e)
    {
        var side = this.Sides.FirstOrDefault(s =>
            s.Panel is { } panel
            && panel.IsSwipeEnabled
            && panel.State == FlyoutPanelState.Expanded
            && this.ResolvePresentation(panel) == FlyoutPresentation.Overlay);

        if (side?.Panel is not { } dragged)
            return;

        this.DragSide(side, dragged, e, openingSign: this.IsPhysicallyLeft(side.Side) ? 1 : -1);
    }


    /// <summary>Swiping in from the edge opens the panel, tracking the finger.</summary>
    void OnEdgePan(SideRuntime side, PanUpdatedEventArgs e)
    {
        if (side.Panel is not { } panel || !panel.IsSwipeEnabled)
            return;

        if (this.ResolvePresentation(panel) != FlyoutPresentation.Overlay)
            return;

        this.DragSide(side, panel, e, openingSign: this.IsPhysicallyLeft(side.Side) ? 1 : -1);
    }


    void DragSide(SideRuntime side, FlyoutPanel panel, PanUpdatedEventArgs e, int openingSign)
    {
        var openWidth = this.ClampWidth(panel.RestingWidth(FlyoutPanelState.Expanded));
        var closedWidth = this.ClampWidth(panel.RestingWidth(panel.CollapsedState));
        if (openWidth <= closedWidth)
            return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                this.AbortAnimation(AnimationName);
                side.IsDragging = true;
                side.DragStartVisible = side.Visible;
                side.PanelWidth = openWidth;
                side.Inset = side.LastRestingInset;
                this.InvalidateMeasure();
                break;

            case GestureStatus.Running:
                if (!side.IsDragging)
                    return;

                side.Visible = Math.Clamp(side.DragStartVisible + (openingSign * e.TotalX), closedWidth, openWidth);
                this.scrimProgress = panel.HasScrim
                    ? (side.Visible - closedWidth) / (openWidth - closedWidth)
                    : 0;
                this.UpdateVisuals();
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                if (!side.IsDragging)
                    return;

                side.IsDragging = false;
                var progress = (side.Visible - closedWidth) / (openWidth - closedWidth);
                var settled = progress >= OpenThreshold ? FlyoutPanelState.Expanded : panel.CollapsedState;

                this.BeginTransition(side);
                if (panel.State == settled)
                    this.Retarget(animate: true);   // same state, but the finger left it part-way
                else
                    panel.State = settled;          // the state change retargets for us
                break;
        }
    }


    /// <summary>The inner-edge handle on a pushing panel: a live resize of <see cref="FlyoutPanel.ExpandedWidth"/>.</summary>
    void OnGripPan(SideRuntime side, PanUpdatedEventArgs e)
    {
        if (side.Panel is not { } panel || !panel.IsResizable)
            return;

        if (panel.State != FlyoutPanelState.Expanded || this.ResolvePresentation(panel) != FlyoutPresentation.Push)
            return;

        var sign = this.IsPhysicallyLeft(side.Side) ? 1 : -1;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                this.AbortAnimation(AnimationName);
                side.DragStartWidth = panel.ExpandedWidth;
                break;

            case GestureStatus.Running:
                panel.ExpandedWidth = Math.Clamp(
                    side.DragStartWidth + (sign * e.TotalX),
                    Math.Max(0, panel.MinExpandedWidth),
                    Math.Max(panel.MinExpandedWidth, panel.MaxExpandedWidth)
                );
                break;
        }
    }

    #endregion
}
