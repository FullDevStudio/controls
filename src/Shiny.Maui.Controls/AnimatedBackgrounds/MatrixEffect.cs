namespace Shiny.Maui.Controls;

/// <summary>Columns of glyphs raining down, bright at the head and fading behind - the digital rain.</summary>
public class MatrixEffect : BackgroundEffect
{
    internal const string DefaultCharacters = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン0123456789";

    public static readonly BindableProperty ColorProperty = BindableProperty.Create(
        nameof(Color), typeof(Color), typeof(MatrixEffect), Color.FromArgb("#22C55E"));

    /// <summary>The colour of the trail. Default green.</summary>
    public Color Color
    {
        get => (Color)this.GetValue(ColorProperty);
        set => this.SetValue(ColorProperty, value);
    }

    public static readonly BindableProperty HeadColorProperty = BindableProperty.Create(
        nameof(HeadColor), typeof(Color), typeof(MatrixEffect), Color.FromArgb("#DCFCE7"));

    /// <summary>The colour of the leading glyph in each column. Default a pale green-white.</summary>
    public Color HeadColor
    {
        get => (Color)this.GetValue(HeadColorProperty);
        set => this.SetValue(HeadColorProperty, value);
    }

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize), typeof(double), typeof(MatrixEffect), 14d);

    /// <summary>The size of the glyphs, which also sets the column width. Default 14.</summary>
    public double FontSize
    {
        get => (double)this.GetValue(FontSizeProperty);
        set => this.SetValue(FontSizeProperty, value);
    }

    public static readonly BindableProperty CharactersProperty = BindableProperty.Create(
        nameof(Characters), typeof(string), typeof(MatrixEffect), DefaultCharacters);

    /// <summary>The glyphs to rain. Default half-width katakana and digits.</summary>
    public string Characters
    {
        get => (string)this.GetValue(CharactersProperty);
        set => this.SetValue(CharactersProperty, value);
    }

    public static readonly BindableProperty TrailLengthProperty = BindableProperty.Create(
        nameof(TrailLength), typeof(int), typeof(MatrixEffect), 16);

    /// <summary>How many glyphs fade out behind each head. Default 16.</summary>
    public int TrailLength
    {
        get => (int)this.GetValue(TrailLengthProperty);
        set => this.SetValue(TrailLengthProperty, value);
    }


    internal override EffectRenderer CreateRenderer(Random random) => new Renderer(this, random);


    internal sealed class Column
    {
        /// <summary>The head's row - fractional, it moves smoothly - and how many rows a second it falls.</summary>
        public double Head, Rate, Wait;
        public char[] Glyphs = [];
    }


    internal sealed class Renderer(MatrixEffect effect, Random random) : EffectRenderer(random)
    {
        internal readonly List<Column> Columns = [];
        int rows;

        protected override void OnResized(bool first)
        {
            if (first)
                this.Columns.Clear();
            this.Sync();
        }

        double Cell => Math.Max(effect.FontSize, 4);

        internal override void Sync()
        {
            if (!this.HasSize)
                return;

            var count = (int)Math.Ceiling(this.Width / this.Cell);
            this.rows = (int)Math.Ceiling(this.Height / this.Cell) + 1;

            Reconcile(this.Columns, count, () => new Column
            {
                Head = this.Between(-this.rows, this.rows),
                Rate = this.Between(6, 16)
            });

            foreach (var c in this.Columns)
            {
                if (c.Glyphs.Length != this.rows)
                {
                    var glyphs = new char[this.rows];
                    for (var i = 0; i < glyphs.Length; i++)
                        glyphs[i] = i < c.Glyphs.Length ? c.Glyphs[i] : this.Glyph();
                    c.Glyphs = glyphs;
                }
            }
        }

        char Glyph()
        {
            var set = string.IsNullOrEmpty(effect.Characters) ? DefaultCharacters : effect.Characters;
            return set[this.Random.Next(set.Length)];
        }

        protected override void OnStep(double seconds)
        {
            var trail = Math.Max(effect.TrailLength, 1);
            foreach (var c in this.Columns)
            {
                if (c.Wait > 0)
                {
                    c.Wait -= seconds;
                    continue;
                }

                c.Head += c.Rate * seconds;
                if (c.Head - trail > this.rows)
                {
                    c.Head = 0;
                    c.Rate = this.Between(6, 16);
                    c.Wait = this.Between(0, 2.5);
                }

                // A few glyphs in the trail flicker to something else.
                if (c.Glyphs.Length > 0 && this.Random.NextDouble() < seconds * 6)
                    c.Glyphs[this.Random.Next(c.Glyphs.Length)] = this.Glyph();
            }
        }

        internal override void Draw(ICanvas canvas)
        {
            var cell = (float)this.Cell;
            var trail = Math.Max(effect.TrailLength, 1);
            var color = effect.Color;
            var head = effect.HeadColor;

            canvas.Font = Microsoft.Maui.Graphics.Font.Default;
            canvas.FontSize = cell * 0.9f;

            for (var x = 0; x < this.Columns.Count; x++)
            {
                var c = this.Columns[x];
                if (c.Wait > 0 || c.Glyphs.Length == 0)
                    continue;

                var headRow = (int)Math.Floor(c.Head);
                for (var i = 0; i < trail; i++)
                {
                    var row = headRow - i;
                    if (row < 0 || row >= c.Glyphs.Length)
                        continue;

                    var alpha = 1f - ((float)i / trail);
                    canvas.FontColor = i == 0 ? head : color.WithAlpha(color.Alpha * alpha * 0.9f);
                    canvas.DrawString(
                        c.Glyphs[row].ToString(),
                        x * cell, row * cell, cell, cell,
                        HorizontalAlignment.Center, VerticalAlignment.Center
                    );
                }
            }
        }
    }
}
