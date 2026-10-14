using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.MotionIcons;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls;

/// <summary>
/// A rounded card whose border is animated - the base of <see cref="BorderBeam"/> and
/// <see cref="ShineBorder"/>. It holds one <see cref="Content"/>, clips itself (and so its
/// <c>Background</c> and content) to <see cref="CornerRadius"/>, and draws a static track plus the
/// animated effect on top.
/// </summary>
/// <remarks>
/// <para>
/// The effect is drawn on a <see cref="GraphicsView"/> laid over the content, so it renders the same on
/// every head - AppKit and GTK4 included - and the content can never paint over it. The canvas is
/// input-transparent: taps go straight through to the content.
/// </para>
/// <para>
/// Frames come from the window's shared frame ticker (one timer for every border, marquee and motion
/// icon on the window) and only while the control is loaded, running and the device has not asked for
/// reduced motion. Content is inset by <c>Padding</c> plus <see cref="BorderWidth"/>.
/// </para>
/// </remarks>
[ContentProperty(nameof(Content))]
public abstract class AnimatedBorder : Layout
{
    readonly GraphicsView canvas;
    MotionTicker? ticker;
    BorderPath path = BorderPath.Empty;
    Size pathSize;
    double pathRadius = -1;
    double pathWidth = -1;
    readonly RoundRectangleGeometry clip = new();
    Size clipSize;
    double clipRadius = -1;
    bool reducedMotion;

    private protected AnimatedBorder()
    {
        this.canvas = new GraphicsView
        {
            Drawable = new Painter(this),
            InputTransparent = true,
            BackgroundColor = Colors.Transparent
        };
        this.Children.Add(this.canvas);

        // ⚠️ The clip is installed once, here, and only its rectangle changes afterwards. On Android the
        // first non-null Clip wraps the native view in a clipping container; doing that from inside
        // ArrangeChildren (mid native layout pass) left the container at a stale size, so a card drew
        // cut off part-way down - or not at all - while iOS, which clips its own layer, looked fine.
        this.Clip = this.clip;

        // The track colour is drawn, not bound, so the token is resolved through this element and the
        // canvas repaints when a light/dark switch swaps it.
        this.SetDynamicResource(ThemeTrackColorProperty, ShinyThemeKeys.Color.OutlineVariant);

        this.Loaded += (_, _) =>
        {
            this.reducedMotion = this.RespectReducedMotion && ReducedMotion.IsEnabled;
            this.UpdateRunning();
        };
        this.Unloaded += (_, _) => this.StopTicker();

        StyleGuard.MarkReady(this, typeof(AnimatedBorder));
    }


