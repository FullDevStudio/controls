namespace Shiny.Maui.Controls;

/// <summary>Slanted streaks of rain, nearer drops longer and brighter, splashing where they land.</summary>
public class RainEffect : BackgroundEffect
{
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color), typeof(Color), typeof(RainEffect), Color.FromArgb("#A5B4FC"));

    /// <summary>The colour of the drops. Default a pale blue.</summary>
    public Color Color
    {
        get => (Color)this.GetValue(ColorProperty);
        set => this.SetValue(ColorProperty, value);
    }

    public static readonly BindableProperty DensityProperty = BindableProperty.Create(
        nameof(Density), typeof(double), typeof(RainEffect), 8d);

    /// <summary>How many drops fill each 100×100 area. Default 8.</summary>
    public double Density
    {
        get => (double)this.GetValue(DensityProperty);
        set => this.SetValue(DensityProperty, value);
    }

    public static readonly BindableProperty AngleProperty = BindableProperty.Create(
        nameof(Angle), typeof(double), typeof(RainEffect), 12d);

    /// <summary>How far the rain leans from vertical, in degrees - positive falls to the right. Default 12.</summary>
    public double Angle
    {
        get => (double)this.GetValue(AngleProperty);
        set => this.SetValue(AngleProperty, value);
    }

    public static readonly BindableProperty LengthProperty = BindableProperty.Create(
        nameof(Length), typeof(double), typeof(RainEffect), 18d);

    /// <summary>How long the nearest drops are. Default 18.</summary>
    public double Length
    {
        get => (double)this.GetValue(LengthProperty);
        set => this.SetValue(LengthProperty, value);
    }

    public static readonly BindableProperty ThicknessProperty = BindableProperty.Create(
        nameof(Thickness), typeof(double), typeof(RainEffect), 1d);

    /// <summary>How thick the drops are. Default 1.</summary>
    public double Thickness
    {
        get => (double)this.GetValue(ThicknessProperty);
        set => this.SetValue(ThicknessProperty, value);
    }

    public static readonly BindableProperty SplashProperty = BindableProperty.Create(
        nameof(Splash), typeof(bool), typeof(RainEffect), true);

    /// <summary>Whether drops splash at the bottom edge. Default true.</summary>
    public bool Splash
    {
        get => (bool)this.GetValue(SplashProperty);
        set => this.SetValue(SplashProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    internal sealed class Drop
    {
        public double X, Y, Depth;
    }


    internal sealed class Ripple
    {
        public double X, Age, Size;
    }


    internal sealed class Renderer(RainEffect effect, Random random) : EffectRenderer(random)
    {
        const double RippleLife = 0.35;
        internal readonly List<Drop> Drops = [];
        internal readonly List<Ripple> Ripples = [];

        protected override void OnResized(bool first)
        {
            if (first)
                this.Drops.Clear();
            this.Sync();
        }

        internal override void Sync()
        {
            if (!this.HasSize)
                return;

            Reconcile(this.Drops, CountFor(effect.Density, this.Width, this.Height), () => this.Spawn(this.Between(-this.Height * 0.1, this.Height)));
            if (!effect.Splash)
                this.Ripples.Clear();
        }

        Drop Spawn(double y)
        {
            // Spawn across a band wide enough that slanted rain still covers the far corner.
            var slant = Math.Tan(effect.Angle * Math.PI / 180) * this.Height;
            return new Drop
            {
                X = this.Between(Math.Min(0, -slant), this.Width + Math.Max(0, -slant)),
                Y = y,
                Depth = this.Between(0.35, 1)
            };
        }

        protected override void OnStep(double seconds)
        {
            var radians = effect.Angle * Math.PI / 180;
            var dx = Math.Sin(radians);
            var dy = Math.Cos(radians);

            foreach (var d in this.Drops)
            {
                var speed = (450 + (d.Depth * 550)) * seconds;
                d.X += dx * speed;
                d.Y += dy * speed;

                if (d.Y > this.Height)
                {
                    if (effect.Splash && d.Depth > 0.6 && d.X >= 0 && d.X <= this.Width)
                        this.Ripples.Add(new Ripple { X = d.X, Size = 3 + (d.Depth * 5) });

                    var next = this.Spawn(-effect.Length * d.Depth);
                    d.X = next.X;
                    d.Y = next.Y;
                    d.Depth = next.Depth;
                }
            }

            for (var i = this.Ripples.Count - 1; i >= 0; i--)
            {
                this.Ripples[i].Age += seconds;
                if (this.Ripples[i].Age >= RippleLife)
                    this.Ripples.RemoveAt(i);
            }
        }

        internal override void Draw(ICanvas canvas)
        {
            var color = effect.Color;
            var radians = effect.Angle * Math.PI / 180;
            var dx = (float)Math.Sin(radians);
            var dy = (float)Math.Cos(radians);

            canvas.StrokeLineCap = LineCap.Round;
            foreach (var d in this.Drops)
            {
                var length = (float)(effect.Length * d.Depth);
                canvas.StrokeColor = color.WithAlpha((float)(color.Alpha * d.Depth * 0.8));
                canvas.StrokeSize = (float)(effect.Thickness * (0.5 + (d.Depth * 0.5)));
                canvas.DrawLine((float)d.X, (float)d.Y, (float)d.X - (dx * length), (float)d.Y - (dy * length));
            }

            canvas.StrokeSize = 1;
            foreach (var r in this.Ripples)
            {
                var t = (float)(r.Age / RippleLife);
                var w = (float)(r.Size * 2 * (0.4 + t));
                canvas.StrokeColor = color.WithAlpha(color.Alpha * 0.6f * (1 - t));
                canvas.DrawEllipse((float)r.X - (w / 2), this.Height - (w * 0.15f) - 1, w, w * 0.3f);
            }
        }
    }
}
