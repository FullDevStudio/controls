using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>Snowflakes drifting down, the larger ones nearer and faster, swaying as they fall.</summary>
public class SnowEffect : BackgroundEffect
{
    /// <summary>The colour of the flakes, as any CSS colour. Default white.</summary>
    public string Color { get; set; } = "#ffffff";

    /// <summary>How many flakes fill each 100×100 CSS-pixel area. Default 5.</summary>
    public double Density { get; set; } = 5;

    /// <summary>The smallest flake's radius. Default 1.</summary>
    public double MinSize { get; set; } = 1;

    /// <summary>The largest flake's radius. Default 3.5.</summary>
    public double MaxSize { get; set; } = 3.5;

    /// <summary>A steady sideways push: negative blows left, positive right, about -1 to 1. Default 0.</summary>
    public double Wind { get; set; } = 0;

    /// <summary>How far flakes rock side to side as they fall, as a multiplier. Default 1; 0 falls straight.</summary>
    public double Sway { get; set; } = 1;

    internal override string Kind => "snow";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColor(writer, "color", this.Color);
        writer.WriteNumber("density", this.Density);
        writer.WriteNumber("minSize", this.MinSize);
        writer.WriteNumber("maxSize", this.MaxSize);
        writer.WriteNumber("wind", this.Wind);
        writer.WriteNumber("sway", this.Sway);
    }
}
