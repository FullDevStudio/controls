using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>A grid of dots, lit up and enlarged under a soft spotlight that follows the pointer or wanders on its own, with an optional ripple of light running through the grid.</summary>
public class DotPatternEffect : BackgroundEffect
{
    /// <summary>The colour of the dots. Default slate grey, which reads on light and dark.</summary>
    public string Color { get; set; } = "#94a3b8";

    /// <summary>The distance between neighbouring dots. Default 18.</summary>
    public double Spacing { get; set; } = 18;

    /// <summary>The radius of each dot. Default 1.2.</summary>
    public double DotSize { get; set; } = 1.2;

    /// <summary>How visible the dots are away from the spotlight. Default 0.35.</summary>
    public double DotOpacity { get; set; } = 0.35;

    /// <summary>Where the spotlight goes. Default <see cref="SpotlightMode.Auto"/>.</summary>
    public SpotlightMode Spotlight { get; set; } = SpotlightMode.Auto;

    /// <summary>The colour of lit dots and of the glow. Unset uses <see cref="Color"/>.</summary>
    public string? SpotlightColor { get; set; }

    /// <summary>How far the spotlight reaches. Default 140.</summary>
    public double SpotlightRadius { get; set; } = 140;

    /// <summary>How strong the soft glow under the spotlight is, from 0 to 1. Default 0.12.</summary>
    public double Glow { get; set; } = 0.12;

    /// <summary>Runs a slow diagonal ripple of light through the grid. Default false.</summary>
    public bool Pulse { get; set; } = false;

    internal override string Kind => "dotPattern";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColor(writer, "color", this.Color);
        writer.WriteNumber("spacing", this.Spacing);
        writer.WriteNumber("dotSize", this.DotSize);
        writer.WriteNumber("dotOpacity", this.DotOpacity);
        writer.WriteString("spotlight", this.Spotlight.ToString().ToLowerInvariant());
        WriteColor(writer, "spotlightColor", this.SpotlightColor);
        writer.WriteNumber("spotlightRadius", this.SpotlightRadius);
        writer.WriteNumber("glow", this.Glow);
        writer.WriteBoolean("pulse", this.Pulse);
    }
}
