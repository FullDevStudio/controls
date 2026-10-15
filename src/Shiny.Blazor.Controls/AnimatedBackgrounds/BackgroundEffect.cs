using System.Text;
using System.Text.Json;

namespace Shiny.Blazor.Controls;

/// <summary>
/// What an <see cref="AnimatedBackground"/> draws behind its content - aurora, snow, rain, meteors and
/// so on. Each effect is a plain settings object; colours are any CSS colour.
/// </summary>
/// <remarks>
/// The settings are sent to the browser as JSON written by hand, which keeps the component trim and
/// AOT safe. The background compares what would be sent rather than the object reference, so changing
/// a property on the same instance and re-rendering takes effect too.
/// </remarks>
public abstract class BackgroundEffect
{
    /// <summary>How fast it moves, as a multiplier of the effect's natural pace. Default 1; 0 holds it still.</summary>
    public double Speed { get; set; } = 1;

    /// <summary>The name the script knows this effect by.</summary>
    internal abstract string Kind { get; }

    private protected abstract void WriteOptions(Utf8JsonWriter writer);

    internal string ToJson()
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("kind", this.Kind);
            writer.WriteNumber("speed", this.Speed);
            this.WriteOptions(writer);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private protected static void WriteColor(Utf8JsonWriter writer, string name, string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
            writer.WriteNull(name);
        else
            writer.WriteString(name, color);
    }

    private protected static void WriteColors(Utf8JsonWriter writer, string name, IReadOnlyList<string>? colors)
    {
        writer.WriteStartArray(name);
        if (colors is not null)
        {
            foreach (var color in colors)
            {
                if (!string.IsNullOrWhiteSpace(color))
                    writer.WriteStringValue(color);
            }
        }
        writer.WriteEndArray();
    }
}


/// <summary>How <see cref="ParticlesEffect"/> reacts to the pointer.</summary>
public enum ParticlePointerMode
{
    /// <summary>Ignores it.</summary>
    None,

    /// <summary>Particles near the pointer are pushed away.</summary>
    Repel,

    /// <summary>Particles near the pointer are drawn towards it.</summary>
    Attract
}


/// <summary>Where the spotlight over a <see cref="DotPatternEffect"/> goes.</summary>
public enum SpotlightMode
{
    /// <summary>No spotlight - a plain dot grid.</summary>
    None,

    /// <summary>Follows the pointer, and goes out when the pointer leaves.</summary>
    Pointer,

    /// <summary>Wanders slowly around the box on its own.</summary>
    Wander,

    /// <summary>Follows the pointer while it is over the box, and wanders otherwise.</summary>
    Auto
}
