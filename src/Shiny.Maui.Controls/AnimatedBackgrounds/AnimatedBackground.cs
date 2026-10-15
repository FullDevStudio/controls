using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.MotionIcons;

namespace Shiny.Maui.Controls;

/// <summary>
/// A container - used like a <see cref="Border"/> - that draws an animated <see cref="Effect"/>
/// (aurora, particles, snow, rain, meteors, a dot pattern with a spotlight, film grain, ...) behind
/// its <see cref="Content"/>.
/// </summary>
/// <example>
/// <code>
/// &lt;shiny:AnimatedBackground CornerRadius="16" Padding="24" Background="#0B1020"&gt;
///     &lt;shiny:AnimatedBackground.Effect&gt;
///         &lt;shiny:SnowEffect Density="7" Wind="0.3" /&gt;
///     &lt;/shiny:AnimatedBackground.Effect&gt;
///     &lt;Label Text="Let it snow" TextColor="White" /&gt;
/// &lt;/shiny:AnimatedBackground&gt;
/// </code>
/// </example>
/// <remarks>
/// <para>
/// The effect is drawn on a <see cref="GraphicsView"/> under the content, so it renders the same on
/// every head (AppKit and GTK4 included) and taps go straight through to the content. Set
/// <c>Background</c> for the colour behind the effect - most effects are made for a dark one.
/// </para>
/// <para>
/// Frames come from the window's shared frame ticker, and only while the control is loaded, running
/// and the device has not asked for reduced motion. When it is not animating it still paints one still
/// frame, so the background never disappears. To fill a page, make it the page's content.
/// </para>
/// </remarks>
[ContentProperty(nameof(Content))]
public class AnimatedBackground : Layout
{
    readonly GraphicsView canvas;
    readonly RoundRectangleGeometry clip = new();
    MotionTicker? ticker;
    EffectRenderer? renderer;
    BackgroundEffect? subscribed;
    Size clipSize;
    double clipRadius = -1;
    bool reducedMotion;

    public AnimatedBackground()
    {
        this.canvas = new GraphicsView
        {
            Drawable = new Painter(this),
            InputTransparent = true,
            BackgroundColor = Colors.Transparent
        };
        this.Children.Add(this.canvas);

        // Installed once and only its rectangle changes afterwards: on Android the first non-null Clip
        // wraps the native view, and doing that mid layout pass leaves it at a stale size.
        this.Clip = this.clip;

        // Hover tracking for effects that follow the pointer. It never claims the touch, so the
        // content's own gestures are unaffected.
        var pointer = new PointerGestureRecognizer();
        pointer.PointerMoved += (_, e) => this.SetPointer(e.GetPosition(this));
        pointer.PointerPressed += (_, e) => this.SetPointer(e.GetPosition(this));
        pointer.PointerExited += (_, _) => this.SetPointer(null);
#if ANDROID || IOS
        // A lifted finger has no hover; leaving the point where it lifted would park the effect there.
        pointer.PointerReleased += (_, _) => this.SetPointer(null);
#endif
        this.GestureRecognizers.Add(pointer);

        this.Loaded += (_, _) =>
        {
            this.reducedMotion = this.RespectReducedMotion && ReducedMotion.IsEnabled;
            this.Subscribe(this.Effect);
            this.renderer?.Sync();
            this.UpdateRunning();
            this.canvas.Invalidate();
        };
        this.Unloaded += (_, _) =>
        {
            this.StopTicker();
            this.Subscribe(null);
        };

        StyleGuard.MarkReady(this, typeof(AnimatedBackground));
    }


    // ---------------------------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------------------------

