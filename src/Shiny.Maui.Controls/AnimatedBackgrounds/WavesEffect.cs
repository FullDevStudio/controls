namespace Shiny.Maui.Controls;

/// <summary>Layered, translucent waves rolling across the bottom of the box, one layer per colour.</summary>
public class WavesEffect : BackgroundEffect
{
    static readonly IReadOnlyList<Color> DefaultColors = [Color.FromArgb("#38BDF8"), Color.FromArgb("#3B82F6"), Color.FromArgb("#6366F1")];

    public static readonly BindableProperty ColorsProperty = BindableProperty.Create(
        nameof(Colors), typeof(IReadOnlyList<Color>), typeof(WavesEffect), DefaultColors);

    /// <summary>One wave per colour, back to front. In XAML a comma-separated list. Default sky, blue and indigo.</summary>
    [System.ComponentModel.TypeConverter(typeof(ColorListTypeConverter))]
    public IReadOnlyList<Color> Colors
    {
        get => (IReadOnlyList<Color>)this.GetValue(ColorsProperty);
        set => this.SetValue(ColorsProperty, value);
    }

    public static readonly BindableProperty OpacityProperty = BindableProperty.Create(
        nameof(Opacity), typeof(double), typeof(WavesEffect), 0.45d);

    /// <summary>How opaque each layer is, so the ones behind show through. Default 0.45.</summary>
    public double Opacity
    {
        get => (double)this.GetValue(OpacityProperty);
        set => this.SetValue(OpacityProperty, value);
    }

    public static readonly BindableProperty AmplitudeProperty = BindableProperty.Create(
        nameof(Amplitude), typeof(double), typeof(WavesEffect), 18d);

    /// <summary>How tall the waves are. Default 18.</summary>
    public double Amplitude
    {
        get => (double)this.GetValue(AmplitudeProperty);
        set => this.SetValue(AmplitudeProperty, value);
    }

    public static readonly BindableProperty WavelengthProperty = BindableProperty.Create(
        nameof(Wavelength), typeof(double), typeof(WavesEffect), 320d);

    /// <summary>The distance from one crest to the next. Default 320.</summary>
    public double Wavelength
    {
        get => (double)this.GetValue(WavelengthProperty);
        set => this.SetValue(WavelengthProperty, value);
    }

    public static readonly BindableProperty LevelProperty = BindableProperty.Create(
        nameof(Level), typeof(double), typeof(WavesEffect), 0.65d);

    /// <summary>Where the back wave's surface sits, as a fraction of the height from the top. Default 0.65.</summary>
    public double Level
    {
        get => (double)this.GetValue(LevelProperty);
        set => this.SetValue(LevelProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    /// <summary>The surface height of <paramref name="layer"/> at <paramref name="x"/>, relative to its rest line.</summary>
    internal static double Surface(int layer, double x, double time, double amplitude, double wavelength)
    {
        var k = Math.PI * 2 / Math.Max(wavelength, 1);
        var direction = layer % 2 == 0 ? 1 : -1;
        var omega = 0.9 + (layer * 0.25);
        return (amplitude * Math.Sin((x * k) + (time * omega * direction) + (layer * 1.3)))
             + (amplitude * 0.35 * Math.Sin((x * k * 1.9) - (time * omega * 1.4 * direction) + layer));
    }


    internal sealed class Renderer(WavesEffect effect, Random random) : EffectRenderer(random)
    {
        protected override void OnStep(double seconds) { }

        internal override void Draw(ICanvas canvas)
        {
            var colors = effect.Colors;
            if (colors is not { Count: > 0 })
                return;

            var opacity = (float)Math.Clamp(effect.Opacity, 0, 1);
            var amplitude = Math.Max(effect.Amplitude, 0);
            var top = this.Height * Math.Clamp(effect.Level, 0, 1);
            var gap = Math.Max(amplitude * 0.9, 6);
            const float step = 8;

            for (var layer = 0; layer < colors.Count; layer++)
            {
                var rest = top + (layer * gap);
                var path = new PathF();
                path.MoveTo(0, this.Height);
                for (float x = 0; x <= this.Width + step; x += step)
                {
                    var px = Math.Min(x, this.Width);
                    path.LineTo(px, (float)(rest + Surface(layer, px, this.Time, amplitude, effect.Wavelength)));
                }
                path.LineTo(this.Width, this.Height);
                path.Close();

                var c = colors[layer];
                canvas.FillColor = c.WithAlpha(c.Alpha * opacity);
                canvas.FillPath(path);
            }
        }
    }
}
