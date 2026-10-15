namespace Shiny.Maui.Controls;

/// <summary>A meteor shower: bright heads with fading tails streaking across at an angle, each after a random pause.</summary>
public class MeteorsEffect : BackgroundEffect
{
    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color), typeof(Color), typeof(MeteorsEffect), Colors.White);

    /// <summary>The colour of the head; the tail fades from it to transparent. Default white.</summary>
    public Color Color
    {
        get => (Color)this.GetValue(ColorProperty);
        set => this.SetValue(ColorProperty, value);
    }

    public static readonly BindableProperty DensityProperty = BindableProperty.Create(
        nameof(Density), typeof(double), typeof(MeteorsEffect), 0.25d);

    /// <summary>How many meteors share each 100×100 area (most are waiting their turn). Default 0.25.</summary>
    public double Density
    {
        get => (double)this.GetValue(DensityProperty);
        set => this.SetValue(DensityProperty, value);
    }

    public static readonly BindableProperty AngleProperty = BindableProperty.Create(
        nameof(Angle), typeof(double), typeof(MeteorsEffect), 135d);

    /// <summary>The direction they travel, in degrees clockwise from pointing right - 90 is straight down. Default 135 (down and to the left).</summary>
    public double Angle
    {
        get => (double)this.GetValue(AngleProperty);
        set => this.SetValue(AngleProperty, value);
    }

    public static readonly BindableProperty LengthProperty = BindableProperty.Create(
        nameof(Length), typeof(double), typeof(MeteorsEffect), 90d);

    /// <summary>How long the tails are. Default 90.</summary>
    public double Length
    {
        get => (double)this.GetValue(LengthProperty);
        set => this.SetValue(LengthProperty, value);
    }

    public static readonly BindableProperty ThicknessProperty = BindableProperty.Create(
        nameof(Thickness), typeof(double), typeof(MeteorsEffect), 1.5d);

    /// <summary>How thick the head end of the tail is. Default 1.5.</summary>
    public double Thickness
    {
        get => (double)this.GetValue(ThicknessProperty);
        set => this.SetValue(ThicknessProperty, value);
    }

    public static readonly BindableProperty MaxDelayProperty = BindableProperty.Create(
        nameof(MaxDelay), typeof(TimeSpan), typeof(MeteorsEffect), TimeSpan.FromSeconds(5));

    /// <summary>The longest a meteor waits before it streaks across again. Default 5 seconds.</summary>
    public TimeSpan MaxDelay
    {
        get => (TimeSpan)this.GetValue(MaxDelayProperty);
        set => this.SetValue(MaxDelayProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    internal sealed class Meteor
    {
        public double X, Y, Travelled, Distance, Speed, Wait;
    }


    internal sealed class Renderer(MeteorsEffect effect, Random random) : EffectRenderer(random)
    {
        internal readonly List<Meteor> Meteors = [];

        protected override void OnResized(bool first)
        {
            if (first)
                this.Meteors.Clear();
            this.Sync();
        }

        internal override void Sync()
        {
            if (!this.HasSize)
                return;

            // At least one, so a small card still gets the odd meteor.
            var count = effect.Density > 0 ? Math.Max(1, CountFor(effect.Density, this.Width, this.Height, 200)) : 0;
            Reconcile(this.Meteors, count, () =>
            {
                var m = new Meteor();
                this.Launch(m);
                return m;
            });
        }

        (double X, double Y) Direction()
        {
            var radians = effect.Angle * Math.PI / 180;
            return (Math.Cos(radians), Math.Sin(radians));
        }

        void Launch(Meteor m)
        {
            // Start at a random point in the box, wound back along the direction of travel far enough to
            // begin off-screen, and travel until it is clear out the other side.
            var (dx, dy) = this.Direction();
            var diagonal = Math.Sqrt((this.Width * this.Width) + (this.Height * this.Height));
            var back = diagonal * 0.6;
            m.X = this.Between(0, this.Width) - (dx * back);
            m.Y = this.Between(0, this.Height) - (dy * back);
            m.Travelled = 0;
            m.Distance = (back * 2) + effect.Length;
            m.Speed = this.Between(260, 520);
            m.Wait = this.Between(0, Math.Max(effect.MaxDelay.TotalSeconds, 0));
        }

        protected override void OnStep(double seconds)
        {
            var (dx, dy) = this.Direction();
            foreach (var m in this.Meteors)
            {
                if (m.Wait > 0)
                {
                    m.Wait -= seconds;
                    continue;
                }

                var step = m.Speed * seconds;
                m.X += dx * step;
                m.Y += dy * step;
                m.Travelled += step;
                if (m.Travelled >= m.Distance)
                    this.Launch(m);
            }
        }

        internal override void Draw(ICanvas canvas)
        {
            var (dx, dy) = this.Direction();
            var color = effect.Color;
            var thickness = (float)Math.Max(effect.Thickness, 0.25);
            var length = (float)Math.Max(effect.Length, 1);

            foreach (var m in this.Meteors)
            {
                if (m.Wait > 0)
                    continue;

                // Fade in over the first stretch and out over the last, so none pop in mid-screen.
                var edge = Math.Min(m.Travelled, m.Distance - m.Travelled);
                var alpha = (float)Math.Clamp(edge / 80, 0, 1);
                if (alpha <= 0)
                    continue;

                var hx = (float)m.X;
                var hy = (float)m.Y;
                var tx = hx - ((float)dx * length);
                var ty = hy - ((float)dy * length);

                // The tail is a sliver of a triangle, wide at the head, filled with a gradient running
                // from the head to the tail's point.
                var nx = -(float)dy * thickness / 2;
                var ny = (float)dx * thickness / 2;
                var path = new PathF();
                path.MoveTo(hx + nx, hy + ny);
                path.LineTo(hx - nx, hy - ny);
                path.LineTo(tx, ty);
                path.Close();

                var bounds = path.Bounds;
                if (bounds.Width < 1) bounds = new RectF(bounds.X - 0.5f, bounds.Y, 1, bounds.Height);
                if (bounds.Height < 1) bounds = new RectF(bounds.X, bounds.Y - 0.5f, bounds.Width, 1);

                var head = color.WithAlpha(color.Alpha * alpha);
                canvas.SetFillPaint(new LinearGradientPaint
                {
                    GradientStops = [new PaintGradientStop(0, head), new PaintGradientStop(1, head.WithAlpha(0))],
                    StartPoint = new Point((hx - bounds.X) / bounds.Width, (hy - bounds.Y) / bounds.Height),
                    EndPoint = new Point((tx - bounds.X) / bounds.Width, (ty - bounds.Y) / bounds.Height)
                }, bounds);
                canvas.FillPath(path);

                canvas.FillColor = head;
                canvas.FillCircle(hx, hy, thickness);
            }
        }
    }
}
