namespace Shiny.Maui.Controls;

/// <summary>
/// Soft, glowing washes of colour drifting slowly over one another, like the northern lights. One
/// glow per colour.
/// </summary>
public class AuroraEffect : BackgroundEffect
{
    static readonly IReadOnlyList<Color> DefaultColors =
    [
        Color.FromArgb("#22D3EE"),
        Color.FromArgb("#6366F1"),
        Color.FromArgb("#A855F7"),
        Color.FromArgb("#10B981")
    ];

    public static readonly BindableProperty ColorsProperty = BindableProperty.Create(
        nameof(Colors), typeof(IReadOnlyList<Color>), typeof(AuroraEffect), DefaultColors);

    /// <summary>The colours of the glows, one glow each. In XAML a comma-separated list. Default cyan, indigo, violet and emerald.</summary>
    [System.ComponentModel.TypeConverter(typeof(ColorListTypeConverter))]
    public IReadOnlyList<Color> Colors
    {
        get => (IReadOnlyList<Color>)this.GetValue(ColorsProperty);
        set => this.SetValue(ColorsProperty, value);
    }

    public static readonly BindableProperty IntensityProperty = BindableProperty.Create(
        nameof(Intensity), typeof(double), typeof(AuroraEffect), 0.6d);

    /// <summary>How strong the glows are at their centres, from 0 to 1. Default 0.6.</summary>
    public double Intensity
    {
        get => (double)this.GetValue(IntensityProperty);
        set => this.SetValue(IntensityProperty, value);
    }

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(AuroraEffect), 0.65d);

    /// <summary>How big each glow is, as a fraction of the box's longer side. Default 0.65.</summary>
    public double Size
    {
        get => (double)this.GetValue(SizeProperty);
        set => this.SetValue(SizeProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    /// <summary>Where glow <paramref name="index"/> is, as fractions of the box, at <paramref name="time"/> seconds.</summary>
    /// <remarks>Each glow follows its own slow Lissajous curve, biased towards the top like a real aurora.</remarks>
    internal static (double X, double Y, double Scale) GlowAt(int index, double time)
    {
        var i = index + 1;
        var fx = 0.05 + (0.023 * i);
        var fy = 0.04 + (0.017 * i);
        var x = 0.5 + (0.42 * Math.Sin((time * fx) + (i * 1.7)));
        var y = 0.38 + (0.3 * Math.Sin((time * fy) + (i * 2.3)));
        var scale = 0.85 + (0.15 * Math.Sin((time * 0.11) + i));
        return (x, y, scale);
    }


    internal sealed class Renderer(AuroraEffect effect, Random random) : EffectRenderer(random)
    {
        // A random start, so two auroras on one page are not in step.
        readonly double offset = random.NextDouble() * 200;

        protected override void OnStep(double seconds) { }

        internal override void Draw(ICanvas canvas)
        {
            var colors = effect.Colors;
            if (colors is not { Count: > 0 })
                return;

            var intensity = (float)Math.Clamp(effect.Intensity, 0, 1);
            var longest = Math.Max(this.Width, this.Height);
            var baseRadius = (float)(Math.Max(effect.Size, 0.05) * longest);

            for (var i = 0; i < colors.Count; i++)
            {
                var c = colors[i];
                var (fx, fy, scale) = GlowAt(i, this.Time + this.offset);
                var r = baseRadius * (float)scale;
                var cx = (float)(fx * this.Width);
                var cy = (float)(fy * this.Height);
                var rect = new RectF(cx - r, cy - r, r * 2, r * 2);

                canvas.SetFillPaint(new RadialGradientPaint
                {
                    GradientStops =
                    [
                        new PaintGradientStop(0, c.WithAlpha(c.Alpha * intensity)),
                        new PaintGradientStop(0.45f, c.WithAlpha(c.Alpha * intensity * 0.45f)),
                        new PaintGradientStop(1, c.WithAlpha(0))
                    ]
                }, rect);
                canvas.FillEllipse(rect);
            }
        }
    }
}
