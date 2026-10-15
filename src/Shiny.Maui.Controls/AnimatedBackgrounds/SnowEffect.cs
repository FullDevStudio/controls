namespace Shiny.Maui.Controls;

/// <summary>Snowflakes drifting down, the larger ones nearer and faster, swaying as they fall.</summary>
public class SnowEffect : BackgroundEffect
{
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color), typeof(Color), typeof(SnowEffect), Colors.White);

    /// <summary>The colour of the flakes. Default white.</summary>
    public Color Color
    {
        get => (Color)this.GetValue(ColorProperty);
        set => this.SetValue(ColorProperty, value);
    }

    public static readonly BindableProperty DensityProperty = BindableProperty.Create(
        nameof(Density), typeof(double), typeof(SnowEffect), 5d);

    /// <summary>How many flakes fill each 100×100 area. Default 5.</summary>
    public double Density
    {
        get => (double)this.GetValue(DensityProperty);
        set => this.SetValue(DensityProperty, value);
    }

    public static readonly BindableProperty MinSizeProperty = BindableProperty.Create(
        nameof(MinSize), typeof(double), typeof(SnowEffect), 1d);

    /// <summary>The smallest flake's radius. Default 1.</summary>
    public double MinSize
    {
        get => (double)this.GetValue(MinSizeProperty);
        set => this.SetValue(MinSizeProperty, value);
    }

    public static readonly BindableProperty MaxSizeProperty = BindableProperty.Create(
        nameof(MaxSize), typeof(double), typeof(SnowEffect), 3.5d);

    /// <summary>The largest flake's radius. Default 3.5.</summary>
    public double MaxSize
    {
        get => (double)this.GetValue(MaxSizeProperty);
        set => this.SetValue(MaxSizeProperty, value);
    }

    public static readonly BindableProperty WindProperty = BindableProperty.Create(
        nameof(Wind), typeof(double), typeof(SnowEffect), 0d);

    /// <summary>A steady sideways push: negative blows left, positive right, about -1 to 1. Default 0.</summary>
    public double Wind
    {
        get => (double)this.GetValue(WindProperty);
        set => this.SetValue(WindProperty, value);
    }

    public static readonly BindableProperty SwayProperty = BindableProperty.Create(
        nameof(Sway), typeof(double), typeof(SnowEffect), 1d);

    /// <summary>How far flakes rock side to side as they fall, as a multiplier. Default 1; 0 falls straight.</summary>
    public double Sway
    {
        get => (double)this.GetValue(SwayProperty);
        set => this.SetValue(SwayProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    internal sealed class Flake
    {
        public double X, Y, Radius, Phase, Alpha;
    }


    internal sealed class Renderer(SnowEffect effect, Random random) : EffectRenderer(random)
    {
        internal readonly List<Flake> Flakes = [];

        protected override void OnResized(bool first)
        {
            if (first)
                this.Flakes.Clear();
            this.Sync();
        }

        internal override void Sync()
        {
            if (!this.HasSize)
                return;

            Reconcile(this.Flakes, CountFor(effect.Density, this.Width, this.Height), () => this.Spawn(this.Between(0, this.Height)));
        }

        Flake Spawn(double y)
        {
            var min = Math.Max(0.1, Math.Min(effect.MinSize, effect.MaxSize));
            var max = Math.Max(min, effect.MaxSize);
            return new Flake
            {
                X = this.Between(0, this.Width),
                Y = y,
                Radius = this.Between(min, max),
                Phase = this.Between(0, Math.PI * 2),
                Alpha = this.Between(0.45, 1)
            };
        }

        protected override void OnStep(double seconds)
        {
            var max = Math.Max(effect.MaxSize, 0.1);
            foreach (var f in this.Flakes)
            {
                var depth = f.Radius / max;
                f.Y += (18 + (depth * 42)) * seconds;
                f.X += ((effect.Wind * 50 * depth) + (Math.Cos(this.Time * 1.2 + f.Phase) * 14 * effect.Sway)) * seconds;

                if (f.Y - f.Radius > this.Height)
                {
                    var next = this.Spawn(-f.Radius);
                    f.X = next.X;
                    f.Y = -next.Radius;
                    f.Radius = next.Radius;
                    f.Alpha = next.Alpha;
                }

                // Wind carries flakes off one side; bring them in on the other.
                if (f.X < -f.Radius)
                    f.X += this.Width + (f.Radius * 2);
                else if (f.X > this.Width + f.Radius)
                    f.X -= this.Width + (f.Radius * 2);
            }
        }

        internal override void Draw(ICanvas canvas)
        {
            var color = effect.Color;
            foreach (var f in this.Flakes)
            {
                canvas.FillColor = color.WithAlpha((float)(color.Alpha * f.Alpha));
                canvas.FillCircle((float)f.X, (float)f.Y, (float)f.Radius);
            }
        }
    }
}
