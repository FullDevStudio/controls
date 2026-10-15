using System.Globalization;

namespace Shiny.Blazor.Controls;

/// <summary>
/// The text half of an avatar: initials from a name, and the stable hue a name maps to.
/// </summary>
/// <remarks>
/// The MAUI host carries an identical copy (<c>Shiny.Maui.Controls.AvatarText</c>). They have to
/// agree byte for byte - the same person must get the same colour in a web app and its MAUI twin -
/// so the hash is spelled out here rather than borrowed from <see cref="string.GetHashCode()"/>,
/// which is randomised per process.
/// </remarks>
static class AvatarText
{
    /// <summary>
    /// Up to two initials: the first letter of the first word and of the last word. An email address
    /// contributes only the part before the <c>@</c>. Text elements, not chars, so an accented or
    /// surrogate-pair first letter survives intact.
    /// </summary>
    public static string GetInitials(string? name)
    {
        if (String.IsNullOrWhiteSpace(name))
            return String.Empty;

        var source = name.Trim();
        var at = source.IndexOf('@');
        if (at > 0)
            source = source[..at];

        var words = source.Split([' ', '\t', '.', '_', '-', ','], StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return String.Empty;

        var first = FirstElement(words[0]);
        if (words.Length == 1)
            return first;

        return first + FirstElement(words[^1]);
    }


    /// <summary>A hue in [0, 360) that a given name always maps to. FNV-1a over the upper-cased name.</summary>
    public static int GetHue(string? name)
    {
        if (String.IsNullOrWhiteSpace(name))
            return 0;

        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in name.Trim().ToUpperInvariant())
            {
                hash ^= c;
                hash *= 16777619u;
            }
            return (int)(hash % 360u);
        }
    }


    /// <summary>The "+N" text for an overflow chip.</summary>
    public static string FormatOverflow(string? format, int count)
        => String.Format(CultureInfo.CurrentCulture, String.IsNullOrEmpty(format) ? "+{0}" : format, count);


    static string FirstElement(string word)
    {
        var element = StringInfo.GetNextTextElement(word);
        return element.ToUpper(CultureInfo.CurrentCulture);
    }
}
