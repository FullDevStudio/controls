using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>Glowing specks wandering lazily and pulsing in and out, like fireflies at dusk.</summary>
public class FirefliesEffect : BackgroundEffect
{
    /// <summary>The colours fireflies are picked from. Default warm yellows and a lime.</summary>
    public IReadOnlyList<string> Colors { get; set; } = ["#fde68a", "#facc15", "#bef264"];

    /// <summary>How many fireflies fill each 100×100 area. Default 0.5.</summary>
    public double Density { get; set; } = 0.5;

    /// <summary>The radius of each bright core. Default 2.5.</summary>
    public double Size { get; set; } = 2.5;

    /// <summary>How far the glow spreads, as a multiple of <see cref="Size"/>. Default 5; 0 for no glow.</summary>
    public double Glow { get; set; } = 5;

    internal override string Kind => "fireflies";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColors(writer, "colors", this.Colors);
        writer.WriteNumber("density", this.Density);
        writer.WriteNumber("size", this.Size);
        writer.WriteNumber("glow", this.Glow);
    }
}
