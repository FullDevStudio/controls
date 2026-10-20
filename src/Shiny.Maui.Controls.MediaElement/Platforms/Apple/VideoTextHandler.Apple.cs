using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace Shiny.Maui.Controls.Media;

partial class VideoTextHandler : ViewHandler<VideoTextSurface, VideoTextPlatformView>
{
    protected override VideoTextPlatformView CreatePlatformView() => new();

    static void MapText(VideoTextHandler handler, VideoTextSurface view)
    {
        var fontManager = handler.MauiContext?.Services.GetService(typeof(IFontManager)) as IFontManager;

        // the resolver lives on the native view, so it captures values, never the virtual view
        var family = view.FontFamily;
        var attributes = view.FontAttributes;

        handler.PlatformView.Update(
            view.Text,
            view.FontSize,
            size => fontManager?.GetFont(VideoTextSurface.ToFont(family, attributes, size)) ?? FallbackFont(family, attributes, size),
            view.HasOverlay ? view.OverlayColor!.ToCGColor() : null,
            view.OverlayOpacity,
            view.ActiveMarquee,
            view.MarqueeDuration);
    }

    static UIFont FallbackFont(string? fontFamily, FontAttributes attributes, double size)
    {
        var font = (fontFamily is { Length: > 0 } family ? UIFont.FromName(family, (nfloat)size) : null)
            ?? UIFont.SystemFontOfSize((nfloat)size, attributes.HasFlag(FontAttributes.Bold) ? UIFontWeight.Bold : UIFontWeight.Regular);

        if (attributes.HasFlag(FontAttributes.Italic)
            && font.FontDescriptor.CreateWithTraits(font.FontDescriptor.SymbolicTraits | UIFontDescriptorSymbolicTraits.Italic) is { } italic)
        {
            font = UIFont.FromDescriptor(italic, (nfloat)size);
        }

        return font;
    }
}


/// <summary>
/// The iOS / Mac Catalyst VideoText view: the AVPlayerLayer-backed media view, either masked to the text
/// (no overlay) or with an overlay sublayer above the video whose own mask has the text knocked out.
/// </summary>
/// <remarks>
/// <para>
/// The text is rendered once into a tight image on its own layer, and a marquee just animates that layer's
/// position, so scrolling costs Core Animation a transform per frame rather than a re-render.
/// </para>
/// <para>
/// The overlay's mask has to be opaque everywhere <i>except</i> the glyphs, and stay that way as the text
/// moves. It is the inverted glyph image plus a layer whose border is wider than the view, framing it: the
/// border travels with the text and always covers whatever part of the view the image doesn't.
/// </para>
/// </remarks>
class VideoTextPlatformView : MediaPlayerUIView, IApplePlayerOutput
{
    const string MarqueeKey = "shiny.videotext.marquee";

    readonly CALayer maskLayer = new();     // no overlay: the view's mask, holding the glyphs
    readonly CALayer overlayLayer = new();  // overlay: the coloured sheet above the video
    readonly CALayer knockoutLayer = new(); // overlay: the sheet's mask, holding the inverted glyphs
    CALayer? textLayer;

    // the managed wrappers behind the current text layer, released when the next rebuild replaces it rather
    // than left to the finalizer (a rebuild runs on every size, text or overlay change)
    readonly List<IDisposable> rendered = [];

    string? text;
    double fontSize;
    Func<double, UIFont>? fontAt;
    CGColor? overlayColor;
    double overlayOpacity = 1;
    MarqueeDirection marquee;
    TimeSpan marqueeDuration;

    CGSize renderedSize = CGSize.Empty;
    bool dirty = true;

    public VideoTextPlatformView()
    {
        this.BackgroundColor = UIColor.Clear;
        this.Layer.BackgroundColor = UIColor.Clear.CGColor;
        this.overlayLayer.Mask = this.knockoutLayer;
    }

    // a decorative mask is not a picture worth floating over other apps
    bool IApplePlayerOutput.SupportsPictureInPicture => false;

    public void Update(
        string? text,
        double fontSize,
        Func<double, UIFont> fontAt,
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
        this.SetNeedsLayout();
    }

    // AVPlayerLayer's AspectFill gravity already crops to cover; nothing to do with the size here.
    public void SetVideoSize(double width, double height) { }

    public override void MovedToWindow()
    {
        base.MovedToWindow();

        // Core Animation drops a running animation when its layer leaves the window; restart the marquee.
        if (this.Window is not null && this.textLayer?.AnimationForKey(MarqueeKey) is null && this.marquee != MarqueeDirection.None)
        {
            this.dirty = true;
            this.SetNeedsLayout();
        }
    }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        var size = this.Bounds.Size;
        if (!this.dirty && size == this.renderedSize)
            return;

