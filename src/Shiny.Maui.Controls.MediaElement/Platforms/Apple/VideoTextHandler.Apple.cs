using CoreAnimation;
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using UIKit;

namespace Shiny.Maui.Controls.Media;

partial class VideoTextHandler : ViewHandler<VideoTextSurface, VideoTextPlatformView>
{
    protected override VideoTextPlatformView CreatePlatformView() => new();

    static void MapText(VideoTextHandler handler, VideoTextSurface view)
    {
        var fontManager = handler.MauiContext?.Services.GetService(typeof(IFontManager)) as IFontManager;

        handler.PlatformView.SetText(
            view.Text,
            view.FontSize,
            size => fontManager?.GetFont(view.ToFont(size)) ?? FallbackFont(view, size));
    }

    static UIFont FallbackFont(VideoTextSurface view, double size)
    {
        var font = (view.FontFamily is { Length: > 0 } family ? UIFont.FromName(family, (nfloat)size) : null)
            ?? UIFont.SystemFontOfSize((nfloat)size, view.FontAttributes.HasFlag(FontAttributes.Bold) ? UIFontWeight.Bold : UIFontWeight.Regular);

        if (view.FontAttributes.HasFlag(FontAttributes.Italic)
            && font.FontDescriptor.CreateWithTraits(font.FontDescriptor.SymbolicTraits | UIFontDescriptorSymbolicTraits.Italic) is { } italic)
        {
            font = UIFont.FromDescriptor(italic, (nfloat)size);
        }

        return font;
    }
}


/// <summary>
/// The iOS / Mac Catalyst VideoText view: the AVPlayerLayer-backed media view with a layer mask rendered
/// from the text. The mask's alpha is the glyphs', so only the letters show video and the rest of the
/// view is fully transparent.
/// </summary>
class VideoTextPlatformView : MediaPlayerUIView, IApplePlayerOutput
{
    readonly CALayer maskLayer = new();
    string? text;
    double fontSize;
    Func<double, UIFont>? fontAt;
    CGSize renderedSize = CGSize.Empty;
    bool dirty = true;

    public VideoTextPlatformView()
    {
        this.BackgroundColor = UIColor.Clear;
        this.Layer.BackgroundColor = UIColor.Clear.CGColor;
        this.Layer.Mask = this.maskLayer;
    }

    // a decorative mask is not a picture worth floating over other apps
    bool IApplePlayerOutput.SupportsPictureInPicture => false;

    public void SetText(string? text, double fontSize, Func<double, UIFont> fontAt)
    {
        this.text = text;
        this.fontSize = fontSize;
        this.fontAt = fontAt;
        this.dirty = true;
        this.SetNeedsLayout();
    }

    // AVPlayerLayer's AspectFill gravity already crops to cover; nothing to do with the size here.
    public void SetVideoSize(double width, double height) { }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();

        var size = this.Bounds.Size;
        if (!this.dirty && size == this.renderedSize)
            return;

        this.dirty = false;
        this.renderedSize = size;

        CATransaction.Begin();
        CATransaction.DisableActions = true;
        this.maskLayer.Frame = this.Bounds;
        this.maskLayer.Contents = this.RenderMask(size);
        this.maskLayer.ContentsScale = this.Window?.Screen.Scale ?? UIScreen.MainScreen.Scale;
        CATransaction.Commit();
    }

    CGImage? RenderMask(CGSize size)
    {
        var lines = VideoTextLayout.SplitLines(this.text);
        if (lines.Count == 0 || this.fontAt is null || size.Width <= 0 || size.Height <= 0)
            return null;

        var joined = String.Join("\n", lines);
        const double reference = 100;
        var measured = Build(joined, this.fontAt(reference)).GetBoundingRect(
            new CGSize(nfloat.MaxValue, nfloat.MaxValue),
            NSStringDrawingOptions.UsesLineFragmentOrigin | NSStringDrawingOptions.UsesFontLeading,
            null);

        var resolved = VideoTextLayout.ResolveFontSize(
            this.fontSize, measured.Width, measured.Height, reference, size.Width, size.Height);
        if (resolved <= 0)
            return null;

        var attributed = Build(joined, this.fontAt(resolved));
        var textHeight = attributed.GetBoundingRect(
            new CGSize(nfloat.MaxValue, nfloat.MaxValue),
            NSStringDrawingOptions.UsesLineFragmentOrigin | NSStringDrawingOptions.UsesFontLeading,
            null).Height;

        var renderer = new UIGraphicsImageRenderer(size);
        var image = renderer.CreateImage(_ => attributed.DrawString(
            new CGRect(0, (size.Height - textHeight) / 2, size.Width, textHeight)));

        return image.CGImage;
    }

    static NSAttributedString Build(string text, UIFont font)
    {
        var paragraph = new NSMutableParagraphStyle
        {
            Alignment = UITextAlignment.Center,
            // display type is sized, never wrapped: a fixed size that is too wide clips at the edges
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
