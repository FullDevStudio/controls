using System.Globalization;
using System.Security;
using System.Text;

namespace Shiny.Controls.Media;

/// <summary>
/// The geometry behind VideoText (text whose letters are cut out of a looping video), shared so the MAUI
/// native masks and the Blazor SVG mask size and place the text the same way.
/// </summary>
public static class VideoTextLayout
{
    /// <summary>The share of the bounds auto-fitted text fills, so glyph edges never touch the control's edge.</summary>
    public const double AutoFitFill = 0.9;

    /// <summary>
    /// Break <paramref name="text"/> into the lines the mask draws. Only explicit line breaks split it:
    /// VideoText is display type, so it is sized to fit rather than wrapped.
    /// </summary>
    public static IReadOnlyList<string> SplitLines(string? text)
    {
        if (String.IsNullOrEmpty(text))
            return [];

        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    /// <summary>
    /// The font size that makes text measured as <paramref name="measuredWidth"/> × <paramref name="measuredHeight"/>
    /// at <paramref name="measuredAtSize"/> fill <see cref="AutoFitFill"/> of the bounds on its tighter axis.
    /// Text size scales linearly, which is what lets one measurement predict every other size.
    /// </summary>
    /// <returns>The fitted size, or 0 when there is nothing to measure or nowhere to put it.</returns>
    public static double FitFontSize(
        double measuredWidth,
        double measuredHeight,
        double measuredAtSize,
        double boundsWidth,
        double boundsHeight)
    {
        if (measuredWidth <= 0 || measuredHeight <= 0 || measuredAtSize <= 0 || boundsWidth <= 0 || boundsHeight <= 0)
            return 0;

        var byWidth = boundsWidth * AutoFitFill / measuredWidth;
        var byHeight = boundsHeight * AutoFitFill / measuredHeight;
        return measuredAtSize * Math.Min(byWidth, byHeight);
    }

    /// <summary>
    /// The font size to draw at: <paramref name="requestedSize"/> when it is positive, otherwise the auto-fit
    /// size from <see cref="FitFontSize"/>.
    /// </summary>
    public static double ResolveFontSize(
        double requestedSize,
        double measuredWidth,
        double measuredHeight,
        double measuredAtSize,
        double boundsWidth,
        double boundsHeight)
        => requestedSize > 0
            ? requestedSize
            : FitFontSize(measuredWidth, measuredHeight, measuredAtSize, boundsWidth, boundsHeight);

    /// <summary>
    /// The per-axis scale that turns a video stretched edge-to-edge over the view into one that covers the
    /// view at its own aspect ratio (cropping the overflow) — for outputs such as Android's TextureView,
    /// which only know how to stretch.
    /// </summary>
    /// <returns>(1, 1) until both sizes are known.</returns>
    public static (double ScaleX, double ScaleY) CoverScale(double videoWidth, double videoHeight, double viewWidth, double viewHeight)
    {
        if (videoWidth <= 0 || videoHeight <= 0 || viewWidth <= 0 || viewHeight <= 0)
            return (1, 1);

        var scale = Math.Max(viewWidth / videoWidth, viewHeight / videoHeight);
        return (videoWidth * scale / viewWidth, videoHeight * scale / viewHeight);
    }


    /// <summary>Line pitch as a multiple of the font size, for the SVG mask.</summary>
    public const double LineHeight = 1.2;

    /// <summary>
    /// A rough average advance for display faces, in ems. Only used to auto-fit before the browser has
    /// measured the real glyphs — a server-prerendered page — so the first paint is close rather than empty.
    /// </summary>
    public const double EstimatedAdvance = 0.62;

    /// <summary>
    /// Build the SVG used as a CSS mask image: white glyphs on transparent, centred, one <c>&lt;text&gt;</c>
    /// per line.
    /// </summary>
    /// <param name="lines">The lines from <see cref="SplitLines"/>.</param>
    /// <param name="fontSize">A fixed size in CSS pixels, or 0 to auto-fit.</param>
    /// <param name="lineWidthsAt100">
    /// Each line's measured width at 100px, for auto-fit. <c>null</c> (or a count mismatch) falls back to
    /// <see cref="EstimatedAdvance"/>.
    /// </param>
    /// <remarks>
    /// Auto-fit needs no knowledge of the element's size: the text is laid out at 100px inside a viewBox
    /// sized so it fills <see cref="AutoFitFill"/> of it, and <c>preserveAspectRatio="xMidYMid meet"</c>
    /// lets the browser scale that box into whatever the mask covers — the same tighter-axis rule as
    /// <see cref="FitFontSize"/>. A fixed size has no viewBox, so one SVG unit stays one CSS pixel.
    /// </remarks>
    public static string BuildSvgMask(
        IReadOnlyList<string> lines,
        double fontSize,
        string? fontFamily,
        string? fontWeight,
        string? fontStyle,
        IReadOnlyList<double>? lineWidthsAt100 = null)
    {
        var svg = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='100%' height='100%'");
        string x, centreY;
        double size;

        if (fontSize > 0)
        {
            size = fontSize;
            x = "50%";
            centreY = "50%";
            svg.Append('>');
        }
        else
        {
            size = 100;
            var widest = 0d;
            for (var i = 0; i < lines.Count; i++)
            {
                var width = lineWidthsAt100 is not null && lineWidthsAt100.Count == lines.Count
                    ? lineWidthsAt100[i]
                    : lines[i].Length * EstimatedAdvance * size;
                widest = Math.Max(widest, width);
            }

            var boxWidth = Math.Max(1, widest) / AutoFitFill;
            var boxHeight = Math.Max(1, lines.Count * LineHeight * size) / AutoFitFill;
            x = F(boxWidth / 2);
            centreY = F(boxHeight / 2);
            svg.Append(" viewBox='0 0 ").Append(F(boxWidth)).Append(' ').Append(F(boxHeight))
                .Append("' preserveAspectRatio='xMidYMid meet'>");
        }

        svg.Append("<g fill='white' text-anchor='middle' dominant-baseline='central' font-size='")
            .Append(F(size)).Append('\'');

        if (!String.IsNullOrWhiteSpace(fontFamily))
            svg.Append(" font-family='").Append(Escape(fontFamily)).Append('\'');
        if (!String.IsNullOrWhiteSpace(fontWeight))
            svg.Append(" font-weight='").Append(Escape(fontWeight)).Append('\'');
        if (!String.IsNullOrWhiteSpace(fontStyle))
            svg.Append(" font-style='").Append(Escape(fontStyle)).Append('\'');
        svg.Append('>');

        // lines are centred as a block: line i sits (i - (n-1)/2) line pitches from the middle
        var pitch = LineHeight * size;
        for (var i = 0; i < lines.Count; i++)
        {
            var dy = (i - (lines.Count - 1) / 2d) * pitch;
            svg.Append("<text x='").Append(x).Append("' y='").Append(centreY)
                .Append("' dy='").Append(F(dy)).Append("'>")
                .Append(Escape(lines[i]))
                .Append("</text>");
        }

        svg.Append("</g></svg>");
        return svg.ToString();
    }

    /// <summary>The SVG from <see cref="BuildSvgMask"/> as a <c>data:</c> URL ready for <c>mask-image: url("…")</c>.</summary>
    public static string ToDataUrl(string svg) => "data:image/svg+xml;charset=utf-8," + Uri.EscapeDataString(svg);

    static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    static string Escape(string value) => SecurityElement.Escape(value) ?? String.Empty;
}
