namespace Shiny.Maui.Controls.Media;

/// <summary>
/// The native output behind a <see cref="VideoText"/>: a <see cref="MediaSurface"/> whose platform view
/// masks the video to the text. Inheriting the surface means the backend binding (and re-binding when a
/// player is swapped in) is the same code path MediaElement uses.
/// </summary>
/// <remarks>
/// The text properties are plain CLR properties pushed by the owning <see cref="VideoText"/>, which then
/// asks the handler to re-map <see cref="Text"/> (one mapper entry rebuilds the whole mask — text, overlay
/// and marquee — since most of them change the glyph geometry or which layers exist).
/// </remarks>
class VideoTextSurface : MediaSurface
{
    public string? Text { get; set; }
    public string? FontFamily { get; set; }
    public double FontSize { get; set; }
    public FontAttributes FontAttributes { get; set; } = FontAttributes.Bold;
    public Color? OverlayColor { get; set; }
    public double OverlayOpacity { get; set; } = 1;
    public MarqueeDirection Marquee { get; set; }
    public TimeSpan MarqueeDuration { get; set; }
    public Size VideoSize { get; set; }

    /// <summary>Whether to draw the overlay sheet with the text knocked out, rather than clip the video to the letters.</summary>
    internal bool HasOverlay => this.OverlayColor is { Alpha: > 0 };

    /// <summary>The marquee to run, or <see cref="MarqueeDirection.None"/> when it has no length to run in.</summary>
    internal MarqueeDirection ActiveMarquee => this.MarqueeDuration > TimeSpan.Zero ? this.Marquee : MarqueeDirection.None;

    internal Microsoft.Maui.Font ToFont(double size) => Microsoft.Maui.Font.OfSize(
        this.FontFamily,
        size,
        this.FontAttributes.HasFlag(FontAttributes.Bold) ? FontWeight.Bold : FontWeight.Regular,
        this.FontAttributes.HasFlag(FontAttributes.Italic) ? FontSlant.Italic : FontSlant.Default,
        enableScaling: false);
}
