using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>A meteor shower: bright heads with fading tails streaking across at an angle, each after a random pause.</summary>
public class MeteorsEffect : BackgroundEffect
{
    /// <summary>The colour of the head; the tail fades from it to transparent. Default white.</summary>
    public string Color { get; set; } = "#ffffff";

    /// <summary>How many meteors share each 100×100 area (most are waiting their turn). Default 0.25.</summary>
    public double Density { get; set; } = 0.25;

    /// <summary>The direction they travel, in degrees clockwise from pointing right - 90 is straight down. Default 135 (down and to the left).</summary>
    public double Angle { get; set; } = 135;

    /// <summary>How long the tails are. Default 90.</summary>
    public double Length { get; set; } = 90;

    /// <summary>How thick the head end of the tail is. Default 1.5.</summary>
    public double Thickness { get; set; } = 1.5;

    /// <summary>The longest a meteor waits before it streaks across again. Default 5 seconds.</summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);

    internal override string Kind => "meteors";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColor(writer, "color", this.Color);
        writer.WriteNumber("density", this.Density);
        writer.WriteNumber("angle", this.Angle);
        writer.WriteNumber("length", this.Length);
        writer.WriteNumber("thickness", this.Thickness);
        writer.WriteNumber("maxDelay", this.MaxDelay.TotalSeconds);
    }
}
