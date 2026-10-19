using Microsoft.Maui.Controls.Shapes;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls;

/// <summary>
/// A header you tap and content that animates in and out beneath (or above) it.
/// </summary>
/// <remarks>
/// The three motion effects are independent and combine — <see cref="ExpanderAnimation.Height"/> grows
/// the panel so everything below it moves with the reveal, while <see cref="ExpanderAnimation.Fade"/>
/// and <see cref="ExpanderAnimation.Slide"/> act on the content inside that panel. Height is the one
/// that needs a measurement, so it quietly stands down when the expander has not been laid out yet;
/// the rest still run.
/// <para>
/// Drop several into an <see cref="Accordion"/> to get the one-open-at-a-time behaviour.
/// </para>
/// </remarks>
/// <example>
/// <code language="xaml">
/// &lt;shiny:Expander HeaderText="Shipping" HeaderDetail="Arrives Tuesday"
///                 Animation="Height,Slide,Fade" SlideFrom="Top"&gt;
///     &lt;VerticalStackLayout&gt;
///         &lt;Label Text="123 Fake Street" /&gt;
///     &lt;/VerticalStackLayout&gt;
/// &lt;/shiny:Expander&gt;
/// </code>
/// </example>
[ContentProperty(nameof(Content))]
public partial class Expander : Grid
{
    const string HeightAnimationName = "ShinyExpanderHeight";

    /// <summary>
    /// Appended to the expander's own <see cref="Element.AutomationId"/> to name the header row.
    /// The tap gesture lives on that row rather than on the expander, so UI automation driving the
    /// expander's own id finds nothing to tap - this is the id that opens it.
    /// </summary>
    public const string HeaderAutomationIdSuffix = "_Header";

    readonly Border rootBorder;
    readonly Grid layout;
    readonly Grid headerRow;
    readonly ContentView headerHost;
    readonly VerticalStackLayout headerTextStack;
    readonly Label headerLabel;
    readonly Label headerDetailLabel;
    readonly ContentView indicatorHost;
    readonly Label indicatorLabel;
    readonly BoxView separator;
    readonly Grid contentClip;
    readonly ContentView contentHost;

    bool contentRealized;
    bool suppressStateChange;
    int animationToken;

    public Expander()
    {
        this.headerLabel = new Label
        {
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        this.headerDetailLabel = new Label
        {
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            IsVisible = false
        };
        this.headerTextStack = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children = { this.headerLabel, this.headerDetailLabel }
        };

        this.headerHost = new ContentView
        {
            VerticalOptions = LayoutOptions.Center,
            Content = this.headerTextStack
        };

        this.indicatorLabel = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.NoWrap
        };
        this.indicatorHost = new ContentView
        {
            VerticalOptions = LayoutOptions.Center,
            // The glyph is rotated about its own middle, so it needs to be square-ish and centred or a
            // quarter turn visibly shifts it sideways.
            HorizontalOptions = LayoutOptions.Center,
            Content = this.indicatorLabel
        };

        this.headerRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        this.headerRow.Add(this.headerHost, 1);
        this.headerRow.Add(this.indicatorHost, 2);

        var tap = new TapGestureRecognizer();
        tap.Tapped += this.OnHeaderTapped;
        this.headerRow.GestureRecognizers.Add(tap);

        this.separator = new BoxView { HeightRequest = 1, IsVisible = false };

        this.contentHost = new ContentView();
        this.contentClip = new Grid
        {
            // Everything the height animation does depends on this: the panel is shorter than what is
            // inside it for the whole of the reveal, and without the clip that overflow paints anyway.
            IsClippedToBounds = true,
            IsVisible = false,
            Children = { this.contentHost }
        };

        this.layout = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        this.layout.Add(this.headerRow);
        this.layout.Add(this.separator);
        this.layout.Add(this.contentClip);

        this.rootBorder = new Border
        {
            StrokeShape = new RoundRectangle(),
            Padding = 0,
            Content = this.layout
        };

        this.Children.Add(this.rootBorder);

