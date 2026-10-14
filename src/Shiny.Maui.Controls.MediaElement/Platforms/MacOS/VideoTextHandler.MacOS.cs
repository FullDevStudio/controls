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
        => handler.PlatformView.SetText(view.Text, view.FontSize, size => ResolveFont(view, size));

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
/// The macOS AppKit VideoText view: the AVPlayerLayer-backed media view with a layer mask rendered from the
/// text, the same arrangement as the UIKit one.
/// </summary>
class VideoTextPlatformView : MediaPlayerNSView
{
    readonly CALayer maskLayer = new();
    string? text;
    double fontSize;
    Func<double, NSFont>? fontAt;
    CGSize renderedSize = CGSize.Empty;
    bool dirty = true;

    public VideoTextPlatformView()
    {
        this.Layer!.BackgroundColor = NSColor.Clear.CGColor;
        this.Layer.Mask = this.maskLayer;
    }

    public void SetText(string? text, double fontSize, Func<double, NSFont> fontAt)
    {
        this.text = text;
        this.fontSize = fontSize;
        this.fontAt = fontAt;
        this.dirty = true;
        this.UpdateMask();
    }

    // AVPlayerLayer's AspectFill gravity already crops to cover.
    public void SetVideoSize(double width, double height) { }

    public override void SetFrameSize(CGSize newSize)
    {
        base.SetFrameSize(newSize);
        this.UpdateMask();
    }

    public override void ViewDidMoveToWindow()
    {
        base.ViewDidMoveToWindow();
        // the backing scale is only known once there is a window (and differs per display)
        this.dirty = true;
        this.UpdateMask();
    }

    void UpdateMask()
    {
        var size = this.Bounds.Size;
        if (!this.dirty && size == this.renderedSize)
            return;

        this.dirty = false;
        this.renderedSize = size;

        var scale = this.Window?.BackingScaleFactor ?? NSScreen.MainScreen?.BackingScaleFactor ?? 2;

        CATransaction.Begin();
        CATransaction.DisableActions = true;
        this.maskLayer.Frame = this.Bounds;
        this.maskLayer.ContentsScale = scale;
        this.maskLayer.Contents = this.RenderMask(size, scale);
        CATransaction.Commit();
    }

    CGImage? RenderMask(CGSize size, nfloat scale)
    {
        var lines = VideoTextLayout.SplitLines(this.text);
        if (lines.Count == 0 || this.fontAt is null || size.Width < 1 || size.Height < 1)
            return null;

        var joined = String.Join("\n", lines);
        const double reference = 100;
        var measured = Build(joined, this.fontAt(reference)).BoundingRectWithSize(
            new CGSize(nfloat.MaxValue, nfloat.MaxValue),
            NSStringDrawingOptions.UsesLineFragmentOrigin | NSStringDrawingOptions.UsesFontLeading);

        var resolved = VideoTextLayout.ResolveFontSize(
            this.fontSize, measured.Width, measured.Height, reference, size.Width, size.Height);
        if (resolved <= 0)
            return null;

        var attributed = Build(joined, this.fontAt(resolved));
        var textHeight = attributed.BoundingRectWithSize(
            new CGSize(nfloat.MaxValue, nfloat.MaxValue),
            NSStringDrawingOptions.UsesLineFragmentOrigin | NSStringDrawingOptions.UsesFontLeading).Height;

        using var colorSpace = CGColorSpace.CreateDeviceRGB();
        using var context = new CGBitmapContext(
            null,
            (nint)Math.Ceiling(size.Width * scale),
            (nint)Math.Ceiling(size.Height * scale),
            8,
            0,
            colorSpace,
            CGImageAlphaInfo.PremultipliedLast);
        context.ScaleCTM(scale, scale);

        NSGraphicsContext.GlobalSaveGraphicsState();
        try
        {
            NSGraphicsContext.CurrentContext = NSGraphicsContext.FromCGContext(context, false);
            // unflipped: y runs up from the bottom, which a vertically centred rect doesn't care about
            attributed.DrawInRect(new CGRect(0, (size.Height - textHeight) / 2, size.Width, textHeight));
        }
        finally
        {
            NSGraphicsContext.GlobalRestoreGraphicsState();
        }

        return context.ToImage();
    }

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
