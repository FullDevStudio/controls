using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>Drifting particles joined by faint lines when they come close - the "constellation" or network background - optionally pushed around by the pointer.</summary>
public class ParticlesEffect : BackgroundEffect
{
    /// <summary>The colours particles are picked from. Default indigo, pink and cyan.</summary>
    public IReadOnlyList<string> Colors { get; set; } = ["#a5b4fc", "#f0abfc", "#67e8f9"];

    /// <summary>How many particles fill each 100×100 area (at most 400 in all). Default 2.5.</summary>
    public double Density { get; set; } = 2.5;

    /// <summary>The smallest particle's radius. Default 1.</summary>
    public double MinSize { get; set; } = 1;

    /// <summary>The largest particle's radius. Default 2.5.</summary>
    public double MaxSize { get; set; } = 2.5;

    /// <summary>How close two particles must be to be joined by a line. Default 110; 0 draws no lines.</summary>
    public double LinkDistance { get; set; } = 110;

    /// <summary>The colour of the joining lines. Unset uses the first of <see cref="Colors"/>.</summary>
    public string? LinkColor { get; set; }

    /// <summary>How strong a line between two touching particles is; it fades out with distance. Default 0.35.</summary>
    public double LinkOpacity { get; set; } = 0.35;

    /// <summary>What the pointer does to nearby particles. Default <see cref="ParticlePointerMode.Repel"/>.</summary>
    public ParticlePointerMode PointerMode { get; set; } = ParticlePointerMode.Repel;

    /// <summary>How far from the pointer particles feel it. Default 120.</summary>
    public double PointerRadius { get; set; } = 120;

    internal override string Kind => "particles";

    private protected override void WriteOptions(Utf8JsonWriter writer)
    {
        WriteColors(writer, "colors", this.Colors);
        writer.WriteNumber("density", this.Density);
        writer.WriteNumber("minSize", this.MinSize);
        writer.WriteNumber("maxSize", this.MaxSize);
        writer.WriteNumber("linkDistance", this.LinkDistance);
        WriteColor(writer, "linkColor", this.LinkColor);
        writer.WriteNumber("linkOpacity", this.LinkOpacity);
        writer.WriteString("pointerMode", this.PointerMode.ToString().ToLowerInvariant());
        writer.WriteNumber("pointerRadius", this.PointerRadius);
    }
}
