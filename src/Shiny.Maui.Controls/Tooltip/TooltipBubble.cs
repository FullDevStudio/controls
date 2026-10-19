using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls;

/// <summary>
/// The bubble a <see cref="Tooltip"/> and a <see cref="Walkthrough"/> callout are both made of: a
/// rounded card, an optional tail that points back at whatever the bubble is about, and either
/// title/text or arbitrary content.
/// </summary>
/// <remarks>
/// Split out from both controls because the fiddly part of a tooltip is not the popup logic, it is
/// getting a tail to stay attached to a card that has been clamped away from the thing it points at.
/// The card is a <see cref="Border"/> so it gets real rounded corners and a shadow; the tail is drawn
/// on a <see cref="GraphicsView"/> so it renders identically on every head, AppKit and GTK4 included.
/// </remarks>
public class TooltipBubble : Grid
{
    readonly Border card;
    readonly GraphicsView tail;
    readonly TooltipTailDrawable tailDrawable;
    readonly VerticalStackLayout body;
    readonly Label titleLabel;
    readonly Label textLabel;
    readonly ContentView contentHost;
    readonly SolidColorBrush fillBrush;
    readonly BoxView fillProbe;
    readonly SolidColorBrush strokeBrush;
    readonly BoxView strokeProbe;
    readonly Shadow shadow;

    public TooltipBubble()
    {
        (this.fillBrush, this.fillProbe) = ThemeProbe.Create();
        (this.strokeBrush, this.strokeProbe) = ThemeProbe.Create();

        this.titleLabel = new Label { IsVisible = false, LineBreakMode = LineBreakMode.WordWrap };
        this.titleLabel.SetDynamicResource(Label.FontSizeProperty, ShinyThemeKeys.Type.TitleSmallSize);
        this.titleLabel.FontAttributes = FontAttributes.Bold;

        this.textLabel = new Label { IsVisible = false, LineBreakMode = LineBreakMode.WordWrap };
        this.textLabel.SetDynamicResource(Label.FontSizeProperty, ShinyThemeKeys.Type.BodySmallSize);

        this.contentHost = new ContentView { IsVisible = false };

        this.body = new VerticalStackLayout
        {
            Spacing = 4,
            Children = { this.titleLabel, this.textLabel, this.contentHost }
        };

        // Shadow.Brush is Brush-typed, so it needs the same probe treatment as the stroke.
        var (shadowBrush, shadowProbe) = ThemeProbe.Create();
        shadowProbe.SetDynamicResource(BoxView.ColorProperty, ShinyThemeKeys.Color.Shadow);

        // One Shadow instance, assigned once and never swapped. Reassigning VisualElement.Shadow tears
        // the native layer down and back up, which on Android drops focus out of anything inside it —
        // so HasShadow toggles this instance's Opacity rather than the property.
        this.shadow = new Shadow
        {
            Brush = shadowBrush,
            Opacity = 0.25f,
            Radius = 8,
            Offset = new Point(0, 3)
        };

        this.card = new Border
        {
            Background = this.fillBrush,
            Stroke = this.strokeBrush,
            StrokeThickness = 0,
            Padding = new Thickness(12, 8),
            StrokeShape = new RoundRectangle().WithCornerRadius(ShinyThemeKeys.Shape.CornerSmallRadius),
            Shadow = this.shadow,
            Content = this.body
        };

        this.tailDrawable = new TooltipTailDrawable(this);
        this.tail = new GraphicsView
        {
            Drawable = this.tailDrawable,
            InputTransparent = true,
            BackgroundColor = Colors.Transparent
        };

        this.Add(this.card);
        this.Add(this.tail);
        this.Add(this.fillProbe);
        this.Add(this.strokeProbe);
        this.Add(shadowProbe);

        // The tail is painted from the probes' resolved colours, and a probe resolves asynchronously —
        // once when the theme dictionary first merges, and again on every ShinyThemeManager.SetTheme.
        // Without this the tail keeps whatever colour it was first drawn with while the card follows
        // the theme, which shows up as a tail in the wrong colour after a light/dark switch.
        this.fillProbe.PropertyChanged += this.OnProbeChanged;
        this.strokeProbe.PropertyChanged += this.OnProbeChanged;

        this.ApplyFill();
        this.ApplyStroke();
        this.ApplyTextColors();
        this.ApplyPlacement();

        // Last line: replays any styled property that was applied before the children existed.
        // See StyleGuard.
        StyleGuard.MarkReady(this, typeof(TooltipBubble));
    }


