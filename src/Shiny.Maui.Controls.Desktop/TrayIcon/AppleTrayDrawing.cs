#if MACOS || MACCATALYST
using CoreGraphics;
using CoreText;
using Foundation;

namespace Shiny.Maui.Controls.Desktop.TrayIcon;

/// <summary>
/// The drawing behind tray pills and progress rows, shared by the AppKit and MacCatalyst heads.
/// It is CoreGraphics + CoreText only, so neither head's AppKit binding (managed on macOS, raw
/// objc_msgSend on Catalyst) leaks in; each head resolves its own colours and hands them over.
/// </summary>
static class AppleTrayDrawing
{
    // Progress row metrics, in points. The row is drawn into a flipped view (origin top-left).
    public const double ProgressWidth = 260;
    public const double TrailingInset = 14;
    const double TopPad = 4;
    const double LabelLine = 16;
    const double BarGap = 5;
    const double BarHeight = 6;
    const double CaptionGap = 4;
    const double CaptionLine = 13;
    const double BottomPad = 6;

    // Pill metrics, in points.
    const double PillHeight = 16;
    const double PillPadding = 6;

    public static double ProgressHeight(bool hasCaption)
        => TopPad + LabelLine + BarGap + BarHeight + BottomPad + (hasCaption ? CaptionGap + CaptionLine : 0);

    public readonly record struct ProgressColors(CGColor Label, CGColor Secondary, CGColor Track, CGColor Fill);

    /// <summary>Draws a progress row into a flipped context whose bounds are the row.</summary>
    public static void DrawProgress(CGContext ctx, CGRect bounds, TrayProgressItem item, ProgressColors colors, double leading)
    {
        using var labelFont = new CTFont(CTFontUIFontType.MenuItem, 0, null);
        using var smallFont = new CTFont(CTFontUIFontType.System, 11, null);

        var left = leading;
        var right = (double)bounds.Width - TrailingInset;
        var width = Math.Max(0, right - left);

        // label row: label left, detail right-aligned, label truncated so the two never overlap
        var labelBaseline = TopPad + labelFont.AscentMetric + (LabelLine - (labelFont.AscentMetric + labelFont.DescentMetric)) / 2;
        var detail = item.DisplayDetail;
        double detailWidth = 0;
        if (!string.IsNullOrEmpty(detail))
        {
            using var detailLine = MakeLine(detail, smallFont, colors.Secondary);
            detailWidth = detailLine.GetTypographicBounds();
            DrawLine(ctx, detailLine, right - detailWidth, labelBaseline);
        }
        var labelMax = Math.Max(0, width - (detailWidth > 0 ? detailWidth + 8 : 0));
        using (var labelLine = MakeLine(item.Label, labelFont, colors.Label))
        using (var truncated = TruncateOrNull(labelLine, labelMax, labelFont, colors.Label))
            DrawLine(ctx, truncated ?? labelLine, left, labelBaseline);

        // bar: full-width rounded track, rounded fill from the left
        var barTop = TopPad + LabelLine + BarGap;
        var track = new CGRect(left, barTop, width, BarHeight);
        FillRounded(ctx, track, colors.Track);
        if (item.Value > 0)
        {
            // never narrower than the bar is tall, or the rounded ends fold into a sliver
            var fillWidth = Math.Max(BarHeight, width * item.Value);
            FillRounded(ctx, new CGRect(left, barTop, Math.Min(width, fillWidth), BarHeight), colors.Fill);
        }

        if (item.HasCaption)
        {
            var captionBaseline = barTop + BarHeight + CaptionGap + smallFont.AscentMetric;
            using var captionLine = MakeLine(item.Caption!, smallFont, colors.Secondary);
            using var truncated = TruncateOrNull(captionLine, width, smallFont, colors.Secondary);
            DrawLine(ctx, truncated ?? captionLine, left, captionBaseline);
        }
    }

