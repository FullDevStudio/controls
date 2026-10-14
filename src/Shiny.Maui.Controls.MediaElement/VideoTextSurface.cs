namespace Shiny.Maui.Controls.Media;

/// <summary>
/// The native output behind a <see cref="VideoText"/>: a <see cref="MediaSurface"/> whose platform view
/// masks the video to the text. Inheriting the surface means the backend binding (and re-binding when a
/// player is swapped in) is the same code path MediaElement uses.
/// </summary>
/// <remarks>
/// The text properties are plain CLR properties pushed by the owning <see cref="VideoText"/>, which then
/// asks the handler to re-map <see cref="Text"/> (one mapper entry redraws the whole mask, since any one of
/// them changes the glyph geometry).
/// </remarks>
class VideoTextSurface : MediaSurface
{
    public string? Text { get; set; }
    public string? FontFamily { get; set; }
    public double FontSize { get; set; }
    public FontAttributes FontAttributes { get; set; } = FontAttributes.Bold;
    public Size VideoSize { get; set; }

    internal Microsoft.Maui.Font ToFont(double size) => Microsoft.Maui.Font.OfSize(
        this.FontFamily,
        size,
        this.FontAttributes.HasFlag(FontAttributes.Bold) ? FontWeight.Bold : FontWeight.Regular,
        this.FontAttributes.HasFlag(FontAttributes.Italic) ? FontSlant.Italic : FontSlant.Default,
        enableScaling: false);
}
