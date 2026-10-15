namespace Shiny.Maui.Controls;

/// <summary>How <see cref="ParticlesEffect"/> reacts to the pointer.</summary>
public enum ParticlePointerMode
{
    /// <summary>Ignores it.</summary>
    None,

    /// <summary>Particles near the pointer are pushed away.</summary>
    Repel,

    /// <summary>Particles near the pointer are drawn towards it.</summary>
    Attract
}


/// <summary>
/// Drifting particles joined by faint lines when they come close - the "constellation" or network
/// background - optionally pushed around by the pointer.
/// </summary>
public class ParticlesEffect : BackgroundEffect
{
    static readonly IReadOnlyList<Color> DefaultColors = [Color.FromArgb("#A5B4FC"), Color.FromArgb("#F0ABFC"), Color.FromArgb("#67E8F9")];

    public static readonly BindableProperty ColorsProperty = BindableProperty.Create(
        nameof(Colors), typeof(IReadOnlyList<Color>), typeof(ParticlesEffect), DefaultColors);

    /// <summary>The colours particles are picked from. In XAML a comma-separated list. Default indigo, pink and cyan.</summary>
    [System.ComponentModel.TypeConverter(typeof(ColorListTypeConverter))]
    public IReadOnlyList<Color> Colors
    {
        get => (IReadOnlyList<Color>)this.GetValue(ColorsProperty);
        set => this.SetValue(ColorsProperty, value);
    }

    public static readonly BindableProperty DensityProperty = BindableProperty.Create(
        nameof(Density), typeof(double), typeof(ParticlesEffect), 2.5d);

    /// <summary>How many particles fill each 100×100 area (at most 400 in all). Default 2.5.</summary>
    public double Density
    {
        get => (double)this.GetValue(DensityProperty);
        set => this.SetValue(DensityProperty, value);
    }

    public static readonly BindableProperty MinSizeProperty = BindableProperty.Create(
        nameof(MinSize), typeof(double), typeof(ParticlesEffect), 1d);

    /// <summary>The smallest particle's radius. Default 1.</summary>
    public double MinSize
    {
        get => (double)this.GetValue(MinSizeProperty);
        set => this.SetValue(MinSizeProperty, value);
    }

    public static readonly BindableProperty MaxSizeProperty = BindableProperty.Create(
        nameof(MaxSize), typeof(double), typeof(ParticlesEffect), 2.5d);

    /// <summary>The largest particle's radius. Default 2.5.</summary>
    public double MaxSize
    {
        get => (double)this.GetValue(MaxSizeProperty);
        set => this.SetValue(MaxSizeProperty, value);
    }

    public static readonly BindableProperty LinkDistanceProperty = BindableProperty.Create(
        nameof(LinkDistance), typeof(double), typeof(ParticlesEffect), 110d);

    /// <summary>How close two particles must be to be joined by a line. Default 110; 0 draws no lines.</summary>
    public double LinkDistance
    {
        get => (double)this.GetValue(LinkDistanceProperty);
        set => this.SetValue(LinkDistanceProperty, value);
    }

    public static readonly BindableProperty LinkColorProperty = BindableProperty.Create(
        nameof(LinkColor), typeof(Color), typeof(ParticlesEffect), null);

    /// <summary>The colour of the joining lines. Unset uses the first of <see cref="Colors"/>.</summary>
    public Color? LinkColor
    {
        get => (Color?)this.GetValue(LinkColorProperty);
        set => this.SetValue(LinkColorProperty, value);
    }

    public static readonly BindableProperty LinkOpacityProperty = BindableProperty.Create(
        nameof(LinkOpacity), typeof(double), typeof(ParticlesEffect), 0.35d);

    /// <summary>How strong a line between two touching particles is; it fades out with distance. Default 0.35.</summary>
    public double LinkOpacity
    {
        get => (double)this.GetValue(LinkOpacityProperty);
        set => this.SetValue(LinkOpacityProperty, value);
    }

    public static readonly BindableProperty PointerModeProperty = BindableProperty.Create(
        nameof(PointerMode), typeof(ParticlePointerMode), typeof(ParticlesEffect), ParticlePointerMode.Repel);

    /// <summary>What the pointer does to nearby particles. Default <see cref="ParticlePointerMode.Repel"/>.</summary>
    public ParticlePointerMode PointerMode
    {
        get => (ParticlePointerMode)this.GetValue(PointerModeProperty);
        set => this.SetValue(PointerModeProperty, value);
    }