    // ---------------------------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------------------------

    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(View), typeof(AnimatedBorder), null,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(AnimatedBorder), () => ((AnimatedBorder)b).OnContentChanged((View?)o, (View?)n)));

    /// <summary>What sits inside the border.</summary>
    public View? Content
    {
        get => (View?)this.GetValue(ContentProperty);
        set => this.SetValue(ContentProperty, value);
    }

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(double), typeof(AnimatedBorder), 12d,
        propertyChanged: (b, _, _) => ((AnimatedBorder)b).OnShapeChanged());

    /// <summary>The radius of the outer corners. Content and background are clipped to it. Default 12.</summary>
    public double CornerRadius
    {
        get => (double)this.GetValue(CornerRadiusProperty);
        set => this.SetValue(CornerRadiusProperty, value);
    }

    public static readonly BindableProperty BorderWidthProperty = BindableProperty.Create(
        nameof(BorderWidth), typeof(double), typeof(AnimatedBorder), 1d,
        propertyChanged: (b, _, _) => ((AnimatedBorder)b).OnShapeChanged());

    /// <summary>How thick the border is - both the track and the animation. Default 1.</summary>
    public double BorderWidth
    {
        get => (double)this.GetValue(BorderWidthProperty);
        set => this.SetValue(BorderWidthProperty, value);
    }

    public static readonly BindableProperty TrackColorProperty = BindableProperty.Create(
        nameof(TrackColor), typeof(Color), typeof(AnimatedBorder), null,
        propertyChanged: (b, _, _) => ((AnimatedBorder)b).Invalidate());

    /// <summary>
    /// The static border the animation runs over. Unset follows the theme's outline-variant colour;
    /// <c>Transparent</c> leaves only the animation.
    /// </summary>
    public Color? TrackColor
    {
        get => (Color?)this.GetValue(TrackColorProperty);
        set => this.SetValue(TrackColorProperty, value);
    }

    public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(
        nameof(IsRunning), typeof(bool), typeof(AnimatedBorder), true,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(AnimatedBorder), () => ((AnimatedBorder)b).UpdateRunning()));

    /// <summary>Whether it animates. False holds the effect where it is.</summary>
    public bool IsRunning
    {
        get => (bool)this.GetValue(IsRunningProperty);
        set => this.SetValue(IsRunningProperty, value);
    }

    public static readonly BindableProperty RespectReducedMotionProperty = BindableProperty.Create(
        nameof(RespectReducedMotion), typeof(bool), typeof(AnimatedBorder), true,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(AnimatedBorder), () =>
        {
            var border = (AnimatedBorder)b;
            border.reducedMotion = border.RespectReducedMotion && ReducedMotion.IsEnabled;
            border.UpdateRunning();
        }));

    /// <summary>Holds still when the device asks for reduced motion. On by default.</summary>
    public bool RespectReducedMotion
    {
        get => (bool)this.GetValue(RespectReducedMotionProperty);
        set => this.SetValue(RespectReducedMotionProperty, value);
    }

    /// <summary>The theme's outline-variant colour, resolved through this element. Read only while <see cref="TrackColor"/> is unset.</summary>
    internal static readonly BindableProperty ThemeTrackColorProperty = BindableProperty.Create(
        "ThemeTrackColor", typeof(Color), typeof(AnimatedBorder), Color.FromArgb("#C4C6D0"),
        propertyChanged: (b, _, _) => ((AnimatedBorder)b).Invalidate());


    // ---------------------------------------------------------------------------------------------
    // Subclass surface
    // ---------------------------------------------------------------------------------------------

    /// <summary>How long the animation has been running. Advances only while it is running.</summary>
    internal TimeSpan Elapsed { get; private set; }

    /// <summary>Whether the frame ticker is currently driving it - a test seam.</summary>
    internal bool IsTicking => this.ticker is not null;

    /// <summary>The resolved track colour.</summary>
    internal Color ResolvedTrackColor => this.TrackColor ?? (Color)this.GetValue(ThemeTrackColorProperty);

    /// <summary>Draws the animated part of the border along <paramref name="path"/>.</summary>
    internal abstract void DrawEffect(ICanvas canvas, BorderPath path, float width, float height);

    /// <summary>Repaints. Subclasses call it when an appearance property changes.</summary>
    protected void Invalidate() => this.canvas?.Invalidate();

    /// <summary>
    /// How far through one loop of <paramref name="duration"/> the animation is, from 0 up to (not
    /// including) 1, with <paramref name="lead"/> already run - Magic UI's (negative) delay.
    /// </summary>
    internal double LoopProgress(TimeSpan duration, TimeSpan lead)
    {
        if (duration <= TimeSpan.Zero)
            return 0;

        var p = (this.Elapsed + lead).TotalMilliseconds / duration.TotalMilliseconds;
        p -= Math.Floor(p);
        return p;
    }

    /// <summary>The path the current frame is drawn on, rebuilt only when the box or the border changes.</summary>
    internal BorderPath PathFor(double width, double height)
    {
        var size = new Size(width, height);
        if (size != this.pathSize || this.pathRadius != this.CornerRadius || this.pathWidth != this.BorderWidth)
        {
            this.pathSize = size;
            this.pathRadius = this.CornerRadius;
            this.pathWidth = this.BorderWidth;
            this.path = BorderPath.Create(width, height, this.CornerRadius, this.BorderWidth);
        }
        return this.path;
    }


    /// <summary>Test seam and the ticker's callback: move the animation on by real time.</summary>
    internal void Advance(TimeSpan delta)
    {
        this.Elapsed += delta;
        this.Invalidate();
    }


    // ---------------------------------------------------------------------------------------------
    // Running
    // ---------------------------------------------------------------------------------------------

    void UpdateRunning()
    {
        if (this.IsRunning && !this.reducedMotion && this.IsLoaded)
            this.StartTicker();
        else
            this.StopTicker();
    }


    void StartTicker()
    {
        if (this.ticker is not null)
            return;

        this.ticker = MotionTicker.For(this);
        if (this.ticker is not null)
            this.ticker.Tick += this.Advance;
    }


    void StopTicker()
    {
        if (this.ticker is null)
            return;

        this.ticker.Tick -= this.Advance;
        this.ticker = null;
    }


    // ---------------------------------------------------------------------------------------------
    // Layout
    // ---------------------------------------------------------------------------------------------

    void OnContentChanged(View? oldContent, View? newContent)
    {
        if (oldContent is not null)
            this.Children.Remove(oldContent);

        // Under the canvas, so the border is always drawn over whatever the content paints.
        if (newContent is not null)
            this.Children.Insert(0, newContent);
    }


    void OnShapeChanged()
    {
        this.InvalidateMeasure();
        this.UpdateClip(this.Width, this.Height);
        this.Invalidate();
    }


    void UpdateClip(double width, double height)
    {
        if (width <= 0 || height <= 0)
            return;

        var size = new Size(width, height);
        if (size == this.clipSize && this.clipRadius == this.CornerRadius)
            return;

        this.clipSize = size;
        this.clipRadius = this.CornerRadius;
        this.clip.CornerRadius = new CornerRadius(Math.Max(this.CornerRadius, 0));
        this.clip.Rect = new Rect(0, 0, width, height);
    }


    protected override ILayoutManager CreateLayoutManager() => new Manager(this);


    sealed class Manager(AnimatedBorder owner) : ILayoutManager
    {
        Thickness Inset
        {
            get
            {
                var border = Math.Max(owner.BorderWidth, 0);
                var padding = owner.Padding;
                return new Thickness(padding.Left + border, padding.Top + border, padding.Right + border, padding.Bottom + border);
            }
        }

        public Size Measure(double widthConstraint, double heightConstraint)
        {
            var inset = this.Inset;
            var content = Size.Zero;

            if (owner.Content is IView view && view.Visibility != Visibility.Collapsed)
            {
                content = view.Measure(
                    Math.Max(widthConstraint - inset.HorizontalThickness, 0),
                    Math.Max(heightConstraint - inset.VerticalThickness, 0)
                );
            }

            ((IView)owner.canvas).Measure(widthConstraint, heightConstraint);

            var self = (IView)owner;
            var width = LayoutManager.ResolveConstraints(widthConstraint, self.Width, content.Width + inset.HorizontalThickness, self.MinimumWidth, self.MaximumWidth);
            var height = LayoutManager.ResolveConstraints(heightConstraint, self.Height, content.Height + inset.VerticalThickness, self.MinimumHeight, self.MaximumHeight);
            return new Size(width, height);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            var inset = this.Inset;

            if (owner.Content is IView view && view.Visibility != Visibility.Collapsed)
            {
                view.Arrange(new Rect(
                    bounds.X + inset.Left,
                    bounds.Y + inset.Top,
                    Math.Max(bounds.Width - inset.HorizontalThickness, 0),
                    Math.Max(bounds.Height - inset.VerticalThickness, 0)
                ));
            }

            ((IView)owner.canvas).Arrange(bounds);
            owner.UpdateClip(bounds.Width, bounds.Height);
            return bounds.Size;
        }
    }


    sealed class Painter(AnimatedBorder owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var width = (float)owner.canvas.Width;
            var height = (float)owner.canvas.Height;
            var border = (float)owner.BorderWidth;

            if (width <= 0 || height <= 0 || border <= 0)
                return;

            var path = owner.PathFor(width, height);
            if (path.Length <= 0)
                return;

            var track = owner.ResolvedTrackColor;
            if (track.Alpha > 0)
            {
                var inset = border / 2;
                canvas.StrokeColor = track;
                canvas.StrokeSize = border;
                canvas.DrawRoundedRectangle(
                    inset,
                    inset,
                    width - border,
                    height - border,
                    Math.Max((float)owner.CornerRadius - inset, 0)
                );
            }

            canvas.StrokeSize = border;
            canvas.StrokeLineCap = LineCap.Butt;
            owner.DrawEffect(canvas, path, width, height);
        }
    }
}