    static void Restyle(BindableObject b, object o, object n)
        => StyleGuard.WhenReady(b, typeof(TooltipBubble), () => ((TooltipBubble)b).ApplyAll());

    static void Relayout(BindableObject b, object o, object n)
        => StyleGuard.WhenReady(b, typeof(TooltipBubble), () => ((TooltipBubble)b).ApplyPlacement());


    // ---------------------------------------------------------------------------------------------
    // Content
    // ---------------------------------------------------------------------------------------------

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(TooltipBubble), null, propertyChanged: Restyle);

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text), typeof(string), typeof(TooltipBubble), null, propertyChanged: Restyle);

    public static readonly BindableProperty BubbleContentProperty = BindableProperty.Create(
        nameof(BubbleContent), typeof(View), typeof(TooltipBubble), null, propertyChanged: Restyle);

    /// <summary>Optional heading above <see cref="Text"/>.</summary>
    public string? Title
    {
        get => (string?)this.GetValue(TitleProperty);
        set => this.SetValue(TitleProperty, value);
    }

    public string? Text
    {
        get => (string?)this.GetValue(TextProperty);
        set => this.SetValue(TextProperty, value);
    }

    /// <summary>Arbitrary content, shown below the title and text (or instead of them, if both are unset).</summary>
    public View? BubbleContent
    {
        get => (View?)this.GetValue(BubbleContentProperty);
        set => this.SetValue(BubbleContentProperty, value);
    }


    // ---------------------------------------------------------------------------------------------
    // Shape and colour
    // ---------------------------------------------------------------------------------------------

    public static readonly BindableProperty PlacementProperty = BindableProperty.Create(
        nameof(Placement), typeof(TooltipPlacement), typeof(TooltipBubble), TooltipPlacement.Top,
        propertyChanged: Relayout);

    public static readonly BindableProperty ShowTailProperty = BindableProperty.Create(
        nameof(ShowTail), typeof(bool), typeof(TooltipBubble), true, propertyChanged: Relayout);

    public static readonly BindableProperty TailSizeProperty = BindableProperty.Create(
        nameof(TailSize), typeof(double), typeof(TooltipBubble), 7d, propertyChanged: Relayout);

    public static readonly BindableProperty TailOffsetProperty = BindableProperty.Create(
        nameof(TailOffset), typeof(double), typeof(TooltipBubble), 0d, propertyChanged: Relayout);

    public static readonly BindableProperty BubbleColorProperty = BindableProperty.Create(
        nameof(BubbleColor), typeof(Color), typeof(TooltipBubble), null, propertyChanged: Restyle);

    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(
        nameof(BorderColor), typeof(Color), typeof(TooltipBubble), null, propertyChanged: Restyle);

    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(
        nameof(BorderThickness), typeof(double), typeof(TooltipBubble), 0d, propertyChanged: Restyle);

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor), typeof(Color), typeof(TooltipBubble), null, propertyChanged: Restyle);

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(double), typeof(TooltipBubble), ThemeTokens.Unset, propertyChanged: Restyle);

    public static readonly BindableProperty BubblePaddingProperty = BindableProperty.Create(
        nameof(BubblePadding), typeof(Thickness), typeof(TooltipBubble), new Thickness(12, 8),
        propertyChanged: Restyle);

    public static readonly BindableProperty MaxBubbleWidthProperty = BindableProperty.Create(
        nameof(MaxBubbleWidth), typeof(double), typeof(TooltipBubble), 280d, propertyChanged: Restyle);

    public static readonly BindableProperty HasShadowProperty = BindableProperty.Create(
        nameof(HasShadow), typeof(bool), typeof(TooltipBubble), true, propertyChanged: Restyle);

    /// <summary>Which side of the target the bubble is on — the tail points the opposite way.</summary>
    public TooltipPlacement Placement
    {
        get => (TooltipPlacement)this.GetValue(PlacementProperty);
        set => this.SetValue(PlacementProperty, value);
    }

    /// <summary>Draw the pointer back at the target. Always off for <see cref="TooltipPlacement.Center"/>.</summary>
    public bool ShowTail
    {
        get => (bool)this.GetValue(ShowTailProperty);
        set => this.SetValue(ShowTailProperty, value);
    }

    /// <summary>How far the tail sticks out. Its base is twice this.</summary>
    public double TailSize
    {
        get => (double)this.GetValue(TailSizeProperty);
        set => this.SetValue(TailSizeProperty, value);
    }

    /// <summary>
    /// Where along the bubble's leading edge the tail sits, from
    /// <see cref="TooltipPlacementSolver"/>. Set by the owning control after it has placed the bubble.
    /// </summary>
    public double TailOffset
    {
        get => (double)this.GetValue(TailOffsetProperty);
        set => this.SetValue(TailOffsetProperty, value);
    }

    /// <summary>Leave unset to follow the theme's inverse surface, which is the tooltip convention.</summary>
    public Color? BubbleColor
    {
        get => (Color?)this.GetValue(BubbleColorProperty);
        set => this.SetValue(BubbleColorProperty, value);
    }

    public Color? BorderColor
    {
        get => (Color?)this.GetValue(BorderColorProperty);
        set => this.SetValue(BorderColorProperty, value);
    }

    public double BorderThickness
    {
        get => (double)this.GetValue(BorderThicknessProperty);
        set => this.SetValue(BorderThicknessProperty, value);
    }

    public Color? TextColor
    {
        get => (Color?)this.GetValue(TextColorProperty);
        set => this.SetValue(TextColorProperty, value);
    }

    /// <summary>Leave unset (negative) to follow the theme's corner token.</summary>
    public double CornerRadius
    {
        get => (double)this.GetValue(CornerRadiusProperty);
        set => this.SetValue(CornerRadiusProperty, value);
    }

    public Thickness BubblePadding
    {
        get => (Thickness)this.GetValue(BubblePaddingProperty);
        set => this.SetValue(BubblePaddingProperty, value);
    }

    /// <summary>Ceiling on the card's width, so long text wraps instead of spanning the screen.</summary>
    public double MaxBubbleWidth
    {
        get => (double)this.GetValue(MaxBubbleWidthProperty);
        set => this.SetValue(MaxBubbleWidthProperty, value);
    }

    public bool HasShadow
    {
        get => (bool)this.GetValue(HasShadowProperty);
        set => this.SetValue(HasShadowProperty, value);
    }


    /// <summary>The card itself, so an owner can animate it independently of the tail.</summary>
    internal Border Card => this.card;

    /// <summary>The title and text labels, so an owner can name them for UI automation.</summary>
    internal Label TitleLabel => this.titleLabel;

    internal Label TextLabel => this.textLabel;

    /// <summary>
    /// Which theme token the fill falls back to when <see cref="BubbleColor"/> is unset. A tooltip
    /// wants the inverse surface (light bubble on a dark app, and the other way round); a walkthrough
    /// callout is a card in the app's own palette and wants a surface container. Setting a token rather
    /// than a colour is what keeps both following a live theme swap.
    /// </summary>
    internal string FillToken
    {
        get => this.fillToken;
        set
        {
            if (this.fillToken == value)
                return;

            this.fillToken = value;
            this.ApplyFill();
        }
    }

    /// <summary>The token the title and text fall back to when <see cref="TextColor"/> is unset.</summary>
    internal string TextToken
    {
        get => this.textToken;
        set
        {
            if (this.textToken == value)
                return;

            this.textToken = value;
            this.ApplyTextColors();
        }
    }

    /// <summary>The token the corner radius falls back to when <see cref="CornerRadius"/> is unset.</summary>
    internal string CornerToken
    {
        get => this.cornerToken;
        set
        {
            if (this.cornerToken == value)
                return;

            this.cornerToken = value;
            this.ApplyMetrics();
        }
    }

    string fillToken = ShinyThemeKeys.Color.InverseSurface;
    string textToken = ShinyThemeKeys.Color.InverseOnSurface;
    string cornerToken = ShinyThemeKeys.Shape.CornerSmallRadius;

    internal Color ResolvedFill => this.fillProbe.Color ?? Colors.Black;

    internal Color ResolvedStroke => this.strokeProbe.Color ?? Colors.Transparent;


    /// <summary>
    /// Works out where this bubble goes against <paramref name="target"/> and applies the side and tail
    /// offset to itself, returning the layout so the caller can position it.
    /// </summary>
    /// <remarks>
    /// Solved twice on purpose. A bubble's size depends on which side it is on — the tail is vertical
    /// for Top/Bottom and horizontal for Left/Right — and the side depends on the size, so something
    /// has to break the loop. The first pass uses the current size padded by the tail on both axes,
    /// purely to choose a side; the second re-solves against the real measured size for that side, so
    /// the gap and the tail line up exactly rather than approximately.
    /// </remarks>
    internal TooltipLayout Place(
        Rect? target,
        Size container,
        TooltipPlacement preferred,
        double gap,
        double margin,
        bool showTail
    )
    {
        var maxW = Math.Max(1, container.Width - (margin * 2));
        var maxH = Math.Max(1, container.Height - (margin * 2));

        if (target is null || preferred == TooltipPlacement.Center)
        {
            this.ShowTail = false;
            this.Placement = TooltipPlacement.Center;
            var size = this.MeasureBubble(maxW, maxH);
            return TooltipPlacementSolver.Solve(
                new Rect(container.Width / 2, container.Height / 2, 0, 0),
                size, container, TooltipPlacement.Center, gap, margin
            );
        }

        // A radius that is following the theme has no number here yet, so the inset falls back to a
        // value large enough for the biggest corner token - the tail sitting slightly further in than
        // it strictly needs is invisible; one crossing a rounded corner is not.
        var tailInset = (ThemeTokens.IsSet(this.CornerRadius) ? this.CornerRadius : 16) + this.TailSize;

        this.ShowTail = showTail;
        var estimate = new Size(this.Width + this.TailSize, this.Height + this.TailSize);
        var first = TooltipPlacementSolver.Solve(target.Value, estimate, container, preferred, gap, margin, tailInset);

        this.Placement = first.Placement;
        var real = this.MeasureBubble(maxW, maxH);

        var final = TooltipPlacementSolver.Solve(target.Value, real, container, first.Placement, gap, margin, tailInset);
        this.TailOffset = final.TailOffset;
        return final;
    }


    /// <summary>
    /// The bubble's desired size, preferring a fresh measure and falling back to the size it was last
    /// arranged at.
    /// </summary>
    /// <remarks>
    /// Measure returns zero for a view whose handler does not exist yet, and a Label has no size at all
    /// until its platform view does. That happens on the very first show, which is exactly when getting
    /// it wrong is most visible - the bubble lands in the corner. The arranged size is stale by one
    /// content change but never zero once the bubble has been on screen, so it is the better guess.
    /// </remarks>
    Size MeasureBubble(double maxWidth, double maxHeight)
    {
        var measured = ((IView)this).Measure(maxWidth, maxHeight);
        if (measured.Width > 0 && measured.Height > 0)
            return measured;

        return new Size(this.Width, this.Height);
    }


    void OnProbeChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == BoxView.ColorProperty.PropertyName)
            this.tail.Invalidate();
    }


    void ApplyAll()
    {
        this.ApplyFill();
        this.ApplyStroke();
        this.ApplyTextColors();
        this.ApplyContent();
        this.ApplyMetrics();
        this.tail.Invalidate();
    }


    void ApplyFill()
    {
        ThemeProbe.Tint(this.fillProbe, BoxView.ColorProperty, this.BubbleColor, this.fillToken);
        this.tail.Invalidate();
    }


    void ApplyStroke()
    {
        ThemeProbe.Tint(this.strokeProbe, BoxView.ColorProperty, this.BorderColor, ShinyThemeKeys.Color.OutlineVariant);
        this.card.StrokeThickness = this.BorderThickness;
        this.tail.Invalidate();
    }


    void ApplyTextColors()
    {
        if (this.TextColor is null)
        {
            this.titleLabel.SetDynamicResource(Label.TextColorProperty, this.textToken);
            this.textLabel.SetDynamicResource(Label.TextColorProperty, this.textToken);
        }
        else
        {
            this.titleLabel.RemoveDynamicResource(Label.TextColorProperty);
            this.textLabel.RemoveDynamicResource(Label.TextColorProperty);
            this.titleLabel.TextColor = this.TextColor;
            this.textLabel.TextColor = this.TextColor;
        }
    }


    void ApplyContent()
    {
        this.titleLabel.Text = this.Title;
        this.titleLabel.IsVisible = !string.IsNullOrWhiteSpace(this.Title);

        this.textLabel.Text = this.Text;
        this.textLabel.IsVisible = !string.IsNullOrWhiteSpace(this.Text);

        if (!ReferenceEquals(this.contentHost.Content, this.BubbleContent))
            this.contentHost.Content = this.BubbleContent;

        this.contentHost.IsVisible = this.BubbleContent is not null;
    }


    void ApplyMetrics()
    {
        this.card.Padding = this.BubblePadding;
        this.card.MaximumWidthRequest = this.MaxBubbleWidth;

        if (this.card.StrokeShape is RoundRectangle rounded)
            rounded.SetCornerTokenOrValue(this.CornerRadius, this.cornerToken);

        // Toggle the existing shadow rather than assigning a new one — see the constructor.
        this.shadow.Opacity = this.HasShadow ? 0.25f : 0f;
    }


    /// <summary>
    /// Rebuilds the grid so the tail lands on the correct edge, then slides it along that edge to
    /// <see cref="TailOffset"/>. The tail overlaps the card by a point so no hairline seam shows
    /// between two separately-rasterized surfaces.
    /// </summary>
    void ApplyPlacement()
    {
        this.ApplyContent();
        this.ApplyMetrics();

        this.RowDefinitions.Clear();
        this.ColumnDefinitions.Clear();

        var placement = this.Placement;
        var visible = this.ShowTail && placement is not TooltipPlacement.Center and not TooltipPlacement.Auto;
        this.tail.IsVisible = visible;

        var size = Math.Max(1, this.TailSize);
        var along = this.TailOffset - size;

        this.tail.HorizontalOptions = LayoutOptions.Start;
        this.tail.VerticalOptions = LayoutOptions.Start;

        switch (placement)
        {
            // Bubble above the target: tail on the bottom edge, pointing down.
            case TooltipPlacement.Top:
                this.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                this.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(this.card, 0);
                Grid.SetRow(this.tail, 1);
                this.tail.WidthRequest = size * 2;
                this.tail.HeightRequest = size;
                this.tail.Margin = new Thickness(Math.Max(0, along), -1, 0, 0);
                this.tailDrawable.Direction = TooltipPlacement.Bottom;
                break;

            case TooltipPlacement.Bottom:
                this.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                this.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                Grid.SetRow(this.tail, 0);
                Grid.SetRow(this.card, 1);
                this.tail.WidthRequest = size * 2;
                this.tail.HeightRequest = size;
                this.tail.Margin = new Thickness(Math.Max(0, along), 0, 0, -1);
                this.tailDrawable.Direction = TooltipPlacement.Top;
                break;

            case TooltipPlacement.Left:
                this.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                this.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                Grid.SetColumn(this.card, 0);
                Grid.SetColumn(this.tail, 1);
                this.tail.WidthRequest = size;
                this.tail.HeightRequest = size * 2;
                this.tail.Margin = new Thickness(-1, Math.Max(0, along), 0, 0);
                this.tailDrawable.Direction = TooltipPlacement.Right;
                break;

            case TooltipPlacement.Right:
                this.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                this.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
                Grid.SetColumn(this.tail, 0);
                Grid.SetColumn(this.card, 1);
                this.tail.WidthRequest = size;
                this.tail.HeightRequest = size * 2;
                this.tail.Margin = new Thickness(0, Math.Max(0, along), -1, 0);
                this.tailDrawable.Direction = TooltipPlacement.Left;
                break;

            default:
                Grid.SetRow(this.card, 0);
                Grid.SetColumn(this.card, 0);
                Grid.SetRow(this.tail, 0);
                Grid.SetColumn(this.tail, 0);
                break;
        }

        this.tail.Invalidate();
    }


    /// <summary>Draws the pointer, filled and stroked to match the card it is glued to.</summary>
    sealed class TooltipTailDrawable(TooltipBubble owner) : IDrawable
    {
        /// <summary>Which way the point faces — the opposite of the bubble's placement.</summary>
        public TooltipPlacement Direction { get; set; } = TooltipPlacement.Bottom;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var w = dirtyRect.Width;
            var h = dirtyRect.Height;
            if (w <= 0 || h <= 0)
                return;

            var path = new PathF();
            switch (this.Direction)
            {
                case TooltipPlacement.Bottom: // points down
                    path.MoveTo(0, 0);
                    path.LineTo(w, 0);
                    path.LineTo(w / 2, h);
                    break;

                case TooltipPlacement.Top: // points up
                    path.MoveTo(0, h);
                    path.LineTo(w, h);
                    path.LineTo(w / 2, 0);
                    break;

                case TooltipPlacement.Right: // points right
                    path.MoveTo(0, 0);
                    path.LineTo(0, h);
                    path.LineTo(w, h / 2);
                    break;

                default: // points left
                    path.MoveTo(w, 0);
                    path.LineTo(w, h);
                    path.LineTo(0, h / 2);
                    break;
            }
            path.Close();

            canvas.FillColor = owner.ResolvedFill;
            canvas.FillPath(path);

            if (owner.BorderThickness > 0)
            {
                canvas.StrokeColor = owner.ResolvedStroke;
                canvas.StrokeSize = (float)owner.BorderThickness;
                canvas.DrawPath(path);
            }
        }
    }
}
