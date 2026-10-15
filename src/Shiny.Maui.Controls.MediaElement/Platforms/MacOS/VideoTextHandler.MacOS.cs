using AppKit;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;

namespace Shiny.Maui.Controls.Media;

partial class VideoTextHandler : ViewHandler<VideoTextSurface, VideoTextPlatformView>
{
    protected override VideoTextPlatformView CreatePlatformView() => new();

    // The AppKit head has no IFontManager to lean on, so fonts resolve straight through NSFont.
    static void MapText(VideoTextHandler handler, VideoTextSurface view)
        => handler.PlatformView.Update(
            view.Text,
            view.FontSize,
            size => ResolveFont(view, size),
            view.HasOverlay ? ToCGColor(view.OverlayColor!) : null,
            view.OverlayOpacity,
            view.ActiveMarquee,
            view.MarqueeDuration);

    static CGColor ToCGColor(Color color)
        => new((nfloat)color.Red, (nfloat)color.Green, (nfloat)color.Blue, (nfloat)color.Alpha);

    static NSFont ResolveFont(VideoTextSurface view, double size)
    {
        var font = (view.FontFamily is { Length: > 0 } family ? NSFont.FromFontName(family, (nfloat)size) : null)
            ?? NSFont.SystemFontOfSize((nfloat)size, view.FontAttributes.HasFlag(FontAttributes.Bold) ? NSFontWeight.Bold : NSFontWeight.Regular)!;

        var traits = (NSFontTraitMask)0;
        if (view.FontAttributes.HasFlag(FontAttributes.Bold))
            traits |= NSFontTraitMask.Bold;
        if (view.FontAttributes.HasFlag(FontAttributes.Italic))
            traits |= NSFontTraitMask.Italic;

        return traits == 0 ? font : NSFontManager.SharedFontManager.ConvertFont(font, traits) ?? font;
    }
}


/// <summary>
/// The macOS AppKit VideoText view: the AVPlayerLayer-backed media view, masked to the text or covered by a
/// knockout overlay — the same arrangement as the UIKit one (see its remarks).
/// </summary>
/// <remarks>
/// AppKit layers are y-up (the view isn't flipped), so vertical marquee offsets are negated, and the glyph
/// bitmaps are drawn in an unflipped context so they come out upright.
/// </remarks>
class VideoTextPlatformView : MediaPlayerNSView
{
    const string MarqueeKey = "shiny.videotext.marquee";

    readonly CALayer maskLayer = new();
    readonly CALayer overlayLayer = new();
    readonly CALayer knockoutLayer = new();
    CALayer? textLayer;

    string? text;
    double fontSize;
    Func<double, NSFont>? fontAt;
    CGColor? overlayColor;
    double overlayOpacity = 1;
    MarqueeDirection marquee;
    TimeSpan marqueeDuration;

    CGSize renderedSize = CGSize.Empty;
    bool dirty = true;

    public VideoTextPlatformView()
    {
        this.Layer!.BackgroundColor = NSColor.Clear.CGColor;
        this.overlayLayer.Mask = this.knockoutLayer;
    }

    public void Update(
        string? text,
        double fontSize,
        Func<double, NSFont> fontAt,
        CGColor? overlayColor,
        double overlayOpacity,
        MarqueeDirection marquee,
        TimeSpan marqueeDuration)
    {
        this.text = text;
        this.fontSize = fontSize;
        this.fontAt = fontAt;
        this.overlayColor = overlayColor;
        this.overlayOpacity = overlayOpacity;
        this.marquee = marquee;
        this.marqueeDuration = marqueeDuration;
        this.dirty = true;
        this.UpdateLayers();
    }

    // AVPlayerLayer's AspectFill gravity already crops to cover.
    public void SetVideoSize(double width, double height) { }

    public override void SetFrameSize(CGSize newSize)
    {
        base.SetFrameSize(newSize);
        this.UpdateLayers();
    }

    public override void ViewDidMoveToWindow()
    {
        base.ViewDidMoveToWindow();
        // the backing scale is only known once there is a window (and differs per display), and a layer
        // that left the window lost its marquee animation
        this.dirty = true;
        this.UpdateLayers();
    }

