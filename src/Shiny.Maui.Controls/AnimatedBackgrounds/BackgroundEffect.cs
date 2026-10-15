using System.ComponentModel;

namespace Shiny.Maui.Controls;

/// <summary>
/// What an <see cref="AnimatedBackground"/> draws behind its content - aurora, snow, rain, meteors and
/// so on. Each effect is a plain bindable object whose properties (colours, density, speed, ...) can be
/// set in XAML, bound, or changed while it runs.
/// </summary>
/// <remarks>
/// An effect holds only settings. The moving state lives in a renderer the host creates from it, so
/// one effect instance can be shared by several backgrounds (from a resource or a style) without them
/// moving in lock-step.
/// </remarks>
public abstract class BackgroundEffect : BindableObject
{
    public static readonly BindableProperty SpeedProperty = BindableProperty.Create(
        nameof(Speed), typeof(double), typeof(BackgroundEffect), 1d);

    /// <summary>How fast it moves, as a multiplier of the effect's natural pace. Default 1; 0 holds it still.</summary>
    public double Speed
    {
        get => (double)this.GetValue(SpeedProperty);
        set => this.SetValue(SpeedProperty, value);
    }


    /// <summary>Raised when any setting changes, so every background showing the effect repaints.</summary>
    internal event EventHandler<string?>? Changed;

    /// <summary>Creates the moving state for one background.</summary>
    internal abstract EffectRenderer CreateRenderer(Random random);

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        this.Changed?.Invoke(this, propertyName);
    }


    /// <summary>A uniformly random value between <paramref name="min"/> and <paramref name="max"/>.</summary>
    internal static double Between(Random random, double min, double max)
        => min + (random.NextDouble() * (max - min));

    /// <summary>How many of something fill a box at a density given per 100×100 area.</summary>
    internal static int CountFor(double density, double width, double height, int cap = 2000)
    {
        if (density <= 0 || width <= 0 || height <= 0)
            return 0;

        return (int)Math.Clamp(Math.Round(density * width * height / 10_000d), 0, cap);
    }

    /// <summary>The colour at <paramref name="index"/>, wrapping round the list, or the fallback when it is empty.</summary>
    internal static Color Pick(IReadOnlyList<Color>? colors, int index, Color fallback)
        => colors is { Count: > 0 } ? colors[Math.Abs(index) % colors.Count] : fallback;
}


/// <summary>The moving state of one effect on one background.</summary>
internal abstract class EffectRenderer
{
    protected EffectRenderer(Random random) => this.Random = random;

    protected Random Random { get; }

    protected float Width { get; private set; }

    protected float Height { get; private set; }

    /// <summary>Speed-scaled seconds the effect has run for.</summary>
    internal double Time { get; private set; }

    /// <summary>Where the pointer is over the background, or null when it is not over it.</summary>
    internal PointF? Pointer { get; set; }

    /// <summary>Called on every layout pass; does work only when the box really changed size.</summary>
    internal void Resize(float width, float height)
    {
        if (width == this.Width && height == this.Height)
            return;

        var first = this.Width <= 0 || this.Height <= 0;
        this.Width = width;
        this.Height = height;
        if (width > 0 && height > 0)
            this.OnResized(first);
    }

    /// <summary>
    /// The box is a new size. <paramref name="first"/> is true the first time, when everything is
    /// scattered across the box; afterwards existing elements are kept and only topped up or trimmed.
    /// </summary>
    protected virtual void OnResized(bool first) => this.Sync();

    /// <summary>A setting changed - reconcile counts and anything cached from the settings.</summary>
    internal virtual void Sync() { }

    /// <summary>Moves on by <paramref name="seconds"/> of (speed-scaled) time.</summary>
    internal void Step(double seconds)
    {
        if (seconds <= 0 || this.Width <= 0 || this.Height <= 0)
            return;

        // A long frame (the ticker clamps at 250ms) is split up, so fast movers do not tunnel or jump.
        while (seconds > 0)
        {
            var dt = Math.Min(seconds, 1d / 30);
            this.Time += dt;
            this.OnStep(dt);
            seconds -= dt;
        }
    }

    protected abstract void OnStep(double seconds);

    /// <summary>The effect is not animating but something it follows (the pointer) moved - jump to it.</summary>
    internal virtual void Settle() { }

    internal abstract void Draw(ICanvas canvas);

    protected bool HasSize => this.Width > 0 && this.Height > 0;

    /// <summary>Tops <paramref name="items"/> up to, or trims it down to, <paramref name="target"/>.</summary>
    protected static void Reconcile<T>(List<T> items, int target, Func<T> create)
    {
        if (items.Count > target)
            items.RemoveRange(target, items.Count - target);

        while (items.Count < target)
            items.Add(create());
    }

    protected double Between(double min, double max) => BackgroundEffect.Between(this.Random, min, max);
}
