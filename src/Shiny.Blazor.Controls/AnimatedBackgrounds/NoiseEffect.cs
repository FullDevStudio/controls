using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>Flickering film grain. On its own it is TV static; at a low <see cref="Intensity"/> over a background gradient it adds the subtle texture that stops flat colour looking digital.</summary>
public class NoiseEffect : BackgroundEffect
{
    /// <summary>The colour of the grain. Default white; black suits a light background.</summary>
    public string Color { get; set; } = "#ffffff";

    /// <summary>How visible the grain is, from 0 to 1. Default 0.15.</summary>
    public double Intensity { get; set; } = 0.15;

    /// <summary>How big each speck is. Default 2.</summary>
    public double GrainSize { get; set; } = 2;

    /// <summary>The fraction of the area covered in specks, from 0 to 1. Default 0.3.</summary>
    public double Coverage { get; set; } = 0.3;

    /// <summary>How many times a second the grain changes (scaled by <see cref="BackgroundEffect.Speed"/>). Default 12, a film's flicker.</summary>
    public double FrameRate { get; set; } = 12;

    internal override string Kind => "noise";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColor(writer, "color", this.Color);
        writer.WriteNumber("intensity", this.Intensity);
        writer.WriteNumber("grainSize", this.GrainSize);
        writer.WriteNumber("coverage", this.Coverage);
        writer.WriteNumber("frameRate", this.FrameRate);
    }
}
