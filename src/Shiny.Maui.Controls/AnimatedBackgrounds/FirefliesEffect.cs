namespace Shiny.Maui.Controls;

/// <summary>Glowing specks wandering lazily and pulsing in and out, like fireflies at dusk.</summary>
public class FirefliesEffect : BackgroundEffect
{
    static readonly IReadOnlyList<Color> DefaultColors = [Color.FromArgb("#FDE68A"), Color.FromArgb("#FACC15"), Color.FromArgb("#BEF264")];

    public static readonly BindableProperty ColorsProperty = BindableProperty.Create(
        nameof(Colors), typeof(IReadOnlyList<Color>), typeof(FirefliesEffect), DefaultColors);

    /// <summary>The colours fireflies are picked from. In XAML a comma-separated list. Default warm yellows and a lime.</summary>
    [System.ComponentModel.TypeConverter(typeof(ColorListTypeConverter))]
    public IReadOnlyList<Color> Colors
    {
        get => (IReadOnlyList<Color>)this.GetValue(ColorsProperty);
        set => this.SetValue(ColorsProperty, value);
    }

    public static readonly BindableProperty DensityProperty = BindableProperty.Create(
        nameof(Density), typeof(double), typeof(FirefliesEffect), 0.5d);

    /// <summary>How many fireflies fill each 100×100 area. Default 0.5.</summary>
    public double Density
    {
        get => (double)this.GetValue(DensityProperty);
        set => this.SetValue(DensityProperty, value);
    }

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(FirefliesEffect), 2.5d);

    /// <summary>The radius of each bright core. Default 2.5.</summary>
    public double Size
    {
        get => (double)this.GetValue(SizeProperty);
        set => this.SetValue(SizeProperty, value);
    }

    public static readonly BindableProperty GlowProperty = BindableProperty.Create(
        nameof(Glow), typeof(double), typeof(FirefliesEffect), 5d);

    /// <summary>How far the glow spreads, as a multiple of <see cref="Size"/>. Default 5; 0 for no glow.</summary>
    public double Glow
    {
        get => (double)this.GetValue(GlowProperty);
        set => this.SetValue(GlowProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    internal sealed class Fly
    {
        public double X, Y, Heading, Turn, Phase, Pulse;
        public int Color;
    }


    internal sealed class Renderer(FirefliesEffect effect, Random random) : EffectRenderer(random)
    {
        internal readonly List<Fly> Flies = [];

        protected override void OnResized(bool first)
        {
            if (first)
                this.Flies.Clear();
            this.Sync();
        }

        internal override void Sync()
        {
            if (!this.HasSize)
                return;

            Reconcile(this.Flies, CountFor(effect.Density, this.Width, this.Height, 400), () => new Fly
            {
                X = this.Between(0, this.Width),
                Y = this.Between(0, this.Height),
                Heading = this.Between(0, Math.PI * 2),
                Turn = this.Between(-1, 1),
                Phase = this.Between(0, Math.PI * 2),
                Pulse = this.Between(0.8, 2),
                Color = this.Random.Next(0, 64)
            });
        }

        protected override void OnStep(double seconds)
        {
            foreach (var f in this.Flies)
            {
                // Drift the rate of turn a little each step: smooth, unhurried wandering.
                f.Turn = Math.Clamp(f.Turn + this.Between(-2, 2) * seconds, -1.2, 1.2);
                f.Heading += f.Turn * seconds;
                f.X += Math.Cos(f.Heading) * 16 * seconds;
                f.Y += Math.Sin(f.Heading) * 16 * seconds;

                if (f.X < -10) f.X = this.Width + 10;
                else if (f.X > this.Width + 10) f.X = -10;
                if (f.Y < -10) f.Y = this.Height + 10;
                else if (f.Y > this.Height + 10) f.Y = -10;
            }
        }

        internal override void Draw(ICanvas canvas)
        {
            var colors = effect.Colors;
            var size = (float)Math.Max(effect.Size, 0.25);
            var glow = (float)Math.Max(effect.Glow, 0) * size;

            foreach (var f in this.Flies)
            {
                var c = Pick(colors, f.Color, Microsoft.Maui.Graphics.Colors.Yellow);
                var brightness = (float)(0.25 + (0.75 * (0.5 + (0.5 * Math.Sin((this.Time * f.Pulse) + f.Phase)))));
                var x = (float)f.X;
                var y = (float)f.Y;

                if (glow > size)
                {
                    var rect = new RectF(x - glow, y - glow, glow * 2, glow * 2);
                    canvas.SetFillPaint(new RadialGradientPaint
                    {
                        GradientStops =
                        [
                            new PaintGradientStop(0, c.WithAlpha(c.Alpha * 0.55f * brightness)),
                            new PaintGradientStop(1, c.WithAlpha(0))
                        ]
                    }, rect);
                    canvas.FillEllipse(rect);
                }

                canvas.FillColor = c.WithAlpha(c.Alpha * brightness);
                canvas.FillCircle(x, y, size);
            }
        }
    }
}