        this.dirty = false;
        this.renderedSize = size;
        this.Rebuild(size);
    }

    void Rebuild(CGSize size)
    {
        CATransaction.Begin();
        CATransaction.DisableActions = true;

        this.textLayer?.RemoveFromSuperLayer();
        this.textLayer = null;
        this.ReleaseRendered();

        var hasOverlay = this.overlayColor is not null;
        this.Layer.Mask = hasOverlay ? null : this.maskLayer;
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

        var glyphs = this.RenderGlyphs(size, out var textSize);

        // nothing to knock out: the overlay is a plain sheet
        this.knockoutLayer.BackgroundColor = glyphs is null ? UIColor.Black.CGColor : null;

        if (glyphs is not null)
        {
            var imageSize = glyphs.Size;
            CALayer layer;

            if (hasOverlay)
            {
                // the border overlaps the image by a point on each side, so no hairline seam can open
                // between them at a fractional marquee position
                var border = (nfloat)(Math.Max(size.Width, size.Height) * 2);
                layer = new CALayer
                {
                    Bounds = new CGRect(0, 0, imageSize.Width + 2 * border - 2, imageSize.Height + 2 * border - 2),
                    BorderWidth = border,
                    BorderColor = UIColor.Black.CGColor
                };
                var inverted = Invert(glyphs);
                var image = new CALayer
                {
                    Frame = new CGRect(border - 1, border - 1, imageSize.Width, imageSize.Height),
                    Contents = inverted.CGImage,
                    ContentsScale = glyphs.CurrentScale
                };
                layer.AddSublayer(image);
                this.knockoutLayer.AddSublayer(layer);
                this.rendered.Add(inverted);
                this.rendered.Add(image);
            }
            else
            {
                layer = new CALayer
                {
                    Bounds = new CGRect(CGPoint.Empty, imageSize),
                    Contents = glyphs.CGImage,
                    ContentsScale = glyphs.CurrentScale
                };
                this.maskLayer.AddSublayer(layer);
            }

            this.textLayer = layer;
            this.rendered.Add(glyphs);
            this.rendered.Add(layer);
            var centre = new CGPoint(size.Width / 2, size.Height / 2);
            layer.Position = centre;

            if (this.marquee != MarqueeDirection.None)
            {
                var (fromX, fromY) = VideoTextLayout.MarqueeOffset(this.marquee, 0, size.Width, size.Height, textSize.Width, textSize.Height);
                var (toX, toY) = VideoTextLayout.MarqueeOffset(this.marquee, 1, size.Width, size.Height, textSize.Width, textSize.Height);
                var animation = CABasicAnimation.FromKeyPath("position");
                animation.From = NSValue.FromCGPoint(new CGPoint(centre.X + fromX, centre.Y + fromY));
                animation.To = NSValue.FromCGPoint(new CGPoint(centre.X + toX, centre.Y + toY));
                animation.Duration = this.marqueeDuration.TotalSeconds;
                animation.RepeatCount = float.PositiveInfinity;
                animation.RemovedOnCompletion = false;
                layer.AddAnimation(animation, MarqueeKey);
            }
        }

        CATransaction.Commit();
    }

    // The layers hold their contents natively, so dropping the managed wrappers frees nothing still in use.
    void ReleaseRendered()
    {
        foreach (var item in this.rendered)
            item.Dispose();
        this.rendered.Clear();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.fontAt = null;
            this.textLayer = null;
            this.ReleaseRendered();
        }

        base.Dispose(disposing);
    }

    // White glyphs on transparent, cropped to the text plus a margin for italic overhang and descenders.
    UIImage? RenderGlyphs(CGSize size, out CGSize textSize)
    {
        textSize = CGSize.Empty;
        var lines = VideoTextLayout.SplitLines(this.text);
        if (lines.Count == 0 || this.fontAt is null || size.Width <= 0 || size.Height <= 0)
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
        var imageSize = new CGSize(Math.Ceiling(textSize.Width) + 2 * margin, Math.Ceiling(textSize.Height) + 2 * margin);
        var text = textSize;

        return new UIGraphicsImageRenderer(imageSize).CreateImage(_ => attributed.DrawString(
            new CGRect(0, margin, imageSize.Width, text.Height)));
    }

    // Opaque everywhere but the glyphs, for the overlay's knockout mask.
    static UIImage Invert(UIImage glyphs)
        => new UIGraphicsImageRenderer(glyphs.Size).CreateImage(context =>
        {
            var rect = new CGRect(CGPoint.Empty, glyphs.Size);
            UIColor.Black.SetFill();
            context.FillRect(rect);
            glyphs.Draw(rect, CGBlendMode.DestinationOut, 1);
        });

    static CGSize Measure(NSAttributedString text) => text.GetBoundingRect(
        new CGSize(nfloat.MaxValue, nfloat.MaxValue),
        NSStringDrawingOptions.UsesLineFragmentOrigin | NSStringDrawingOptions.UsesFontLeading,
        null).Size;

    static NSAttributedString Build(string text, UIFont font)
    {
        var paragraph = new NSMutableParagraphStyle
        {
            Alignment = UITextAlignment.Center,
            // display type is sized, never wrapped
            LineBreakMode = UILineBreakMode.Clip
        };

        return new NSAttributedString(text, new UIStringAttributes
        {
            Font = font,
            ForegroundColor = UIColor.White,
            ParagraphStyle = paragraph
        });
    }
}
