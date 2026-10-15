using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>A night sky: stars twinkling and drifting slowly, or - with <see cref="Warp"/> - streaking out from the centre as if flying through them.</summary>
public class StarfieldEffect : BackgroundEffect
{
    /// <summary>The colour of the stars. Default white.</summary>
    public string Color { get; set; } = "#ffffff";

    /// <summary>How many stars fill each 100×100 area. Default 6.</summary>
    public double Density { get; set; } = 6;

    /// <summary>The radius of the brightest stars. Default 1.6.</summary>
    public double MaxSize { get; set; } = 1.6;

    /// <summary>Flies through the stars instead of watching them. Default false.</summary>
    public bool Warp { get; set; } = false;

    /// <summary>Whether stars twinkle. Default true.</summary>
    public bool Twinkle { get; set; } = true;

    internal override string Kind => "starfield";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColor(writer, "color", this.Color);
        writer.WriteNumber("density", this.Density);
        writer.WriteNumber("maxSize", this.MaxSize);
        writer.WriteBoolean("warp", this.Warp);
        writer.WriteBoolean("twinkle", this.Twinkle);
    }
}
