namespace Shiny.Maui.Controls;

/// <summary>Where the spotlight over a <see cref="DotPatternEffect"/> goes.</summary>
public enum SpotlightMode
{
    /// <summary>No spotlight - a plain dot grid.</summary>
    None,

    /// <summary>Follows the pointer, and goes out when the pointer leaves.</summary>
    Pointer,

    /// <summary>Wanders slowly around the box on its own.</summary>
    Wander,

    /// <summary>Follows the pointer while it is over the box, and wanders otherwise - right for touch screens, which have no hover.</summary>
    Auto
}


/// <summary>
/// A grid of dots, lit up and enlarged under a soft spotlight that follows the pointer or wanders on
/// its own, with an optional ripple of light running through the grid.
/// </summary>
public class DotPatternEffect : BackgroundEffect
{
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color), typeof(Color), typeof(DotPatternEffect), Color.FromArgb("#94A3B8"));

    /// <summary>The colour of the dots. Default slate grey, which reads on light and dark.</summary>
    public Color Color
    {
        get => (Color)this.GetValue(ColorProperty);
        set => this.SetValue(ColorProperty, value);
    }

    public static readonly BindableProperty SpacingProperty = BindableProperty.Create(
        nameof(Spacing), typeof(double), typeof(DotPatternEffect), 18d);

    /// <summary>The distance between neighbouring dots. Default 18.</summary>
    public double Spacing
    {
        get => (double)this.GetValue(SpacingProperty);
        set => this.SetValue(SpacingProperty, value);
    }

    public static readonly BindableProperty DotSizeProperty = BindableProperty.Create(
        nameof(DotSize), typeof(double), typeof(DotPatternEffect), 1.2d);

    /// <summary>The radius of each dot. Default 1.2.</summary>
    public double DotSize
    {
        get => (double)this.GetValue(DotSizeProperty);
        set => this.SetValue(DotSizeProperty, value);
    }

    public static readonly BindableProperty DotOpacityProperty = BindableProperty.Create(
        nameof(DotOpacity), typeof(double), typeof(DotPatternEffect), 0.35d);

    /// <summary>How visible the dots are away from the spotlight. Default 0.35.</summary>
    public double DotOpacity
    {
        get => (double)this.GetValue(DotOpacityProperty);
        set => this.SetValue(DotOpacityProperty, value);
    }

    public static readonly BindableProperty SpotlightProperty = BindableProperty.Create(
        nameof(Spotlight), typeof(SpotlightMode), typeof(DotPatternEffect), SpotlightMode.Auto);

    /// <summary>Where the spotlight goes. Default <see cref="SpotlightMode.Auto"/>.</summary>
    public SpotlightMode Spotlight
    {
        get => (SpotlightMode)this.GetValue(SpotlightProperty);
        set => this.SetValue(SpotlightProperty, value);
    }

    public static readonly BindableProperty SpotlightColorProperty = BindableProperty.Create(
        nameof(SpotlightColor), typeof(Color), typeof(DotPatternEffect), null);

    /// <summary>The colour of lit dots and of the glow. Unset uses <see cref="Color"/>.</summary>
    public Color? SpotlightColor
    {
        get => (Color?)this.GetValue(SpotlightColorProperty);
        set => this.SetValue(SpotlightColorProperty, value);
    }

    public static readonly BindableProperty SpotlightRadiusProperty = BindableProperty.Create(
        nameof(SpotlightRadius), typeof(double), typeof(DotPatternEffect), 140d);

    /// <summary>How far the spotlight reaches. Default 140.</summary>
    public double SpotlightRadius
    {
        get => (double)this.GetValue(SpotlightRadiusProperty);
        set => this.SetValue(SpotlightRadiusProperty, value);
    }

    public static readonly BindableProperty GlowProperty = BindableProperty.Create(
        nameof(Glow), typeof(double), typeof(DotPatternEffect), 0.12d);

    /// <summary>How strong the soft glow under the spotlight is, from 0 to 1. Default 0.12.</summary>
    public double Glow
    {
        get => (double)this.GetValue(GlowProperty);
        set => this.SetValue(GlowProperty, value);
    }

    public static readonly BindableProperty PulseProperty = BindableProperty.Create(
        nameof(Pulse), typeof(bool), typeof(DotPatternEffect), false);

    /// <summary>Runs a slow diagonal ripple of light through the grid. Default false.</summary>
    public bool Pulse
    {
        get => (bool)this.GetValue(PulseProperty);
        set => this.SetValue(PulseProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    internal sealed class Renderer(DotPatternEffect effect, Random random) : EffectRenderer(random)
    {
        readonly double wanderOffset = random.NextDouble() * 100;
        PathF? grid;
        (float Width, float Height, double Spacing, double Size) gridKey;
        bool placed;

        /// <summary>Where the spotlight is drawn, eased towards <see cref="Target"/>.</summary>
        internal PointF Spot { get; private set; }

        /// <summary>How lit the spotlight is, 0 to 1 - it fades out when a pointer-only spotlight loses the pointer.</summary>
        internal float SpotLevel { get; private set; }

        internal (PointF Point, bool Lit) Target
        {
            get
            {
                var wander = new PointF(
                    (float)(this.Width * (0.5 + (0.38 * Math.Sin((this.Time + this.wanderOffset) * 0.31)))),
                    (float)(this.Height * (0.5 + (0.36 * Math.Sin((this.Time + this.wanderOffset) * 0.23 + 1.1))))
                );
                return effect.Spotlight switch
                {
                    SpotlightMode.Pointer => this.Pointer is { } p ? (p, true) : (this.Spot, false),
                    SpotlightMode.Wander => (wander, true),
                    SpotlightMode.Auto => (this.Pointer ?? wander, true),
                    _ => (this.Spot, false)
                };
            }
        }

        protected override void OnStep(double seconds)
        {
            var (target, lit) = this.Target;
            if (!this.placed)
                this.Settle();

            // Ease rather than snap: the spotlight glides after a fast-moving pointer.
            var k = (float)(1 - Math.Exp(-9 * seconds));
            this.Spot = new PointF(this.Spot.X + ((target.X - this.Spot.X) * k), this.Spot.Y + ((target.Y - this.Spot.Y) * k));
            this.SpotLevel += ((lit ? 1 : 0) - this.SpotLevel) * k;
        }

        internal override void Settle()
        {
            var (target, lit) = this.Target;
            this.Spot = target;
            this.SpotLevel = lit ? 1 : 0;
            this.placed = true;
        }

        internal override void Draw(ICanvas canvas)
        {
            if (!this.placed)
                this.Settle();

            var spacing = Math.Max(effect.Spacing, 2);
            var size = (float)Math.Max(effect.DotSize, 0.2);
            var color = effect.Color;
            var lightColor = effect.SpotlightColor ?? color;
            var baseAlpha = (float)Math.Clamp(effect.DotOpacity, 0, 1);
            var spotOn = effect.Spotlight != SpotlightMode.None && this.SpotLevel > 0.01f;
            var radius = (float)Math.Max(effect.SpotlightRadius, 1);
            var spot = this.Spot;

            if (spotOn && effect.Glow > 0)
            {
                var r = radius * 1.4f;
                var rect = new RectF(spot.X - r, spot.Y - r, r * 2, r * 2);
                var g = lightColor.WithAlpha(lightColor.Alpha * (float)Math.Clamp(effect.Glow, 0, 1) * this.SpotLevel);
                canvas.SetFillPaint(new RadialGradientPaint
                {
                    GradientStops = [new PaintGradientStop(0, g), new PaintGradientStop(1, g.WithAlpha(0))]
                }, rect);
                canvas.FillEllipse(rect);
            }

            // Centre the grid, so the margin left over is shared evenly round the edges.
            var columns = (int)(this.Width / spacing) + 1;
            var rows = (int)(this.Height / spacing) + 1;
            var originX = (float)((this.Width - ((columns - 1) * spacing)) / 2);
            var originY = (float)((this.Height - ((rows - 1) * spacing)) / 2);

            if (effect.Pulse)
            {
                // Every dot has its own alpha, so they are drawn one by one.
                for (var row = 0; row < rows; row++)
                for (var col = 0; col < columns; col++)
                {
                    var x = originX + (float)(col * spacing);
                    var y = originY + (float)(row * spacing);
                    var wave = (float)(0.5 + (0.5 * Math.Sin(((x + y) * 0.02) - (this.Time * 2.2))));
                    canvas.FillColor = color.WithAlpha(color.Alpha * baseAlpha * (0.35f + (0.65f * wave)));
                    canvas.FillCircle(x, y, size);
                }
            }
            else
            {
                // The resting grid is one path, rebuilt only when the box or the grid changes.
                var key = (this.Width, this.Height, spacing, (double)size);
                if (this.grid is null || key != this.gridKey)
                {
                    this.gridKey = key;
                    this.grid = new PathF();
                    for (var row = 0; row < rows; row++)
                    for (var col = 0; col < columns; col++)
                        this.grid.AppendCircle(originX + (float)(col * spacing), originY + (float)(row * spacing), size);
                }
                canvas.FillColor = color.WithAlpha(color.Alpha * baseAlpha);
                canvas.FillPath(this.grid);
            }

            if (!spotOn)
                return;

            // Only the dots under the spotlight are drawn again, brighter and bigger.
            var firstCol = Math.Max(0, (int)Math.Floor((spot.X - radius - originX) / spacing));
            var lastCol = Math.Min(columns - 1, (int)Math.Ceiling((spot.X + radius - originX) / spacing));
            var firstRow = Math.Max(0, (int)Math.Floor((spot.Y - radius - originY) / spacing));
            var lastRow = Math.Min(rows - 1, (int)Math.Ceiling((spot.Y + radius - originY) / spacing));

            for (var row = firstRow; row <= lastRow; row++)
            for (var col = firstCol; col <= lastCol; col++)
            {
                var x = originX + (float)(col * spacing);
                var y = originY + (float)(row * spacing);
                var dx = x - spot.X;
                var dy = y - spot.Y;
                var d = MathF.Sqrt((dx * dx) + (dy * dy));
                if (d >= radius)
                    continue;

                var t = (1 - (d / radius)) * this.SpotLevel;
                t *= t;
                canvas.FillColor = lightColor.WithAlpha(lightColor.Alpha * Math.Min(1, baseAlpha + (t * (1 - baseAlpha))));
                canvas.FillCircle(x, y, size * (1 + (t * 0.9f)));
            }
        }
    }
}
