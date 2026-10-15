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
    /// <see cref="FitFontSize(double,double,double,double,double)"/> for scrolling text: a marquee runs the
    /// text off the control along its axis, so only the <i>cross</i> axis bounds it — the height for
    /// <see cref="MarqueeDirection.Left"/>/<see cref="MarqueeDirection.Right"/>, the width for
    /// <see cref="MarqueeDirection.Up"/>/<see cref="MarqueeDirection.Down"/>.
    /// </summary>
    public static double FitFontSize(
        double measuredWidth,
        double measuredHeight,
        double measuredAtSize,
        double boundsWidth,
        double boundsHeight,
        MarqueeDirection marquee)
    {
        if (marquee == MarqueeDirection.None)
            return FitFontSize(measuredWidth, measuredHeight, measuredAtSize, boundsWidth, boundsHeight);

        if (measuredWidth <= 0 || measuredHeight <= 0 || measuredAtSize <= 0 || boundsWidth <= 0 || boundsHeight <= 0)
            return 0;

        return IsHorizontal(marquee)
            ? measuredAtSize * boundsHeight * AutoFitFill / measuredHeight
            : measuredAtSize * boundsWidth * AutoFitFill / measuredWidth;
    }

    /// <summary>
    /// The font size to draw at: <paramref name="requestedSize"/> when it is positive, otherwise the auto-fit
    /// size for <paramref name="marquee"/>.
    /// </summary>
    public static double ResolveFontSize(
        double requestedSize,
        double measuredWidth,
        double measuredHeight,
        double measuredAtSize,
        double boundsWidth,
        double boundsHeight,
        MarqueeDirection marquee = MarqueeDirection.None)
        => requestedSize > 0
            ? requestedSize
            : FitFontSize(measuredWidth, measuredHeight, measuredAtSize, boundsWidth, boundsHeight, marquee);

    /// <summary>Whether <paramref name="marquee"/> scrolls along the x axis.</summary>
    public static bool IsHorizontal(MarqueeDirection marquee)
        => marquee is MarqueeDirection.Left or MarqueeDirection.Right;

    /// <summary>
    /// Where the centre of the text block sits, relative to the centre of the view, <paramref name="progress"/>
    /// of the way through one marquee pass (screen coordinates: +y is down).
    /// </summary>
    /// <remarks>
    /// One pass runs from the text sitting just outside the leading edge to just outside the trailing one,
    /// so progress 0 and 1 both leave the view empty and the loop restarts without a visible jump.
    /// Progress is not wrapped — pass a value in [0, 1].
    /// </remarks>
    public static (double X, double Y) MarqueeOffset(
        MarqueeDirection marquee,
        double progress,
        double viewWidth,
        double viewHeight,
        double textWidth,
        double textHeight)
    {
        var runX = viewWidth + textWidth;
        var runY = viewHeight + textHeight;

        return marquee switch
        {
            MarqueeDirection.Left => (runX / 2 - progress * runX, 0),
            MarqueeDirection.Right => (-runX / 2 + progress * runX, 0),
            MarqueeDirection.Up => (0, runY / 2 - progress * runY),
            MarqueeDirection.Down => (0, -runY / 2 + progress * runY),
            _ => (0, 0)
        };
    }


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
    /// per line. Only the letters show the video; everything around them is transparent.
    /// </summary>
    /// <param name="lines">The lines from <see cref="SplitLines"/>.</param>
    /// <param name="fontSize">A fixed size in CSS pixels, or 0 to auto-fit.</param>
    /// <param name="lineWidthsAt100">
    /// Each line's measured width at 100px, for auto-fit and marquee travel. <c>null</c> (or a count
    /// mismatch) falls back to <see cref="EstimatedAdvance"/>.
    /// </param>
    /// <param name="marquee">The scroll direction; only animates once <paramref name="viewWidth"/> and <paramref name="viewHeight"/> are known.</param>
    /// <param name="marqueeSeconds">The length of one pass across the view.</param>
    /// <param name="viewWidth">The element's width in CSS pixels, or 0 when not yet measured.</param>
    /// <param name="viewHeight">The element's height in CSS pixels, or 0 when not yet measured.</param>
    /// <remarks>
    /// Static auto-fit needs no knowledge of the element's size: the text is laid out at 100px inside a
    /// viewBox sized so it fills <see cref="AutoFitFill"/> of it, and <c>preserveAspectRatio="xMidYMid meet"</c>
    /// lets the browser scale that box into whatever the mask covers — the same tighter-axis rule as
    /// <see cref="FitFontSize(double,double,double,double,double)"/>. A fixed size, or a marquee (whose
    /// travel is in pixels), lays out in CSS pixels instead and scrolls with a CSS animation declared in
    /// the SVG's own <c>&lt;style&gt;</c>, which keeps running inside an SVG used as an image.
    /// </remarks>
    public static string BuildSvgMask(
        IReadOnlyList<string> lines,
        double fontSize,
        string? fontFamily,
        string? fontWeight,
        string? fontStyle,
        IReadOnlyList<double>? lineWidthsAt100 = null,
        MarqueeDirection marquee = MarqueeDirection.None,
        double marqueeSeconds = 0,
        double viewWidth = 0,
        double viewHeight = 0)
    {
        var svg = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='100%' height='100%'>");
        AppendText(svg, "shiny-vt", "white", lines, fontSize, fontFamily, fontWeight, fontStyle, lineWidthsAt100, marquee, marqueeSeconds, viewWidth, viewHeight);
        svg.Append("</svg>");
        return svg.ToString();
    }

    /// <summary>
    /// Build an inline SVG overlay: a rectangle of <paramref name="overlayColor"/> covering the whole element
    /// with the text knocked out of it, so the video underneath shows at full strength through the letters
    /// and dimmed by the overlay everywhere else.
    /// </summary>
    /// <param name="overlayColor">Any SVG/CSS colour.</param>
    /// <param name="overlayOpacity">The overlay's opacity, 0–1. Below 1 the video shows faintly around the letters.</param>
    /// <param name="maskId">A document-unique id for the knockout mask; also names the marquee keyframes, so it must be a valid CSS identifier.</param>
    /// <remarks>
    /// Meant to be placed in the page (not used as an image), so unlike <see cref="BuildSvgMask"/> it can use
    /// the page's web fonts. The other parameters are as for <see cref="BuildSvgMask"/>.
    /// </remarks>
    public static string BuildSvgOverlay(
        IReadOnlyList<string> lines,
        double fontSize,
        string? fontFamily,
        string? fontWeight,
        string? fontStyle,
        string overlayColor,
        double overlayOpacity,
        string maskId,
        IReadOnlyList<double>? lineWidthsAt100 = null,
        MarqueeDirection marquee = MarqueeDirection.None,
        double marqueeSeconds = 0,
        double viewWidth = 0,
        double viewHeight = 0)
    {
        var id = Escape(maskId);
        var svg = new StringBuilder("<svg xmlns='http://www.w3.org/2000/svg' width='100%' height='100%' aria-hidden='true'>")
            .Append("<defs><mask id='").Append(id)
            .Append("' maskUnits='userSpaceOnUse' x='0' y='0' width='100%' height='100%'>")
            .Append("<rect width='100%' height='100%' fill='white'/>");

        AppendText(svg, id, "black", lines, fontSize, fontFamily, fontWeight, fontStyle, lineWidthsAt100, marquee, marqueeSeconds, viewWidth, viewHeight);

        svg.Append("</mask></defs><rect width='100%' height='100%' fill='").Append(Escape(overlayColor))
            .Append("' opacity='").Append(F(Math.Clamp(overlayOpacity, 0, 1)))
            .Append("' mask='url(#").Append(id).Append(")'/></svg>");
        return svg.ToString();
    }

    static void AppendText(
        StringBuilder svg,
        string id,
        string fill,
        IReadOnlyList<string> lines,
        double fontSize,
        string? fontFamily,
        string? fontWeight,
        string? fontStyle,
        IReadOnlyList<double>? lineWidthsAt100,
        MarqueeDirection marquee,
        double marqueeSeconds,
        double viewWidth,
        double viewHeight)
    {
        if (lines.Count == 0)
            return;

        var widestAt100 = 0d;
        for (var i = 0; i < lines.Count; i++)
        {
            var width = lineWidthsAt100 is not null && lineWidthsAt100.Count == lines.Count
                ? lineWidthsAt100[i]
                : lines[i].Length * EstimatedAdvance * 100;
            widestAt100 = Math.Max(widestAt100, width);
        }

        var scrolling = marquee != MarqueeDirection.None && marqueeSeconds > 0 && viewWidth > 0 && viewHeight > 0;
        string x, centreY;
        double size;
        var nested = false;

        if (fontSize > 0 || scrolling)
        {
            size = fontSize > 0
                ? fontSize
                : FitFontSize(Math.Max(1, widestAt100), lines.Count * LineHeight * 100, 100, viewWidth, viewHeight, marquee);
            if (size <= 0)
                return;

            x = "50%";
            centreY = "50%";
        }
        else
        {
            // auto-fit without a known size: a nested viewport that scales itself into the element
            size = 100;
            var boxWidth = Math.Max(1, widestAt100) / AutoFitFill;
            var boxHeight = Math.Max(1, lines.Count * LineHeight * size) / AutoFitFill;
            x = F(boxWidth / 2);
            centreY = F(boxHeight / 2);
            nested = true;
            svg.Append("<svg width='100%' height='100%' viewBox='0 0 ").Append(F(boxWidth)).Append(' ').Append(F(boxHeight))
                .Append("' preserveAspectRatio='xMidYMid meet'>");
        }

        if (scrolling)
        {
            // A CSS animation, not SMIL: Chrome leaves an <animateTransform> in inline SVG frozen on its
            // first frame after some re-inserts, while @keyframes run both inline and inside an SVG image.
            var textWidth = widestAt100 * size / 100;
            var textHeight = lines.Count * LineHeight * size;
            var (fromX, fromY) = MarqueeOffset(marquee, 0, viewWidth, viewHeight, textWidth, textHeight);
            var (toX, toY) = MarqueeOffset(marquee, 1, viewWidth, viewHeight, textWidth, textHeight);

            svg.Append("<style>@keyframes ").Append(id).Append("-marquee{from{transform:translate(")
                .Append(F(fromX)).Append("px,").Append(F(fromY)).Append("px)}to{transform:translate(")
                .Append(F(toX)).Append("px,").Append(F(toY)).Append("px)}}#").Append(id)
                .Append("-text{animation:").Append(id).Append("-marquee ").Append(F(marqueeSeconds))
                .Append("s linear infinite}</style>");
        }

        svg.Append("<g");
        if (scrolling)
            svg.Append(" id='").Append(id).Append("-text'");
        svg.Append(" fill='").Append(fill).Append("' text-anchor='middle' dominant-baseline='central' font-size='")
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

        svg.Append("</g>");
        if (nested)
            svg.Append("</svg>");
    }

    /// <summary>The SVG from <see cref="BuildSvgMask"/> as a <c>data:</c> URL ready for <c>mask-image: url("…")</c>.</summary>
    public static string ToDataUrl(string svg) => "data:image/svg+xml;charset=utf-8," + Uri.EscapeDataString(svg);

    static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    static string Escape(string value) => SecurityElement.Escape(value) ?? String.Empty;
}
