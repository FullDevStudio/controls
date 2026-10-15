namespace Shiny.Maui.Controls;

/// <summary>
/// A night sky: stars twinkling and drifting slowly, or - with <see cref="Warp"/> - streaking out
/// from the centre as if flying through them.
/// </summary>
public class StarfieldEffect : BackgroundEffect
{
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color), typeof(Color), typeof(StarfieldEffect), Colors.White);

    /// <summary>The colour of the stars. Default white.</summary>
    public Color Color
    {
        get => (Color)this.GetValue(ColorProperty);
        set => this.SetValue(ColorProperty, value);
    }

    public static readonly BindableProperty DensityProperty = BindableProperty.Create(
        nameof(Density), typeof(double), typeof(StarfieldEffect), 6d);

    /// <summary>How many stars fill each 100×100 area. Default 6.</summary>
    public double Density
    {
        get => (double)this.GetValue(DensityProperty);
        set => this.SetValue(DensityProperty, value);
    }

    public static readonly BindableProperty MaxSizeProperty = BindableProperty.Create(
        nameof(MaxSize), typeof(double), typeof(StarfieldEffect), 1.6d);

    /// <summary>The radius of the brightest stars. Default 1.6.</summary>
    public double MaxSize
    {
        get => (double)this.GetValue(MaxSizeProperty);
        set => this.SetValue(MaxSizeProperty, value);
    }

    public static readonly BindableProperty WarpProperty = BindableProperty.Create(
        nameof(Warp), typeof(bool), typeof(StarfieldEffect), false);

    /// <summary>Flies through the stars instead of watching them. Default false.</summary>
    public bool Warp
    {
        get => (bool)this.GetValue(WarpProperty);
        set => this.SetValue(WarpProperty, value);
    }

    public static readonly BindableProperty TwinkleProperty = BindableProperty.Create(
        nameof(Twinkle), typeof(bool), typeof(StarfieldEffect), true);

    /// <summary>Whether stars twinkle. Default true.</summary>
    public bool Twinkle
    {
        get => (bool)this.GetValue(TwinkleProperty);
        set => this.SetValue(TwinkleProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    internal sealed class Star
    {
        /// <summary>Calm: screen position. Warp: position on the projection plane, -1 to 1.</summary>
        public double X, Y;

        /// <summary>Calm: depth, 0.2 (far) to 1 (near). Warp: distance, shrinking towards the viewer.</summary>
        public double Z;

        public double Phase, Rate;
    }


    internal sealed class Renderer(StarfieldEffect effect, Random random) : EffectRenderer(random)
    {
        internal readonly List<Star> Stars = [];
        bool warp;

        protected override void OnResized(bool first)
        {
            if (first)
                this.Stars.Clear();
            this.Sync();
        }

        internal override void Sync()
        {
            if (!this.HasSize)
                return;

            // The two modes keep their stars in different spaces, so switching scatters them afresh.
            if (this.warp != effect.Warp)
            {
                this.warp = effect.Warp;
                this.Stars.Clear();
            }

            Reconcile(this.Stars, CountFor(effect.Density, this.Width, this.Height, 1500), () =>
            {
                var star = new Star { Phase = this.Between(0, Math.PI * 2), Rate = this.Between(0.6, 2.4) };
                this.Place(star, this.Between(0.05, 1));
                return star;
            });
        }

        void Place(Star star, double z)
        {
            if (this.warp)
            {
                star.X = this.Between(-1, 1);
                star.Y = this.Between(-1, 1);
                star.Z = z;
            }
            else
            {
                star.X = this.Between(0, this.Width);
                star.Y = this.Between(0, this.Height);
                star.Z = this.Between(0.2, 1);
            }
        }

        (float X, float Y) Project(double x, double y, double z)
        {
            var half = Math.Max(this.Width, this.Height) / 2;
            return ((float)((this.Width / 2) + (x / z * half)), (float)((this.Height / 2) + (y / z * half)));
        }

        protected override void OnStep(double seconds)
        {
            foreach (var s in this.Stars)
            {
                if (this.warp)
                {
                    s.Z -= 0.35 * seconds;
                    var (px, py) = this.Project(s.X, s.Y, Math.Max(s.Z, 0.001));
                    if (s.Z <= 0.02 || px < -20 || px > this.Width + 20 || py < -20 || py > this.Height + 20)
                        this.Place(s, 1);
                }
                else
                {
                    // Nearer stars drift faster - a gentle parallax.
                    s.X -= 4 * s.Z * seconds;
                    if (s.X < -2)
                    {
                        s.X = this.Width + 2;
                        s.Y = this.Between(0, this.Height);
                    }
                }
            }
        }

        internal override void Draw(ICanvas canvas)
        {
            var color = effect.Color;
            var max = (float)Math.Max(effect.MaxSize, 0.2);
            canvas.StrokeLineCap = LineCap.Round;

            foreach (var s in this.Stars)
            {
                var twinkle = effect.Twinkle ? (float)(0.55 + (0.45 * Math.Sin((this.Time * s.Rate) + s.Phase))) : 1f;

                if (this.warp)
                {
                    var z = Math.Max(s.Z, 0.02);
                    var near = (float)Math.Clamp(1 - z, 0, 1);
                    var (x, y) = this.Project(s.X, s.Y, z);
                    var (tx, ty) = this.Project(s.X, s.Y, Math.Min(z + 0.06, 1.2));
                    var alpha = color.Alpha * Math.Min(1, near * 1.6f) * twinkle;

                    canvas.StrokeColor = color.WithAlpha(alpha);
                    canvas.StrokeSize = Math.Max(0.5f, max * near * 1.4f);
                    canvas.DrawLine(tx, ty, x, y);
                }
                else
                {
                    var depth = (float)s.Z;
                    canvas.FillColor = color.WithAlpha(color.Alpha * depth * twinkle);
                    canvas.FillCircle((float)s.X, (float)s.Y, Math.Max(0.35f, max * depth));
                }
            }
        }
    }
}