    void UpdateLayers()
    {
        var size = this.Bounds.Size;
        if (!this.dirty && size == this.renderedSize)
            return;

        this.dirty = false;
        this.renderedSize = size;

        var scale = this.Window?.BackingScaleFactor ?? NSScreen.MainScreen?.BackingScaleFactor ?? 2;

        CATransaction.Begin();
        CATransaction.DisableActions = true;

        this.textLayer?.RemoveFromSuperLayer();
        this.textLayer = null;

        var hasOverlay = this.overlayColor is not null;
        this.Layer!.Mask = hasOverlay ? null : this.maskLayer;
        if (hasOverlay && this.overlayLayer.SuperLayer is null)
            this.Layer.AddSublayer(this.overlayLayer);
        else if (!hasOverlay)
            this.overlayLayer.RemoveFromSuperLayer();

        var bounds = new CGRect(CGPoint.Empty, size);
        this.maskLayer.Frame = bounds;
        this.overlayLayer.Frame = bounds;
        this.overlayLayer.BackgroundColor = this.overlayColor;
        this.overlayLayer.Opacity = (float)this.overlayOpacity;
        this.knockoutLayer.Frame = bounds;

        var glyphs = this.RenderGlyphs(size, scale, out var textSize, out var imageSize);
        this.knockoutLayer.BackgroundColor = glyphs is null ? NSColor.Black.CGColor : null;

        if (glyphs is not null)
        {
            CALayer layer;

            if (hasOverlay)
            {
                var border = (nfloat)(Math.Max(size.Width, size.Height) * 2);
                layer = new CALayer
                {
                    Bounds = new CGRect(0, 0, imageSize.Width + 2 * border - 2, imageSize.Height + 2 * border - 2),
                    BorderWidth = border,
                    BorderColor = NSColor.Black.CGColor
                };
                layer.AddSublayer(new CALayer
                {
                    Frame = new CGRect(border - 1, border - 1, imageSize.Width, imageSize.Height),
                    Contents = Invert(glyphs),
                    ContentsScale = scale
                });
                this.knockoutLayer.AddSublayer(layer);
            }
            else
            {
                layer = new CALayer
                {
                    Bounds = new CGRect(CGPoint.Empty, imageSize),
                    Contents = glyphs,
                    ContentsScale = scale
                };
                this.maskLayer.AddSublayer(layer);
            }

            this.textLayer = layer;
            var centre = new CGPoint(size.Width / 2, size.Height / 2);
            layer.Position = centre;

            if (this.marquee != MarqueeDirection.None)
            {
                var (fromX, fromY) = VideoTextLayout.MarqueeOffset(this.marquee, 0, size.Width, size.Height, textSize.Width, textSize.Height);
                var (toX, toY) = VideoTextLayout.MarqueeOffset(this.marquee, 1, size.Width, size.Height, textSize.Width, textSize.Height);
                var animation = CABasicAnimation.FromKeyPath("position");
                animation.From = NSValue.FromCGPoint(new CGPoint(centre.X + fromX, centre.Y - fromY));
                animation.To = NSValue.FromCGPoint(new CGPoint(centre.X + toX, centre.Y - toY));
                animation.Duration = this.marqueeDuration.TotalSeconds;
                animation.RepeatCount = float.PositiveInfinity;
                animation.RemovedOnCompletion = false;
                layer.AddAnimation(animation, MarqueeKey);
            }
        }

        CATransaction.Commit();
    }

    CGImage? RenderGlyphs(CGSize size, nfloat scale, out CGSize textSize, out CGSize imageSize)
    {
        textSize = CGSize.Empty;
        imageSize = CGSize.Empty;
        var lines = VideoTextLayout.SplitLines(this.text);
        if (lines.Count == 0 || this.fontAt is null || size.Width < 1 || size.Height < 1)
            return null;

        var joined = String.Join("\n", lines);
        const double reference = 100;
        var measured = Measure(Build(joined, this.fontAt(reference)));

        var resolved = VideoTextLayout.ResolveFontSize(
            this.fontSize, measured.Width, measured.Height, reference, size.Width, size.Height, this.marquee);
        if (resolved <= 0)
            return null;

        var attributed = Build(joined, this.fontAt(resolved));
        textSize = Measure(attributed);

        var margin = (nfloat)Math.Ceiling(resolved * 0.25);
        imageSize = new CGSize(Math.Ceiling(textSize.Width) + 2 * margin, Math.Ceiling(textSize.Height) + 2 * margin);

        using var context = CreateContext(imageSize, scale);
        NSGraphicsContext.GlobalSaveGraphicsState();
        try
        {
            NSGraphicsContext.CurrentContext = NSGraphicsContext.FromCGContext(context, false);
            attributed.DrawInRect(new CGRect(0, margin, imageSize.Width, textSize.Height));
        }
        finally
        {
            NSGraphicsContext.GlobalRestoreGraphicsState();
        }

        return context.ToImage();
    }

    // Opaque everywhere but the glyphs, for the overlay's knockout mask.
    static CGImage? Invert(CGImage glyphs)
    {
        using var colorSpace = CGColorSpace.CreateDeviceRGB();
        using var context = new CGBitmapContext(null, glyphs.Width, glyphs.Height, 8, 0, colorSpace, CGImageAlphaInfo.PremultipliedLast);
        var rect = new CGRect(0, 0, glyphs.Width, glyphs.Height);
        context.SetFillColor(new CGColor(0, 0, 0, 1));
        context.FillRect(rect);
        context.SetBlendMode(CGBlendMode.DestinationOut);
        context.DrawImage(rect, glyphs);
        return context.ToImage();
    }

    static CGBitmapContext CreateContext(CGSize size, nfloat scale)
    {
        using var colorSpace = CGColorSpace.CreateDeviceRGB();
        var context = new CGBitmapContext(
            null,
            (nint)Math.Ceiling(size.Width * scale),
            (nint)Math.Ceiling(size.Height * scale),
            8,
            0,
            colorSpace,
            CGImageAlphaInfo.PremultipliedLast);
        context.ScaleCTM(scale, scale);
        return context;
    }

    static CGSize Measure(NSAttributedString text) => text.BoundingRectWithSize(
        new CGSize(nfloat.MaxValue, nfloat.MaxValue),
        NSStringDrawingOptions.UsesLineFragmentOrigin | NSStringDrawingOptions.UsesFontLeading).Size;

    static NSAttributedString Build(string text, NSFont font)
    {
        var paragraph = new NSMutableParagraphStyle
        {
            Alignment = NSTextAlignment.Center,
            LineBreakMode = NSLineBreakMode.Clipping
        };

        return new NSAttributedString(text, new NSStringAttributes
        {
            Font = font,
            ForegroundColor = NSColor.White,
            ParagraphStyle = paragraph
        });
    }
}
