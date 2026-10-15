using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>Soft, glowing washes of colour drifting slowly over one another, like the northern lights. One glow per colour.</summary>
public class AuroraEffect : BackgroundEffect
{
    /// <summary>The colours of the glows, one glow each. Default cyan, indigo, violet and emerald.</summary>
    public IReadOnlyList<string> Colors { get; set; } = ["#22d3ee", "#6366f1", "#a855f7", "#10b981"];

    /// <summary>How strong the glows are at their centres, from 0 to 1. Default 0.6.</summary>
    public double Intensity { get; set; } = 0.6;

    /// <summary>How big each glow is, as a fraction of the box's longer side. Default 0.65.</summary>
    public double Size { get; set; } = 0.65;

    internal override string Kind => "aurora";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColors(writer, "colors", this.Colors);
        writer.WriteNumber("intensity", this.Intensity);
        writer.WriteNumber("size", this.Size);
    }
}
