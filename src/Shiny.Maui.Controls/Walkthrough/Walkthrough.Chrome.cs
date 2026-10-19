using Microsoft.Maui.Layouts;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls;

/// <summary>
/// The parts a running walkthrough puts on screen: the scrim, the tap shields around its cut-out, and
/// the callout.
/// </summary>
public partial class Walkthrough
{
    const int ShieldCount = 4;


    void BuildChrome()
    {
        if (this.layer is null || this.scrim is not null)
            return;

        this.scrim = new WalkthroughScrim { Opacity = 0 };
        AbsoluteLayout.SetLayoutFlags(this.scrim, AbsoluteLayoutFlags.All);
        AbsoluteLayout.SetLayoutBounds(this.scrim, new Rect(0, 0, 1, 1));
        this.layer.Children.Add(this.scrim);

        // A GraphicsView cannot hold children, so the probe that resolves the scrim's theme colour has
        // to be parented by someone. It only resolves a resource; it paints nothing.
        this.layer.Children.Add(this.scrim.ColorProbe);

        // Transparent panels that fence off the backdrop. Four of them rather than one full-screen
        // catcher because that is what lets the hole pass touches through to the real control: hit
        // testing has no notion of a hole, so the hole has to be a gap between shields.
        this.shields = new BoxView[ShieldCount];
        for (var i = 0; i < ShieldCount; i++)
        {
            var shield = new BoxView { Color = Colors.Transparent, BackgroundColor = Colors.Transparent };
            AbsoluteLayout.SetLayoutFlags(shield, AbsoluteLayoutFlags.None);
            AbsoluteLayout.SetLayoutBounds(shield, Rect.Zero);

            var tap = new TapGestureRecognizer();
            tap.Tapped += this.OnBackdropTapped;
            shield.GestureRecognizers.Add(tap);

            this.shields[i] = shield;
            this.layer.Children.Add(shield);
        }

        this.BuildCallout();
        this.NameChrome();
        this.ApplyChrome();
    }


    void NameChrome()
    {
        if (this.callout is not null)
        {
            AutomationIds.Set(this.callout, AutomationIds.For(this, CalloutAutomationIdSuffix));
            AutomationIds.Set(this.callout.TitleLabel, AutomationIds.For(this, TitleAutomationIdSuffix));
            AutomationIds.Set(this.callout.TextLabel, AutomationIds.For(this, TextAutomationIdSuffix));
        }

        if (this.counterLabel is not null)
            AutomationIds.Set(this.counterLabel, AutomationIds.For(this, CounterAutomationIdSuffix));
        if (this.skipLabel is not null)
            AutomationIds.Set(this.skipLabel, AutomationIds.For(this, SkipAutomationIdSuffix));
        if (this.backLabel is not null)
            AutomationIds.Set(this.backLabel, AutomationIds.For(this, BackAutomationIdSuffix));
        if (this.nextLabel is not null)
            AutomationIds.Set(this.nextLabel, AutomationIds.For(this, NextAutomationIdSuffix));
    }


    void BuildCallout()
    {
        if (this.layer is null)
            return;

        this.customHost = new ContentView { IsVisible = false };

        this.counterLabel = new Label { VerticalOptions = LayoutOptions.Center };
        this.counterLabel.SetDynamicResource(Label.FontSizeProperty, ShinyThemeKeys.Type.LabelSmallSize);

        this.skipLabel = BuildNavLabel(this.OnSkipTapped);
        this.backLabel = BuildNavLabel(this.OnBackTapped);
        this.nextLabel = BuildNavLabel(this.OnNextTapped);
        this.nextLabel.FontAttributes = FontAttributes.Bold;

        this.navRow = new HorizontalStackLayout
        {
            Spacing = 18,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center,
            Children = { this.skipLabel, this.backLabel, this.nextLabel }
        };

        var navGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12,
            Margin = new Thickness(0, 12, 0, 0)
        };
        navGrid.Add(this.counterLabel, 0);
        navGrid.Add(this.navRow, 1);

        this.body = new VerticalStackLayout
        {
            Spacing = 0,
            Children = { this.customHost, navGrid }
        };

        this.callout = new TooltipBubble
        {
            Opacity = 0,
            BubbleContent = this.body
        };