    /// <summary>
    /// Renders a pill as a bitmap at <paramref name="scale"/>x. Pills are solid with white text so
    /// they stay legible on light, dark and highlighted (accent-filled) menu rows alike.
    /// </summary>
    public static (CGImage Image, CGSize PointSize) RenderPill(string text, CGColor fill, nfloat scale)
    {
        using var font = new CTFont(CTFontUIFontType.EmphasizedSystem, 10, null);
        using var line = MakeLine(text, font, new CGColor(1, 1, 1, 1));
        var textWidth = line.GetTypographicBounds();
        var w = Math.Ceiling(Math.Max(PillHeight, textWidth + PillPadding * 2));
        var h = PillHeight;

        using var space = CGColorSpace.CreateSrgb();
        using var bmp = new CGBitmapContext(null, (nint)(w * scale), (nint)(h * scale), 8, 0, space, CGImageAlphaInfo.PremultipliedLast);
        bmp.ScaleCTM(scale, scale);
        FillRounded(bmp, new CGRect(0, 0, w, h), fill);

        // bitmap contexts are unflipped: baseline measured up from the bottom edge
        var baseline = (h - (font.AscentMetric + font.DescentMetric)) / 2 + font.DescentMetric;
        bmp.TextMatrix = CGAffineTransform.MakeIdentity();
        bmp.TextPosition = new CGPoint((w - textWidth) / 2, baseline);
        line.Draw(bmp);

        return (bmp.ToImage()!, new CGSize(w, h));
    }

    /// <summary>Vertical offset that centres a pill attachment on the menu font's cap height.</summary>
    public static double PillBaselineOffset()
    {
        using var font = new CTFont(CTFontUIFontType.MenuItem, 0, null);
        return Math.Round((font.CapHeightMetric - PillHeight) / 2);
    }

    /// <summary>The solid colour for a tone. <paramref name="accent"/> is the system accent colour when known.</summary>
    public static CGColor ToneColor(TrayTone tone, CGColor? accent) => tone switch
    {
        TrayTone.Accent => accent ?? Rgb(0x00, 0x7A, 0xFF),
        TrayTone.Success => Rgb(0x34, 0xC7, 0x59),
        TrayTone.Warning => Rgb(0xFF, 0x95, 0x00),
        TrayTone.Danger => Rgb(0xFF, 0x3B, 0x30),
        _ => Rgb(0x8E, 0x8E, 0x93)
    };

    static CGColor Rgb(int r, int g, int b) => new((nfloat)(r / 255d), (nfloat)(g / 255d), (nfloat)(b / 255d), 1);

    static CTLine MakeLine(string text, CTFont font, CGColor color)
    {
        using var attributed = new NSAttributedString(text ?? string.Empty, new CTStringAttributes
        {
            Font = font,
            ForegroundColor = color
        });
        return new CTLine(attributed);
    }

    /// <summary>A truncated copy when the line is too wide, otherwise null (draw the original).</summary>
    static CTLine? TruncateOrNull(CTLine line, double maxWidth, CTFont font, CGColor color)
    {
        if (line.GetTypographicBounds() <= maxWidth)
            return null;
        using var ellipsis = MakeLine("…", font, color);
        return line.GetTruncatedLine(maxWidth, CTLineTruncation.End, ellipsis);
    }

    /// <summary>Draws a line at a baseline in a flipped (top-left origin) context.</summary>
    static void DrawLine(CGContext ctx, CTLine line, double x, double baseline)
    {
        ctx.SaveState();
        ctx.TextMatrix = new CGAffineTransform(1, 0, 0, -1, 0, 0);
        ctx.TextPosition = new CGPoint(x, baseline);
        line.Draw(ctx);
        ctx.RestoreState();
    }

    static void FillRounded(CGContext ctx, CGRect rect, CGColor color)
    {
        var radius = Math.Min((double)rect.Height, (double)rect.Width) / 2;
        using var path = CGPath.FromRoundedRect(rect, (nfloat)radius, (nfloat)radius);
        ctx.SaveState();
        ctx.SetFillColor(color);
        ctx.AddPath(path);
        ctx.FillPath();
        ctx.RestoreState();
    }
}
#endif
