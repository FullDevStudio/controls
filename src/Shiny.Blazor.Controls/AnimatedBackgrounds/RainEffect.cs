using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>Slanted streaks of rain, nearer drops longer and brighter, splashing where they land.</summary>
public class RainEffect : BackgroundEffect
{
    /// <summary>The colour of the drops. Default a pale blue.</summary>
    public string Color { get; set; } = "#a5b4fc";

    /// <summary>How many drops fill each 100×100 area. Default 8.</summary>
    public double Density { get; set; } = 8;

    /// <summary>How far the rain leans from vertical, in degrees - positive falls to the right. Default 12.</summary>
    public double Angle { get; set; } = 12;

    /// <summary>How long the nearest drops are. Default 18.</summary>
    public double Length { get; set; } = 18;

    /// <summary>How thick the drops are. Default 1.</summary>
    public double Thickness { get; set; } = 1;

    /// <summary>Whether drops splash at the bottom edge. Default true.</summary>
    public bool Splash { get; set; } = true;

    internal override string Kind => "rain";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColor(writer, "color", this.Color);
        writer.WriteNumber("density", this.Density);
        writer.WriteNumber("angle", this.Angle);
        writer.WriteNumber("length", this.Length);
        writer.WriteNumber("thickness", this.Thickness);
        writer.WriteBoolean("splash", this.Splash);
    }
}
