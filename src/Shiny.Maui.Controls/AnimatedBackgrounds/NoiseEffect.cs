namespace Shiny.Maui.Controls;

/// <summary>
/// Flickering film grain. On its own it is TV static; at a low <see cref="Intensity"/> over a
/// <c>Background</c> gradient it adds the subtle texture that stops flat colour looking digital.
/// </summary>
public class NoiseEffect : BackgroundEffect
{
    /// <summary>The grain is built as this many frames and cycled - cheap, and indistinguishable from fresh noise.</summary>
    internal const int FrameCount = 4;

    /// <summary>The grain is drawn as a tile of at most this size, repeated across the box.</summary>
    internal const float TileSize = 192;

    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color), typeof(Color), typeof(NoiseEffect), Colors.White);

    /// <summary>The colour of the grain. Default white; black suits a light background.</summary>
    public Color Color
    {
        get => (Color)this.GetValue(ColorProperty);
        set => this.SetValue(ColorProperty, value);
    }

    public static readonly BindableProperty IntensityProperty = BindableProperty.Create(
        nameof(Intensity), typeof(double), typeof(NoiseEffect), 0.15d);

    /// <summary>How visible the grain is, from 0 to 1. Default 0.15.</summary>
    public double Intensity
    {
        get => (double)this.GetValue(IntensityProperty);
        set => this.SetValue(IntensityProperty, value);
    }

    public static readonly BindableProperty GrainSizeProperty = BindableProperty.Create(
        nameof(GrainSize), typeof(double), typeof(NoiseEffect), 2d);

    /// <summary>How big each speck is. Default 2.</summary>
    public double GrainSize
    {
        get => (double)this.GetValue(GrainSizeProperty);
        set => this.SetValue(GrainSizeProperty, value);
    }

    public static readonly BindableProperty CoverageProperty = BindableProperty.Create(
        nameof(Coverage), typeof(double), typeof(NoiseEffect), 0.3d);

    /// <summary>The fraction of the area covered in specks, from 0 to 1. Default 0.3.</summary>
    public double Coverage
    {
        get => (double)this.GetValue(CoverageProperty);
        set => this.SetValue(CoverageProperty, value);
    }

    public static readonly BindableProperty FrameRateProperty = BindableProperty.Create(
        nameof(FrameRate), typeof(double), typeof(NoiseEffect), 12d);

    /// <summary>How many times a second the grain changes (scaled by <see cref="BackgroundEffect.Speed"/>). Default 12, a film's flicker.</summary>
    public double FrameRate
    {
        get => (double)this.GetValue(FrameRateProperty);
        set => this.SetValue(FrameRateProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    internal sealed class Renderer(NoiseEffect effect, Random random) : EffectRenderer(random)
    {
        /// <summary>Each frame is two paths, bright specks and faint ones.</summary>
        internal readonly (PathF Bright, PathF Faint)[] Frames = new (PathF, PathF)[FrameCount];
        (float W, float H, double Grain, double Coverage) key;
        double clock;
        internal int Frame;
        float offsetX, offsetY;

        internal SizeF Tile { get; private set; }

        internal override void Sync()
        {
            if (!this.HasSize)
                return;

            var grain = Math.Max(effect.GrainSize, 0.5);
            var coverage = Math.Clamp(effect.Coverage, 0, 1);
            var tile = new SizeF(Math.Min(this.Width, TileSize), Math.Min(this.Height, TileSize));
            var key = (tile.Width, tile.Height, grain, coverage);
            if (this.Frames[0].Bright is not null && key == this.key)
                return;

            this.key = key;
            this.Tile = tile;
            var columns = (int)Math.Ceiling(tile.Width / grain);
            var rows = (int)Math.Ceiling(tile.Height / grain);
            var g = (float)grain;

            for (var f = 0; f < FrameCount; f++)
            {
                var bright = new PathF();
                var faint = new PathF();
                for (var row = 0; row < rows; row++)
                for (var col = 0; col < columns; col++)
                {
                    var roll = this.Random.NextDouble();
                    if (roll >= coverage)
                        continue;

                    var rect = new RectF(col * g, row * g, g, g);
                    if (roll < coverage * 0.4)
                        bright.AppendRectangle(rect);
                    else
                        faint.AppendRectangle(rect);
                }
                this.Frames[f] = (bright, faint);
            }
        }

        protected override void OnStep(double seconds)
        {
            var rate = Math.Max(effect.FrameRate, 0);
            if (rate <= 0)
                return;

            this.clock += seconds;
            if (this.clock < 1 / rate)
                return;

            this.clock = 0;
            this.Frame = (this.Frame + 1) % FrameCount;

            // Shift the tile seams each frame so the repetition never settles into a visible pattern.
            this.offsetX = (float)this.Between(0, this.Tile.Width);
            this.offsetY = (float)this.Between(0, this.Tile.Height);
        }

        internal override void Draw(ICanvas canvas)
        {
            if (this.Frames[0].Bright is null)
                this.Sync();

            var (bright, faint) = this.Frames[this.Frame];
            if (bright is null || this.Tile.Width <= 0 || this.Tile.Height <= 0)
                return;

            var color = effect.Color;
            var intensity = (float)Math.Clamp(effect.Intensity, 0, 1);
            var brightColor = color.WithAlpha(color.Alpha * intensity);
            var faintColor = color.WithAlpha(color.Alpha * intensity * 0.45f);

            canvas.ClipRectangle(0, 0, this.Width, this.Height);
            for (var y = -this.offsetY; y < this.Height; y += this.Tile.Height)
            for (var x = -this.offsetX; x < this.Width; x += this.Tile.Width)
            {
                canvas.SaveState();
                canvas.Translate(x, y);
                canvas.FillColor = brightColor;
                canvas.FillPath(bright);
                canvas.FillColor = faintColor;
                canvas.FillPath(faint);
                canvas.RestoreState();
            }
        }
    }
}