    public static readonly BindableProperty ContentProperty = BindableProperty.Create(
        nameof(Content), typeof(View), typeof(AnimatedBackground), null,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(AnimatedBackground), () => ((AnimatedBackground)b).OnContentChanged((View?)o, (View?)n)));

    /// <summary>What sits on top of the effect.</summary>
    public View? Content
    {
        get => (View?)this.GetValue(ContentProperty);
        set => this.SetValue(ContentProperty, value);
    }

    public static readonly BindableProperty EffectProperty = BindableProperty.Create(
        nameof(Effect), typeof(BackgroundEffect), typeof(AnimatedBackground), null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(AnimatedBackground), () => ((AnimatedBackground)b).OnEffectChanged((BackgroundEffect?)n)));

    /// <summary>The effect drawn behind the content. Null draws nothing but the background.</summary>
    public BackgroundEffect? Effect
    {
        get => (BackgroundEffect?)this.GetValue(EffectProperty);
        set => this.SetValue(EffectProperty, value);
    }

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius), typeof(double), typeof(AnimatedBackground), 0d,
        propertyChanged: (b, _, _) => ((AnimatedBackground)b).UpdateClip(((AnimatedBackground)b).Width, ((AnimatedBackground)b).Height, force: true));

    /// <summary>The radius of the corners. The background, effect and content are clipped to it. Default 0.</summary>
    public double CornerRadius
    {
        get => (double)this.GetValue(CornerRadiusProperty);
        set => this.SetValue(CornerRadiusProperty, value);
    }

    public static readonly BindableProperty IsRunningProperty = BindableProperty.Create(
        nameof(IsRunning), typeof(bool), typeof(AnimatedBackground), true,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(AnimatedBackground), () => ((AnimatedBackground)b).UpdateRunning()));

    /// <summary>Whether it animates. False holds the effect where it is.</summary>
    public bool IsRunning
    {
        get => (bool)this.GetValue(IsRunningProperty);
        set => this.SetValue(IsRunningProperty, value);
    }

    public static readonly BindableProperty RespectReducedMotionProperty = BindableProperty.Create(
        nameof(RespectReducedMotion), typeof(bool), typeof(AnimatedBackground), true,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(AnimatedBackground), () =>
        {
            var bg = (AnimatedBackground)b;
            bg.reducedMotion = bg.RespectReducedMotion && ReducedMotion.IsEnabled;
            bg.UpdateRunning();
        }));

    /// <summary>Holds a still frame when the device asks for reduced motion. On by default.</summary>
    public bool RespectReducedMotion
    {
        get => (bool)this.GetValue(RespectReducedMotionProperty);
        set => this.SetValue(RespectReducedMotionProperty, value);
    }


    // ---------------------------------------------------------------------------------------------
    // Test seams
    // ---------------------------------------------------------------------------------------------

    /// <summary>The random source new renderers are seeded from. Tests replace it to get repeatable frames.</summary>
    internal Random Random { get; set; } = Random.Shared;

    /// <summary>Whether the frame ticker is currently driving it.</summary>
    internal bool IsTicking => this.ticker is not null;

    /// <summary>The moving state of the current effect.</summary>
    internal EffectRenderer? Renderer => this.renderer;

    /// <summary>The ticker's callback: move the effect on by real time.</summary>
    internal void Advance(TimeSpan delta)
    {
        if (this.renderer is null || this.Effect is null)
            return;

        var seconds = delta.TotalSeconds * Math.Max(this.Effect.Speed, 0);
        if (seconds > 0)
            this.renderer.Step(seconds);

        this.canvas.Invalidate();
    }

    internal void SetPointer(Point? point)
    {
        if (this.renderer is not null)
            this.renderer.Pointer = point is { } p ? new PointF((float)p.X, (float)p.Y) : null;

        // A still background (paused, reduced motion) still follows the pointer.
        if (!this.IsTicking)
        {
            this.renderer?.Settle();
            this.canvas.Invalidate();
        }
    }


    // ---------------------------------------------------------------------------------------------
    // Effect
    // ---------------------------------------------------------------------------------------------

    void OnEffectChanged(BackgroundEffect? effect)
    {
        this.renderer = effect?.CreateRenderer(this.Random);
        if (this.renderer is not null && this.canvas.Width > 0 && this.canvas.Height > 0)
            this.renderer.Resize((float)this.canvas.Width, (float)this.canvas.Height);

        if (this.IsLoaded)
            this.Subscribe(effect);

        this.UpdateRunning();
        this.canvas.Invalidate();
    }


    void Subscribe(BackgroundEffect? effect)
    {
        if (ReferenceEquals(effect, this.subscribed))
            return;

        // Only while loaded: a shared effect (a resource, a style) would otherwise root every
        // background that ever showed it.
        if (this.subscribed is not null)
            this.subscribed.Changed -= this.OnEffectSettingChanged;

        this.subscribed = effect;
        if (effect is not null)
            effect.Changed += this.OnEffectSettingChanged;
    }


    void OnEffectSettingChanged(object? sender, string? property)
    {
        this.renderer?.Sync();
        if (!this.IsTicking)
            this.canvas.Invalidate();
    }


    // ---------------------------------------------------------------------------------------------
    // Running
    // ---------------------------------------------------------------------------------------------

    void UpdateRunning()
    {
        if (this.IsRunning && !this.reducedMotion && this.IsLoaded && this.renderer is not null)
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

        // Over the canvas, so the content always paints on top of the effect.
        if (newContent is not null)
            this.Children.Add(newContent);
    }


    void UpdateClip(double width, double height, bool force = false)
    {
        if (width <= 0 || height <= 0)
            return;

        var size = new Size(width, height);
        if (!force && size == this.clipSize && this.clipRadius == this.CornerRadius)
            return;

        this.clipSize = size;
        this.clipRadius = this.CornerRadius;
        this.clip.CornerRadius = new CornerRadius(Math.Max(this.CornerRadius, 0));
        this.clip.Rect = new Rect(0, 0, width, height);
    }


    protected override ILayoutManager CreateLayoutManager() => new Manager(this);


    sealed class Manager(AnimatedBackground owner) : ILayoutManager
    {
        public Size Measure(double widthConstraint, double heightConstraint)
        {
            var padding = owner.Padding;
            var content = Size.Zero;

            if (owner.Content is IView view && view.Visibility != Visibility.Collapsed)
            {
                content = view.Measure(
                    Math.Max(widthConstraint - padding.HorizontalThickness, 0),
                    Math.Max(heightConstraint - padding.VerticalThickness, 0)
                );
            }

            ((IView)owner.canvas).Measure(widthConstraint, heightConstraint);

            var self = (IView)owner;
            var width = LayoutManager.ResolveConstraints(widthConstraint, self.Width, content.Width + padding.HorizontalThickness, self.MinimumWidth, self.MaximumWidth);
            var height = LayoutManager.ResolveConstraints(heightConstraint, self.Height, content.Height + padding.VerticalThickness, self.MinimumHeight, self.MaximumHeight);
            return new Size(width, height);
        }

        public Size ArrangeChildren(Rect bounds)
        {
            var padding = owner.Padding;

            if (owner.Content is IView view && view.Visibility != Visibility.Collapsed)
            {
                view.Arrange(new Rect(
                    bounds.X + padding.Left,
                    bounds.Y + padding.Top,
                    Math.Max(bounds.Width - padding.HorizontalThickness, 0),
                    Math.Max(bounds.Height - padding.VerticalThickness, 0)
                ));
            }

            ((IView)owner.canvas).Arrange(bounds);
            owner.UpdateClip(bounds.Width, bounds.Height);
            owner.renderer?.Resize((float)bounds.Width, (float)bounds.Height);
            return bounds.Size;
        }
    }


    sealed class Painter(AnimatedBackground owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            var renderer = owner.renderer;
            if (renderer is null || owner.canvas.Width <= 0 || owner.canvas.Height <= 0)
                return;

            canvas.SaveState();
            renderer.Draw(canvas);
            canvas.RestoreState();
        }
    }
}
