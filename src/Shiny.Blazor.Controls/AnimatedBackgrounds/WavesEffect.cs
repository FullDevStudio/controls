using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>Layered, translucent waves rolling across the bottom of the box, one layer per colour.</summary>
public class WavesEffect : BackgroundEffect
{
    /// <summary>One wave per colour, back to front. Default sky, blue and indigo.</summary>
    public IReadOnlyList<string> Colors { get; set; } = ["#38bdf8", "#3b82f6", "#6366f1"];

    /// <summary>How opaque each layer is, so the ones behind show through. Default 0.45.</summary>
    public double Opacity { get; set; } = 0.45;

    /// <summary>How tall the waves are. Default 18.</summary>
    public double Amplitude { get; set; } = 18;

    /// <summary>The distance from one crest to the next. Default 320.</summary>
    public double Wavelength { get; set; } = 320;

    /// <summary>Where the back wave's surface sits, as a fraction of the height from the top. Default 0.65.</summary>
    public double Level { get; set; } = 0.65;

    internal override string Kind => "waves";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColors(writer, "colors", this.Colors);
        writer.WriteNumber("opacity", this.Opacity);
        writer.WriteNumber("amplitude", this.Amplitude);
        writer.WriteNumber("wavelength", this.Wavelength);
        writer.WriteNumber("level", this.Level);
    }
}
