using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>Translucent bubbles rising and wobbling, each with a little highlight.</summary>
public class BubblesEffect : BackgroundEffect
{
    /// <summary>The colours bubbles are picked from. Default sky, periwinkle and pink.</summary>
    public IReadOnlyList<string> Colors { get; set; } = ["#7dd3fc", "#a5b4fc", "#f0abfc"];

    /// <summary>How many bubbles fill each 100×100 area. Default 0.6.</summary>
    public double Density { get; set; } = 0.6;

    /// <summary>The smallest bubble's radius. Default 4.</summary>
    public double MinSize { get; set; } = 4;

    /// <summary>The largest bubble's radius. Default 18.</summary>
    public double MaxSize { get; set; } = 18;

    internal override string Kind => "bubbles";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColors(writer, "colors", this.Colors);
        writer.WriteNumber("density", this.Density);
        writer.WriteNumber("minSize", this.MinSize);
        writer.WriteNumber("maxSize", this.MaxSize);
    }
}