        // Through SetValue, not the AutomationId setter: that throws the second time, and a bound id
        // changes every time a list rebuild re-binds this expander. See AutomationIds.
        AutomationIds.Link(this, this.headerRow, HeaderAutomationIdSuffix);

        this.ApplyLayoutOrder();
        this.RebuildHeader();
        this.ApplyAppearance();

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(Expander));
    }


    /// <summary>The accordion this expander belongs to, when it was declared inside one.</summary>
    public Accordion? Owner { get; internal set; }

    /// <summary>Raised before the content opens. Cancelable.</summary>
    public event EventHandler<ExpanderChangingEventArgs>? Expanding;

    /// <summary>Raised before the content closes. Cancelable.</summary>
    public event EventHandler<ExpanderChangingEventArgs>? Collapsing;

    /// <summary>Raised once the content is open.</summary>
    public event EventHandler<ExpanderEventArgs>? Expanded;

    /// <summary>Raised once the content is closed.</summary>
    public event EventHandler<ExpanderEventArgs>? Collapsed;

    /// <summary>Raised on every state change, opening or closing.</summary>
    public event EventHandler<ExpanderEventArgs>? ExpandedChanged;


    /// <summary>Open the expander.</summary>
    public void Expand() => this.IsExpanded = true;

    /// <summary>Close the expander.</summary>
    public void Collapse() => this.IsExpanded = false;

    /// <summary>Flip the expander between open and closed.</summary>
    public void Toggle() => this.IsExpanded = !this.IsExpanded;


    // ---------------------------------------------------------------------------------------------
    // Header + content composition
    // ---------------------------------------------------------------------------------------------

    void RebuildHeader()
    {
        var header = this.Header;
        if (header == null && this.HeaderTemplate != null)
            header = this.HeaderTemplate.CreateContent() as View;

        // The built-in two-line header is the fallback, and it is the only one whose labels this
        // control owns - a custom header is styled by whoever wrote it.
        this.headerHost.Content = header ?? this.headerTextStack;

        var mode = this.IndicatorMode;
        this.indicatorHost.IsVisible = mode != ExpanderIndicatorMode.None;
        this.indicatorHost.Content = this.IndicatorView ?? this.indicatorLabel;

        var indicatorColumn = this.IndicatorPosition == ExpanderIndicatorPosition.Start ? 0 : 2;
        Grid.SetColumn(this.indicatorHost, indicatorColumn);

        this.ApplyAppearance();
        this.ApplyIndicator(animate: false);
    }


    void RebuildContent()
    {
        this.contentRealized = false;
        this.contentHost.Content = null;

        // Lazy content stays unbuilt until the first expand; anything else is built now so the very
        // first reveal has something to measure.
        if (!this.LoadContentOnDemand || this.IsExpanded)
            this.EnsureContentRealized();
    }


    void EnsureContentRealized()
    {
        if (this.contentRealized)
            return;

        var content = this.Content;
        if (content == null && this.ContentTemplate != null)
            content = this.ContentTemplate.CreateContent() as View;

        this.contentHost.Content = content;
        this.contentRealized = true;
    }


    void ApplyLayoutOrder()
    {
        var down = this.ExpandDirection == ExpandDirection.Down;
        Grid.SetRow(this.headerRow, down ? 0 : 2);
        Grid.SetRow(this.separator, 1);
        Grid.SetRow(this.contentClip, down ? 2 : 0);
    }


    // ---------------------------------------------------------------------------------------------
    // Appearance
    // ---------------------------------------------------------------------------------------------

    void ApplyAppearance()
    {
        // Border
        this.rootBorder.Stroke = this.BorderColor == null
            ? ThemeTokens.TokenBrush(ShinyThemeKeys.Color.OutlineVariant)
            : new SolidColorBrush(this.BorderColor);
        this.rootBorder.SetTokenOrValue(Border.StrokeThicknessProperty, this.BorderThickness, ShinyThemeKeys.Border.Thin);

        if (this.rootBorder.StrokeShape is RoundRectangle shape)
            shape.SetCornerTokenOrValue(this.CornerRadius, ShinyThemeKeys.Shape.CornerMediumRadius);

        if (this.HasShadow)
            this.rootBorder.WithElevation(ShinyThemeKeys.Elevation.Level1);
        else
            // null is how a Shadow is cleared; the property is annotated non-nullable regardless.
            this.rootBorder.Shadow = null!;

        // Header
        SetColorOrToken(this.headerRow, BackgroundColorProperty, this.HeaderBackgroundColor, ShinyThemeKeys.Color.SurfaceContainerLow);
        this.headerRow.Padding = this.HeaderPadding;

        if (ThemeTokens.IsSet(this.HeaderHeight))
        {
            this.headerRow.HeightRequest = this.HeaderHeight;
            this.headerRow.ClearValue(MinimumHeightRequestProperty);
        }
        else
        {
            this.headerRow.ClearValue(HeightRequestProperty);
            this.headerRow.SetDynamicResource(MinimumHeightRequestProperty, ShinyThemeKeys.Density.TouchTarget);
        }

        SetColorOrToken(this.headerLabel, Label.TextColorProperty, this.HeaderTextColor, ShinyThemeKeys.Color.OnSurface);
        this.headerLabel.SetTokenOrValue(Label.FontSizeProperty, this.HeaderFontSize, ShinyThemeKeys.Type.TitleSmallSize);
        this.headerLabel.FontAttributes = this.HeaderFontAttributes;
        this.headerLabel.Text = this.HeaderText;

        SetColorOrToken(this.headerDetailLabel, Label.TextColorProperty, this.HeaderDetailColor, ShinyThemeKeys.Color.OnSurfaceVariant);
        this.headerDetailLabel.WithFontSize(ShinyThemeKeys.Type.BodySmallSize);
        this.headerDetailLabel.Text = this.HeaderDetail;
        this.headerDetailLabel.IsVisible = !String.IsNullOrWhiteSpace(this.HeaderDetail);

        if (this.HeaderFontFamily == null)
        {
            this.headerLabel.SetDynamicResource(Label.FontFamilyProperty, ShinyThemeKeys.Type.FontFamily);
            this.headerDetailLabel.SetDynamicResource(Label.FontFamilyProperty, ShinyThemeKeys.Type.FontFamily);
        }
        else
        {
            this.headerLabel.FontFamily = this.HeaderFontFamily;
            this.headerDetailLabel.FontFamily = this.HeaderFontFamily;
        }

        // Indicator
        SetColorOrToken(this.indicatorLabel, Label.TextColorProperty, this.IndicatorColor, ShinyThemeKeys.Color.OnSurfaceVariant);
        this.indicatorLabel.FontSize = this.IndicatorSize;

        // Separator
        SetColorOrToken(this.separator, BoxView.ColorProperty, this.SeparatorColor, ShinyThemeKeys.Color.OutlineVariant);
        this.separator.IsVisible = this.ShowSeparator && this.contentClip.IsVisible;

        // Content
        SetColorOrToken(this.contentHost, BackgroundColorProperty, this.ContentBackgroundColor, ShinyThemeKeys.Color.Surface);
        this.contentHost.Padding = this.ContentPadding;

        this.ApplySemantics();
    }


    static void SetColorOrToken(Element element, BindableProperty property, Color? color, string themeKey)
    {
        if (color == null)
            element.SetDynamicResource(property, themeKey);
        else
            element.SetValue(property, color);
    }


    void ApplySemantics()
    {
        SemanticProperties.SetDescription(this.headerRow, this.HeaderText ?? String.Empty);
        SemanticProperties.SetHint(this.headerRow, this.IsExpanded ? "Expanded. Activate to collapse." : "Collapsed. Activate to expand.");
    }


    void ApplyIndicator(bool animate)
    {
        var mode = this.IndicatorMode;
        if (mode == ExpanderIndicatorMode.None)
            return;

        if (mode == ExpanderIndicatorMode.Swap)
        {
            this.indicatorLabel.Text = this.IsExpanded ? this.ExpandedIcon : this.CollapsedIcon;
            this.indicatorHost.Rotation = 0;
            return;
        }

        this.indicatorLabel.Text = this.CollapsedIcon;

        // Down-expanding: ▶ turns clockwise into ▼. Up-expanding: it turns the other way, so the open
        // state points at the content wherever the content actually is.
        var target = this.IsExpanded
            ? (this.ExpandDirection == ExpandDirection.Down ? 90d : -90d)
            : 0d;

        if (!animate || this.AnimationDuration == 0 || this.Handler == null)
            this.indicatorHost.Rotation = target;
        else
            _ = this.indicatorHost.RotateToAsync(target, this.AnimationDuration, this.AnimationEasing ?? Easing.CubicOut);
    }


    // ---------------------------------------------------------------------------------------------
    // State
    // ---------------------------------------------------------------------------------------------

    void OnHeaderTapped(object? sender, TappedEventArgs e)
    {
        if (!this.IsToggleEnabled || !this.IsEnabled)
            return;

        // CanCollapse is a tap-level guard only. Code that sets IsExpanded outright still wins, which
        // is what lets an accordion re-point "the one that must stay open" at a different item.
        if (this.IsExpanded && !this.CanCollapse)
            return;

        this.Toggle();
    }


    void OnIsExpandedChanged(bool oldValue, bool newValue)
    {
        this.ApplySemantics();

        if (this.suppressStateChange)
        {
            this.ApplyIndicator(animate: false);
            _ = this.SyncContentAsync(newValue, animate: false);
            return;
        }

        var changing = new ExpanderChangingEventArgs(newValue);
        if (newValue)
            this.Expanding?.Invoke(this, changing);
        else
            this.Collapsing?.Invoke(this, changing);

        if (changing.Cancel)
        {
            this.SetExpandedSilently(oldValue);
            return;
        }

        this.ApplyIndicator(animate: true);
        _ = this.SyncContentAsync(newValue, animate: true);

        if (newValue)
            this.Expanded?.Invoke(this, new ExpanderEventArgs(true));
        else
            this.Collapsed?.Invoke(this, new ExpanderEventArgs(false));

        this.ExpandedChanged?.Invoke(this, new ExpanderEventArgs(newValue));

        var command = this.ExpandedChangedCommand;
        if (command?.CanExecute(newValue) == true)
            command.Execute(newValue);

        this.Owner?.OnItemExpandedChanged(this);
    }


    /// <summary>
    /// Change the state without raising anything. The accordion uses it to close the other items when
    /// one opens — those closes are a consequence of the user's tap, not events of their own.
    /// </summary>
    internal void SetExpandedSilently(bool expanded)
    {
        if (this.IsExpanded == expanded)
            return;

        this.suppressStateChange = true;
        try
        {
            this.IsExpanded = expanded;
        }
        finally
        {
            this.suppressStateChange = false;
        }
        this.ApplyIndicator(animate: true);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        // Nothing was measurable before there was a handler, so land the initial state now - without
        // animation, because an expander that starts open should not be seen opening.
        if (this.Handler != null)
        {
            this.ApplyIndicator(animate: false);
            _ = this.SyncContentAsync(this.IsExpanded, animate: false);
        }
    }


    // ---------------------------------------------------------------------------------------------
    // Reveal
    // ---------------------------------------------------------------------------------------------

    async Task SyncContentAsync(bool expanded, bool animate)
    {
        var token = ++this.animationToken;
        this.AbortAnimation(HeightAnimationName);

        if (expanded)
            this.EnsureContentRealized();

        var animation = this.Animation;
        var duration = this.AnimationDuration;
        var easing = this.AnimationEasing ?? Easing.CubicOut;

        var shouldAnimate = animate
            && animation != ExpanderAnimation.None
            && duration > 0
            && this.Handler != null;

        if (!shouldAnimate)
        {
            this.Settle(expanded);
            return;
        }

        var (dx, dy) = this.SlideOffset();
        var useSlide = animation.HasFlag(ExpanderAnimation.Slide) && (dx != 0 || dy != 0);
        var useFade = animation.HasFlag(ExpanderAnimation.Fade);

        // Height is the only effect that needs a number out of the layout, and on the very first
        // reveal there may not be one yet. When that happens it stands down rather than snapping the
        // panel to a guess - fade and slide still carry the transition.
        var measured = animation.HasFlag(ExpanderAnimation.Height) ? this.MeasureContentHeight() : -1d;
        var useHeight = measured > 0;

        this.contentClip.IsVisible = true;
        this.separator.IsVisible = this.ShowSeparator;

        var tasks = new List<Task>();

        if (expanded)
        {
            if (useHeight)
                this.contentClip.HeightRequest = 0;

            if (useFade)
                this.contentHost.Opacity = 0;

            if (useSlide)
            {
                this.contentHost.TranslationX = dx;
                this.contentHost.TranslationY = dy;
            }

            if (useHeight)
                tasks.Add(this.AnimateClipHeightAsync(0, measured, duration, easing));
            if (useFade)
                tasks.Add(this.contentHost.FadeToAsync(1, duration, easing));
            if (useSlide)
                tasks.Add(this.contentHost.TranslateToAsync(0, 0, duration, easing));
        }
        else
        {
            var from = this.contentClip.Height > 0 ? this.contentClip.Height : measured;
            if (useHeight && from > 0)
                tasks.Add(this.AnimateClipHeightAsync(from, 0, duration, easing));
            if (useFade)
                tasks.Add(this.contentHost.FadeToAsync(0, duration, easing));
            if (useSlide)
                tasks.Add(this.contentHost.TranslateToAsync(dx, dy, duration, easing));
        }

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(true);
        }
        catch (Exception)
        {
            // Torn down mid-flight (page popped, handler disconnected). The settle below still runs so
            // the panel is never left frozen half-open.
        }

        // A newer toggle already owns the panel - it will do its own settling.
        if (token != this.animationToken)
            return;

        this.Settle(expanded);
    }


    /// <summary>Put the panel in its resting state for <paramref name="expanded"/>, with no animation.</summary>
    void Settle(bool expanded)
    {
        this.contentClip.IsVisible = expanded;
        // Back to auto: the panel has to track its content again once the reveal is over, or anything
        // that grows inside it later is clipped for good.
        this.contentClip.HeightRequest = -1;
        this.contentHost.Opacity = 1;
        this.contentHost.TranslationX = 0;
        this.contentHost.TranslationY = 0;
        this.separator.IsVisible = expanded && this.ShowSeparator;
    }


    Task AnimateClipHeightAsync(double from, double to, uint duration, Easing easing)
    {
        var tcs = new TaskCompletionSource<bool>();
        var animation = new Microsoft.Maui.Controls.Animation(
            v => this.contentClip.HeightRequest = v,
            from,
            to,
            easing
        );
        animation.Commit(this, HeightAnimationName, 16, duration, finished: (_, _) => tcs.TrySetResult(true));
        return tcs.Task;
    }


    double MeasureContentHeight()
    {
        var width = this.contentClip.Width > 0
            ? this.contentClip.Width
            : this.Width - (this.rootBorder.StrokeThickness * 2);

        if (width <= 0 || Double.IsNaN(width) || Double.IsInfinity(width))
            return -1;

        var size = ((IView)this.contentHost).Measure(width, Double.PositiveInfinity);
        return size.Height > 0 ? size.Height : -1;
    }


    (double X, double Y) SlideOffset()
    {
        // The travel is a fraction of what is being revealed rather than the whole of it: the content
        // should look like it is settling into place, not flying in from off-screen.
        var height = this.contentClip.Height > 0 ? this.contentClip.Height : 48;
        var width = this.Width > 0 ? this.Width : 240;

        return this.SlideFrom switch
        {
            ExpanderSlideFrom.Top => (0, -Math.Min(height, 64)),
            ExpanderSlideFrom.Bottom => (0, Math.Min(height, 64)),
            ExpanderSlideFrom.Left => (-Math.Min(width, 96), 0),
            ExpanderSlideFrom.Right => (Math.Min(width, 96), 0),
            _ => (0, 0)
        };
    }
}