        AbsoluteLayout.SetLayoutFlags(this.callout, AbsoluteLayoutFlags.None);
        AbsoluteLayout.SetLayoutBounds(this.callout, new Rect(0, 0, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));
        this.layer.Children.Add(this.callout);
    }


    static Label BuildNavLabel(EventHandler<TappedEventArgs> onTapped)
    {
        var label = new Label { VerticalOptions = LayoutOptions.Center };
        label.SetDynamicResource(Label.FontSizeProperty, ShinyThemeKeys.Type.LabelLargeSize);

        var tap = new TapGestureRecognizer();
        tap.Tapped += onTapped;
        label.GestureRecognizers.Add(tap);
        return label;
    }


    void TeardownChrome()
    {
        if (this.layer is not null)
        {
            if (this.scrim is not null)
            {
                this.layer.Children.Remove(this.scrim);
                this.layer.Children.Remove(this.scrim.ColorProbe);
            }

            if (this.shields is not null)
            {
                foreach (var shield in this.shields)
                    this.layer.Children.Remove(shield);
            }

            if (this.callout is not null)
                this.layer.Children.Remove(this.callout);
        }

        this.scrim = null;
        this.shields = null;
        this.callout = null;
        this.body = null;
        this.customHost = null;
        this.counterLabel = null;
        this.skipLabel = null;
        this.backLabel = null;
        this.nextLabel = null;
        this.navRow = null;
        this.currentHole = Rect.Zero;
    }


    /// <summary>Re-applies everything that is a walkthrough-level setting rather than a per-step one.</summary>
    void ApplyChrome()
    {
        if (this.scrim is not null)
        {
            this.scrim.IsVisible = this.UseOverlay;
            this.scrim.OverlayColor = this.OverlayColor;
            this.scrim.OverlayOpacity = this.OverlayOpacity;
            this.scrim.HoleCornerRadius = this.HighlightCornerRadius;
            this.scrim.RingColor = this.RingColor;
            this.scrim.RingThickness = this.RingThickness;
        }

        if (this.callout is not null)
        {
            // A walkthrough callout is a card in the app's own palette rather than a tooltip bubble, so
            // it takes the medium corner rather than the small one.
            this.callout.CornerToken = ShinyThemeKeys.Shape.CornerMediumRadius;
            this.callout.CornerRadius = this.CalloutCornerRadius;
            this.callout.MaxBubbleWidth = this.MaxCalloutWidth;
        }

        if (this.nextLabel is not null)
            this.nextLabel.Text = this.CurrentStepIndex >= this.StepCount - 1 ? this.FinishText : this.NextText;

        if (this.backLabel is not null)
            this.backLabel.Text = this.BackText;

        if (this.skipLabel is not null)
            this.skipLabel.Text = this.SkipText;
    }


    // ---------------------------------------------------------------------------------------------
    // Per-step configuration
    // ---------------------------------------------------------------------------------------------

    void ConfigureCallout(WalkthroughStep step, int index, int count)
    {
        var view = this.callout;
        if (view is null)
            return;

        // Bare text on live content is unreadable, so a spotlight step without a backdrop falls back to
        // the card rather than rendering something nobody can see.
        var display = step.Display == WalkthroughDisplay.Spotlight && !this.UseOverlay
            ? WalkthroughDisplay.Popover
            : step.Display;

        var carded = display != WalkthroughDisplay.Spotlight;
        var compact = display == WalkthroughDisplay.Tooltip;
        var tailed = display is WalkthroughDisplay.Tooltip or WalkthroughDisplay.Popover;

        view.HasShadow = carded;
        view.BubblePadding = carded
            ? (compact ? new Thickness(12, 8) : new Thickness(16, 14))
            : new Thickness(4, 2);

        if (carded)
        {
            // Unset means "follow the theme", so the token is what changes between the two presets
            // rather than a resolved colour — otherwise a live theme swap would leave the card behind.
            view.FillToken = ShinyThemeKeys.Color.SurfaceContainerHigh;
            view.TextToken = ShinyThemeKeys.Color.OnSurface;
            view.BubbleColor = this.CalloutColor;
            view.TextColor = this.CalloutTextColor;
        }
        else
        {
            view.BubbleColor = Colors.Transparent;
            view.TextToken = ShinyThemeKeys.Color.InverseOnSurface;
            view.TextColor = this.CalloutTextColor;
        }

        view.ShowTail = tailed;
        view.Title = compact ? null : step.Title;
        view.Text = step.Text;

        var custom = step.ResolveContent();
        if (custom is not null && !ReferenceEquals(this.customHost!.Content, custom))
            this.customHost.Content = custom;

        this.customHost!.IsVisible = custom is not null;

        var showNav = this.ShowNavigation && !compact;
        if (this.body is not null && this.body.Children.Count > 1 && this.body.Children[1] is View navGrid)
            navGrid.IsVisible = showNav;

        if (showNav)
            this.ConfigureNav(index, count, carded);
    }


    void ConfigureNav(int index, int count, bool carded)
    {
        // A spotlight's text sits on the scrim, which is dark in both light and dark themes, so the nav
        // takes the inverse pair. Inside a card it takes the ordinary surface pair.
        var muted = carded ? ShinyThemeKeys.Color.OnSurfaceVariant : ShinyThemeKeys.Color.InverseOnSurface;
        var accent = carded ? ShinyThemeKeys.Color.Primary : ShinyThemeKeys.Color.InversePrimary;

        if (this.counterLabel is not null)
        {
            this.counterLabel.Text = $"{index + 1} of {count}";
            this.counterLabel.IsVisible = this.ShowStepCounter && count > 1;
            this.counterLabel.SetDynamicResource(Label.TextColorProperty, muted);
            this.counterLabel.Opacity = carded ? 1 : 0.75;
        }

        if (this.skipLabel is not null)
        {
            this.skipLabel.Text = this.SkipText;
            this.skipLabel.IsVisible = this.ShowSkip && index < count - 1;
            this.skipLabel.SetDynamicResource(Label.TextColorProperty, muted);
            this.skipLabel.Opacity = carded ? 1 : 0.75;
        }

        if (this.backLabel is not null)
        {
            this.backLabel.Text = this.BackText;
            this.backLabel.IsVisible = this.ShowBack && index > 0;
            this.backLabel.SetDynamicResource(Label.TextColorProperty, muted);
            this.backLabel.Opacity = carded ? 1 : 0.75;
        }

        if (this.nextLabel is not null)
        {
            this.nextLabel.Text = index >= count - 1 ? this.FinishText : this.NextText;
            this.nextLabel.SetDynamicResource(Label.TextColorProperty, accent);
        }
    }


    /// <summary>
    /// Lets the callout size itself to the step's content before anything asks where it goes.
    /// </summary>
    /// <remarks>
    /// Placement is solved from the callout's size, and the size is only right once the layout pass has
    /// seen the new content: a Label reports nothing until its platform view exists, which on the first
    /// step it does not. Auto-sizing and giving the dispatcher a turn is what turns a first callout
    /// stuck in the corner into one that lands on its target. The callout is at zero opacity
    /// throughout, so the intermediate position is never seen.
    /// </remarks>
    async Task SettleCalloutAsync()
    {
        var view = this.callout;
        if (view is null)
            return;

        AbsoluteLayout.SetLayoutBounds(view, new Rect(0, 0, AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));

        await WaitForLayoutAsync(view);

        var tcs = new TaskCompletionSource();
        this.Dispatcher.Dispatch(() => tcs.TrySetResult());
        await tcs.Task;
    }


    TooltipPlacement PlaceCallout(WalkthroughStep step, Rect hole, Rect? targetRect, Size container)
    {
        var view = this.callout;
        if (view is null || container.Width <= 0 || container.Height <= 0)
            return TooltipPlacement.Center;

        // Placed against the cut-out where there is one, so the gap is measured from the edge of the
        // lit area rather than from the control inside it.
        Rect? anchor = hole.Width > 0 && hole.Height > 0 ? hole : targetRect;

        var display = step.Display == WalkthroughDisplay.Spotlight && !this.UseOverlay
            ? WalkthroughDisplay.Popover
            : step.Display;
        var tailed = display is WalkthroughDisplay.Tooltip or WalkthroughDisplay.Popover;

        var layout = view.Place(
            anchor,
            container,
            anchor is null ? TooltipPlacement.Center : step.Placement,
            this.CalloutOffset,
            this.ScreenMargin,
            tailed
        );

        AbsoluteLayout.SetLayoutBounds(view, layout.Bubble);
        return layout.Placement;
    }


    /// <summary>
    /// Fences off the backdrop around the cut-out.
    /// </summary>
    /// <remarks>
    /// With no pass-through wanted, one shield covers the lot. With it wanted, four shields frame the
    /// hole and leave it open, so a tap inside reaches the real control while every tap outside is
    /// still the walkthrough's. There is no way to punch a hole in hit testing itself.
    /// </remarks>
    void UpdateShields(WalkthroughStep step, Rect hole, Size container)
    {
        if (this.shields is null)
            return;

        // No backdrop means the app is fully live: catching taps would be the opposite of what
        // UseOverlay="False" asks for.
        if (!this.UseOverlay || container.Width <= 0 || container.Height <= 0)
        {
            foreach (var shield in this.shields)
                shield.IsVisible = false;

            return;
        }

        var passThrough = (step.AllowTargetInteraction || step.AdvanceOnTargetTap)
            && hole.Width > 0
            && hole.Height > 0;

        Rect[] rects;
        if (!passThrough)
        {
            rects = [new Rect(0, 0, container.Width, container.Height), Rect.Zero, Rect.Zero, Rect.Zero];
        }
        else
        {
            var top = Math.Clamp(hole.Top, 0, container.Height);
            var bottom = Math.Clamp(hole.Bottom, 0, container.Height);
            var left = Math.Clamp(hole.Left, 0, container.Width);
            var right = Math.Clamp(hole.Right, 0, container.Width);

            rects =
            [
                new Rect(0, 0, container.Width, top),
                new Rect(0, bottom, container.Width, Math.Max(0, container.Height - bottom)),
                new Rect(0, top, left, Math.Max(0, bottom - top)),
                new Rect(right, top, Math.Max(0, container.Width - right), Math.Max(0, bottom - top))
            ];
        }

        for (var i = 0; i < this.shields.Length; i++)
        {
            var rect = rects[i];
            this.shields[i].IsVisible = rect.Width > 0 && rect.Height > 0;
            AbsoluteLayout.SetLayoutBounds(this.shields[i], rect);
        }
    }


    void OnBackdropTapped(object? sender, TappedEventArgs e)
    {
        if (this.AdvanceOnBackdropTap)
            this.Next();
    }


    void OnNextTapped(object? sender, TappedEventArgs e) => this.Next();

    void OnBackTapped(object? sender, TappedEventArgs e) => this.Back();

    void OnSkipTapped(object? sender, TappedEventArgs e) => this.Skip();


    // ---------------------------------------------------------------------------------------------
    // Motion
    // ---------------------------------------------------------------------------------------------

    async Task FadeScrimAsync(bool visible)
    {
        if (this.scrim is null || !this.UseOverlay)
            return;

        try
        {
            await this.scrim.FadeToAsync(visible ? 1 : 0, 200, visible ? Easing.CubicOut : Easing.CubicIn);
        }
        catch
        {
            // A view detached mid-animation throws rather than completing. Snapped below either way.
        }
        this.scrim.Opacity = visible ? 1 : 0;
    }


    /// <summary>
    /// Slides the cut-out from where it is to where the next step wants it — the travelling spotlight.
    /// </summary>
    Task MoveHoleAsync(Rect hole, WalkthroughStep step)
    {
        if (this.scrim is null)
            return Task.CompletedTask;

        var shape = step.Highlight ?? this.Highlight;
        this.scrim.HoleShape = shape;
        this.scrim.HoleCornerRadius = step.HighlightCornerRadius ?? this.HighlightCornerRadius;

        var from = this.currentHole;

        // Nothing to travel from on the first step (or after a step that had no target): grow out of
        // the destination's own centre instead of sliding in from the top-left corner.
        if (from.Width <= 0 || from.Height <= 0)
            from = new Rect(hole.Center.X, hole.Center.Y, 0, 0);

        // Nothing to travel to: shrink into the current centre rather than collapsing towards the origin.
        if (hole.Width <= 0 || hole.Height <= 0)
            hole = new Rect(from.Center.X, from.Center.Y, 0, 0);

        this.currentHole = hole;
        return this.AnimateHoleAsync(from, hole, this.SpotlightMoveDuration);
    }


    /// <summary>Collapses the spotlight to nothing as the run ends.</summary>
    Task ShrinkHoleAsync()
    {
        if (this.scrim is null || this.currentHole.Width <= 0 || this.currentHole.Height <= 0)
            return Task.CompletedTask;

        var from = this.currentHole;
        var to = new Rect(from.Center.X, from.Center.Y, 0, 0);
        this.currentHole = to;
        return this.AnimateHoleAsync(from, to, Math.Max(120, this.SpotlightMoveDuration / 2));
    }


    Task AnimateHoleAsync(Rect from, Rect to, uint duration)
    {
        var target = this.scrim;
        if (target is null)
            return Task.CompletedTask;

        if (duration == 0)
        {
            target.Hole = to;
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource();
        var animation = new Microsoft.Maui.Controls.Animation(
            v => target.Hole = ViewGeometry.Lerp(from, to, v),
            0,
            1,
            Easing.CubicInOut
        );

        // Named so a move already in flight is replaced rather than fighting the new one — a user
        // clicking Next twice quickly would otherwise leave two animations writing the same property.
        animation.Commit(
            this,
            "shiny-walkthrough-hole",
            16,
            duration,
            finished: (_, _) =>
            {
                target.Hole = to;
                tcs.TrySetResult();
            }
        );

        return tcs.Task;
    }


    async Task AnimateCalloutInAsync(WalkthroughStep step, TooltipPlacement placement)
    {
        var view = this.callout;
        if (view is null)
            return;

        var duration = step.DurationIn;
        view.Opacity = 0;
        view.Scale = 1;
        view.TranslationX = 0;
        view.TranslationY = 0;

        try
        {
            switch (step.AnimationIn)
            {
                case WalkthroughAnimation.None:
                    break;

                case WalkthroughAnimation.Slide:
                    var (dx, dy) = SlideFrom(placement);
                    view.TranslationX = dx;
                    view.TranslationY = dy;
                    await Task.WhenAll(
                        view.FadeToAsync(1, duration, Easing.CubicOut),
                        view.TranslateToAsync(0, 0, duration, Easing.CubicOut)
                    );
                    break;

                case WalkthroughAnimation.Zoom:
                    SetGrowthAnchor(view, placement);
                    view.Scale = ZoomFrom;
                    await Task.WhenAll(
                        view.FadeToAsync(1, duration, Easing.CubicOut),
                        view.ScaleToAsync(1, duration, Easing.CubicOut)
                    );
                    break;

                case WalkthroughAnimation.Pop:
                    SetGrowthAnchor(view, placement);
                    view.Scale = ZoomFrom;
                    await Task.WhenAll(
                        view.FadeToAsync(1, Math.Max(1u, duration / 2), Easing.CubicOut),
                        view.ScaleToAsync(PopOvershoot, Math.Max(1u, (uint)(duration * 0.7)), Easing.CubicOut)
                    );
                    await view.ScaleToAsync(1, Math.Max(1u, (uint)(duration * 0.3)), Easing.CubicIn);
                    break;

                default:
                    await view.FadeToAsync(1, duration, Easing.CubicOut);
                    break;
            }
        }
        catch
        {
            // See FadeScrimAsync.
        }

        view.Opacity = 1;
        view.Scale = 1;
        view.TranslationX = 0;
        view.TranslationY = 0;
    }


    async Task AnimateCalloutOutAsync(WalkthroughStep step)
    {
        var view = this.callout;
        if (view is null)
            return;

        var duration = step.DurationOut;

        try
        {
            switch (step.AnimationOut)
            {
                case WalkthroughAnimation.None:
                    break;

                case WalkthroughAnimation.Slide:
                    await Task.WhenAll(
                        view.FadeToAsync(0, duration, Easing.CubicIn),
                        view.TranslateToAsync(0, SlideDistance, duration, Easing.CubicIn)
                    );
                    break;

                case WalkthroughAnimation.Zoom:
                case WalkthroughAnimation.Pop:
                    await Task.WhenAll(
                        view.FadeToAsync(0, duration, Easing.CubicIn),
                        view.ScaleToAsync(ZoomFrom, duration, Easing.CubicIn)
                    );
                    break;

                default:
                    await view.FadeToAsync(0, duration, Easing.CubicIn);
                    break;
            }
        }
        catch
        {
            // See FadeScrimAsync.
        }

        view.Opacity = 0;
        view.Scale = 1;
        view.TranslationX = 0;
        view.TranslationY = 0;
    }


    static void SetGrowthAnchor(TooltipBubble view, TooltipPlacement placement)
    {
        var alongX = view.Width > 0 ? Math.Clamp(view.TailOffset / view.Width, 0, 1) : 0.5;
        var alongY = view.Height > 0 ? Math.Clamp(view.TailOffset / view.Height, 0, 1) : 0.5;

        (view.AnchorX, view.AnchorY) = placement switch
        {
            TooltipPlacement.Top => (alongX, 1d),
            TooltipPlacement.Bottom => (alongX, 0d),
            TooltipPlacement.Left => (1d, alongY),
            TooltipPlacement.Right => (0d, alongY),
            _ => (0.5, 0.5)
        };
    }


    static (double X, double Y) SlideFrom(TooltipPlacement placement) => placement switch
    {
        TooltipPlacement.Top => (0, SlideDistance),
        TooltipPlacement.Bottom => (0, -SlideDistance),
        TooltipPlacement.Left => (SlideDistance, 0),
        TooltipPlacement.Right => (-SlideDistance, 0),
        _ => (0, SlideDistance)
    };
}
