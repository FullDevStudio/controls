using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>Columns of glyphs raining down, bright at the head and fading behind - the digital rain.</summary>
public class MatrixEffect : BackgroundEffect
{
    internal const string DefaultCharacters = "アイウエオカキクケコサシスセソタチツテトナニヌネノハヒフヘホマミムメモヤユヨラリルレロワン0123456789";

    /// <summary>The colour of the trail. Default green.</summary>
    public string Color { get; set; } = "#22c55e";

    /// <summary>The colour of the leading glyph in each column. Default a pale green-white.</summary>
    public string HeadColor { get; set; } = "#dcfce7";

    /// <summary>The size of the glyphs, which also sets the column width. Default 14.</summary>
    public double FontSize { get; set; } = 14;

    /// <summary>The glyphs to rain. Default half-width katakana and digits.</summary>
    public string Characters { get; set; } = DefaultCharacters;

    /// <summary>How many glyphs fade out behind each head. Default 16.</summary>
    public int TrailLength { get; set; } = 16;

    internal override string Kind => "matrix";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColor(writer, "color", this.Color);
        WriteColor(writer, "headColor", this.HeadColor);
        writer.WriteNumber("fontSize", this.FontSize);
        writer.WriteString("characters", this.Characters);
        writer.WriteNumber("trailLength", this.TrailLength);
    }
}
