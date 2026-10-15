namespace Shiny.Maui.Controls;

/// <summary>Translucent bubbles rising and wobbling, each with a little highlight.</summary>
public class BubblesEffect : BackgroundEffect
{
    static readonly IReadOnlyList<Color> DefaultColors = [Color.FromArgb("#7DD3FC"), Color.FromArgb("#A5B4FC"), Color.FromArgb("#F0ABFC")];

    public static readonly BindableProperty ColorsProperty = BindableProperty.Create(
        nameof(Colors), typeof(IReadOnlyList<Color>), typeof(BubblesEffect), DefaultColors);

    /// <summary>The colours bubbles are picked from. In XAML a comma-separated list. Default sky, periwinkle and pink.</summary>
    [System.ComponentModel.TypeConverter(typeof(ColorListTypeConverter))]
    public IReadOnlyList<Color> Colors
    {
        get => (IReadOnlyList<Color>)this.GetValue(ColorsProperty);
        set => this.SetValue(ColorsProperty, value);
    }

    public static readonly BindableProperty DensityProperty = BindableProperty.Create(
        nameof(Density), typeof(double), typeof(BubblesEffect), 0.6d);

    /// <summary>How many bubbles fill each 100×100 area. Default 0.6.</summary>
    public double Density
    {
        get => (double)this.GetValue(DensityProperty);
        set => this.SetValue(DensityProperty, value);
    }

    public static readonly BindableProperty MinSizeProperty = BindableProperty.Create(
        nameof(MinSize), typeof(double), typeof(BubblesEffect), 4d);

    /// <summary>The smallest bubble's radius. Default 4.</summary>
    public double MinSize
    {
        get => (double)this.GetValue(MinSizeProperty);
        set => this.SetValue(MinSizeProperty, value);
    }

    public static readonly BindableProperty MaxSizeProperty = BindableProperty.Create(
        nameof(MaxSize), typeof(double), typeof(BubblesEffect), 18d);

    /// <summary>The largest bubble's radius. Default 18.</summary>
    public double MaxSize
    {
        get => (double)this.GetValue(MaxSizeProperty);
        set => this.SetValue(MaxSizeProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    internal sealed class Bubble
    {
        public double X, Y, Radius, Phase, Rise;
        public int Color;
    }


    internal sealed class Renderer(BubblesEffect effect, Random random) : EffectRenderer(random)
    {
        internal readonly List<Bubble> Bubbles = [];

        protected override void OnResized(bool first)
        {
            if (first)
                this.Bubbles.Clear();
            this.Sync();
        }

        internal override void Sync()
        {
            if (!this.HasSize)
                return;

            Reconcile(this.Bubbles, CountFor(effect.Density, this.Width, this.Height, 600), () => this.Spawn(this.Between(0, this.Height)));
        }

        Bubble Spawn(double y)
        {
            var min = Math.Max(0.5, Math.Min(effect.MinSize, effect.MaxSize));
            var max = Math.Max(min, effect.MaxSize);
            var radius = this.Between(min, max);
            return new Bubble
            {
                X = this.Between(0, this.Width),
                Y = y,
                Radius = radius,
                Phase = this.Between(0, Math.PI * 2),
                Rise = this.Between(18, 46),
                Color = this.Random.Next(0, 64)
            };
        }

        protected override void OnStep(double seconds)
        {
            foreach (var b in this.Bubbles)
            {
                b.Y -= b.Rise * seconds;
                b.X += Math.Sin((this.Time * 1.5) + b.Phase) * 12 * seconds;
                if (b.Y + b.Radius < 0)
                {
                    var next = this.Spawn(this.Height + b.Radius);
                    b.X = next.X;
                    b.Y = this.Height + next.Radius;
                    b.Radius = next.Radius;
                    b.Rise = next.Rise;
                    b.Color = next.Color;
                }
            }
        }

        internal override void Draw(ICanvas canvas)
        {
            var colors = effect.Colors;
            canvas.StrokeSize = 1;
            foreach (var b in this.Bubbles)
            {
                var c = Pick(colors, b.Color, Microsoft.Maui.Graphics.Colors.White);
                var x = (float)b.X;
                var y = (float)b.Y;
                var r = (float)b.Radius;

                canvas.FillColor = c.WithAlpha(c.Alpha * 0.12f);
                canvas.FillCircle(x, y, r);
                canvas.StrokeColor = c.WithAlpha(c.Alpha * 0.6f);
                canvas.DrawCircle(x, y, r);
                canvas.FillColor = Microsoft.Maui.Graphics.Colors.White.WithAlpha(0.45f);
                canvas.FillCircle(x - (r * 0.35f), y - (r * 0.35f), Math.Max(r * 0.18f, 0.75f));
            }
        }
    }
}