    public static readonly BindableProperty PointerRadiusProperty = BindableProperty.Create(
        nameof(PointerRadius), typeof(double), typeof(ParticlesEffect), 120d);

    /// <summary>How far from the pointer particles feel it. Default 120.</summary>
    public double PointerRadius
    {
        get => (double)this.GetValue(PointerRadiusProperty);
        set => this.SetValue(PointerRadiusProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    internal sealed class Particle
    {
        public double X, Y, VX, VY, Radius;
        public int Color;
    }


    internal sealed class Renderer(ParticlesEffect effect, Random random) : EffectRenderer(random)
    {
        internal readonly List<Particle> Particles = [];

        protected override void OnResized(bool first)
        {
            if (first)
                this.Particles.Clear();
            this.Sync();
        }

        internal override void Sync()
        {
            if (!this.HasSize)
                return;

            var min = Math.Max(0.25, Math.Min(effect.MinSize, effect.MaxSize));
            var max = Math.Max(min, effect.MaxSize);
            Reconcile(this.Particles, CountFor(effect.Density, this.Width, this.Height, 400), () =>
            {
                var angle = this.Between(0, Math.PI * 2);
                var speed = this.Between(6, 22);
                return new Particle
                {
                    X = this.Between(0, this.Width),
                    Y = this.Between(0, this.Height),
                    VX = Math.Cos(angle) * speed,
                    VY = Math.Sin(angle) * speed,
                    Radius = this.Between(min, max),
                    Color = this.Random.Next(0, 64)
                };
            });
        }

        protected override void OnStep(double seconds)
        {
            var pointer = this.Pointer;
            var mode = effect.PointerMode;
            var reach = Math.Max(effect.PointerRadius, 1);

            foreach (var p in this.Particles)
            {
                var x = p.X + (p.VX * seconds);
                var y = p.Y + (p.VY * seconds);

                if (pointer is { } at && mode != ParticlePointerMode.None)
                {
                    var dx = x - at.X;
                    var dy = y - at.Y;
                    var distance = Math.Sqrt((dx * dx) + (dy * dy));
                    if (distance < reach && distance > 0.01)
                    {
                        // Displace, rather than accelerate, so particles settle back to their drift as
                        // soon as the pointer leaves instead of being flung across the box.
                        var force = (1 - (distance / reach)) * 160 * seconds;
                        var sign = mode == ParticlePointerMode.Repel ? 1 : -1;
                        x += dx / distance * force * sign;
                        y += dy / distance * force * sign;
                    }
                }

                if (x < 0) { x = 0; p.VX = Math.Abs(p.VX); }
                else if (x > this.Width) { x = this.Width; p.VX = -Math.Abs(p.VX); }
                if (y < 0) { y = 0; p.VY = Math.Abs(p.VY); }
                else if (y > this.Height) { y = this.Height; p.VY = -Math.Abs(p.VY); }

                p.X = x;
                p.Y = y;
            }
        }

        internal override void Draw(ICanvas canvas)
        {
            var colors = effect.Colors;
            var fallback = Microsoft.Maui.Graphics.Colors.White;
            var link = effect.LinkDistance;

            if (link > 0 && effect.LinkOpacity > 0)
            {
                var lineColor = effect.LinkColor ?? Pick(colors, 0, fallback);
                var linkSquared = link * link;
                canvas.StrokeSize = 1;

                for (var i = 0; i < this.Particles.Count; i++)
                {
                    var a = this.Particles[i];
                    for (var j = i + 1; j < this.Particles.Count; j++)
                    {
                        var b = this.Particles[j];
                        var dx = a.X - b.X;
                        var dy = a.Y - b.Y;
                        var d2 = (dx * dx) + (dy * dy);
                        if (d2 >= linkSquared)
                            continue;

                        var strength = (float)((1 - (Math.Sqrt(d2) / link)) * effect.LinkOpacity);
                        canvas.StrokeColor = lineColor.WithAlpha(lineColor.Alpha * strength);
                        canvas.DrawLine((float)a.X, (float)a.Y, (float)b.X, (float)b.Y);
                    }
                }
            }

            foreach (var p in this.Particles)
            {
                canvas.FillColor = Pick(colors, p.Color, fallback);
                canvas.FillCircle((float)p.X, (float)p.Y, (float)p.Radius);
            }
        }
    }
}
