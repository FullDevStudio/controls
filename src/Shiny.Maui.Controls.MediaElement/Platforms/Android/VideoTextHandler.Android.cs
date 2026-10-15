using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Views;
using Android.Widget;
using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using APaint = Android.Graphics.Paint;

namespace Shiny.Maui.Controls.Media;

/// <summary>An output that renders video into a <see cref="Android.Views.TextureView"/> rather than a <c>PlayerView</c>.</summary>
interface IVideoTextureOutput
{
    TextureView TextureView { get; }
}


partial class VideoTextHandler : ViewHandler<VideoTextSurface, VideoTextPlatformView>
{
    protected override VideoTextPlatformView CreatePlatformView() => new(this.Context);

    static void MapText(VideoTextHandler handler, VideoTextSurface view)
    {
        var fontManager = handler.MauiContext?.Services.GetService(typeof(IFontManager)) as IFontManager;
        var typeface = fontManager?.GetTypeface(view.ToFont(100)) ?? FallbackTypeface(view);
        handler.PlatformView.Update(
            view.Text,
            view.FontSize,
            typeface,
            view.HasOverlay ? view.OverlayColor!.ToPlatform() : null,
            view.OverlayOpacity,
            view.ActiveMarquee,
            view.MarqueeDuration);
    }

    static Typeface FallbackTypeface(VideoTextSurface view)
    {
        var style = (view.FontAttributes.HasFlag(FontAttributes.Bold), view.FontAttributes.HasFlag(FontAttributes.Italic)) switch
        {
            (true, true) => TypefaceStyle.BoldItalic,
            (true, false) => TypefaceStyle.Bold,
            (false, true) => TypefaceStyle.Italic,
            _ => TypefaceStyle.Normal
        };

        return Typeface.Create(view.FontFamily, style) ?? Typeface.Default!;
    }
}


/// <summary>
/// The Android VideoText view: a <see cref="TextureView"/> the player renders into, either composited
/// through the text with <c>DST_IN</c> so only the glyphs keep their video pixels, or covered by an overlay
/// layer the glyphs are cut out of with <c>DST_OUT</c>.
/// </summary>
/// <remarks>
/// <para>
/// Two layers for the mask, not one paint with a <c>DST_IN</c> transfer mode. Drawing text with
/// <c>DST_IN</c> only touches the pixels the glyphs cover, so everything <i>between</i> the letters would
/// keep its video. The text goes into its own layer instead, and restoring that layer composites its whole
/// bounds — the transparent area included, which is what clears the video around the letters.
/// </para>
/// <para>
/// TextureView rather than SurfaceView because a SurfaceView is a separate window surface punched through
/// the view hierarchy; nothing drawn over it in the hierarchy can mask it.
/// </para>
/// <para>
/// A marquee is drawn, not animated: each frame offsets the text by the elapsed time and asks for the next
/// frame. The video already redraws the view every frame, so the extra cost is the text itself.
/// </para>
/// </remarks>
class VideoTextPlatformView : FrameLayout, IVideoTextureOutput
{
    readonly TextureView textureView;
    readonly APaint textPaint = new(PaintFlags.AntiAlias) { Color = Android.Graphics.Color.White, TextAlign = APaint.Align.Center };
    readonly APaint maskPaint = new();
    readonly PorterDuffXfermode knockout = new(PorterDuff.Mode.DstOut!);
    readonly Matrix transform = new();
    IReadOnlyList<string> lines = [];
    double fontSize;
    Android.Graphics.Color? overlayColor;
    double overlayOpacity = 1;
    MarqueeDirection marquee;
    TimeSpan marqueeDuration;
    long marqueeStart;
    double videoWidth;
    double videoHeight;

    public VideoTextPlatformView(Context context) : base(context)
    {
        this.textureView = new TextureView(context);
        this.AddView(this.textureView, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
        this.maskPaint.SetXfermode(new PorterDuffXfermode(PorterDuff.Mode.DstIn!));
    }

    public TextureView TextureView => this.textureView;

    public void Update(
        string? text,
        double fontSize,
        Typeface typeface,
        Android.Graphics.Color? overlayColor,
        double overlayOpacity,
        MarqueeDirection marquee,
        TimeSpan marqueeDuration)
    {
        this.lines = VideoTextLayout.SplitLines(text);
        this.fontSize = fontSize;
        this.textPaint.SetTypeface(typeface);
        this.overlayColor = overlayColor;
        this.overlayOpacity = overlayOpacity;

        if (marquee != this.marquee || marqueeDuration != this.marqueeDuration)
            this.marqueeStart = SystemClock.UptimeMillis();
        this.marquee = marquee;
        this.marqueeDuration = marqueeDuration;

        this.Invalidate();
    }

    public void SetVideoSize(double width, double height)
    {
        this.videoWidth = width;
        this.videoHeight = height;
        this.ApplyCoverTransform();
    }

    protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
    {
        base.OnSizeChanged(w, h, oldw, oldh);
        this.ApplyCoverTransform();
    }

    // TextureView stretches the frame edge-to-edge; scale it back to the video's own aspect, overflowing
    // the short axis, which is AspectFill.
    void ApplyCoverTransform()
    {
        var (sx, sy) = VideoTextLayout.CoverScale(this.videoWidth, this.videoHeight, this.Width, this.Height);
        this.transform.Reset();
        this.transform.SetScale((float)sx, (float)sy, this.Width / 2f, this.Height / 2f);
        this.textureView.SetTransform(this.transform);
    }

    protected override void DispatchDraw(Canvas canvas)
    {
        if (this.Width <= 0 || this.Height <= 0)
            return;

        var density = this.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        var size = this.lines.Count == 0 ? 0 : this.ResolveTextSize(density);

        if (this.overlayColor is { } overlay)
        {
            base.DispatchDraw(canvas);

            var sheet = canvas.SaveLayerAlpha(0, 0, this.Width, this.Height, (int)Math.Round(this.overlayOpacity * 255));
            canvas.DrawColor(overlay);
            if (size > 0)
            {
                this.textPaint.SetXfermode(this.knockout);
                this.DrawLines(canvas, size);
                this.textPaint.SetXfermode(null);
            }
            canvas.RestoreToCount(sheet);
        }
        else
        {
            if (size <= 0)
                return; // no text = no letters to see the video through

            var videoLayer = canvas.SaveLayer(0, 0, this.Width, this.Height, null);
            base.DispatchDraw(canvas);

            var maskLayer = canvas.SaveLayer(0, 0, this.Width, this.Height, this.maskPaint);
            this.DrawLines(canvas, size);
            canvas.RestoreToCount(maskLayer);
            canvas.RestoreToCount(videoLayer);
        }

        if (this.marquee != MarqueeDirection.None && size > 0)
            this.PostInvalidateOnAnimation();
    }

    void DrawLines(Canvas canvas, float size)
    {
        this.textPaint.TextSize = size;
        var metrics = this.textPaint.GetFontMetrics()!;
        var lineHeight = metrics.Descent - metrics.Ascent;
        var blockHeight = lineHeight * this.lines.Count;

        var (dx, dy) = (0d, 0d);
        if (this.marquee != MarqueeDirection.None)
        {
            var duration = Math.Max(1, (long)this.marqueeDuration.TotalMilliseconds);
            var progress = (SystemClock.UptimeMillis() - this.marqueeStart) % duration / (double)duration;
            var blockWidth = this.lines.Max(l => this.textPaint.MeasureText(l));
            (dx, dy) = VideoTextLayout.MarqueeOffset(this.marquee, progress, this.Width, this.Height, blockWidth, blockHeight);
        }

        var x = this.Width / 2f + (float)dx;
        var top = (this.Height - blockHeight) / 2f + (float)dy;

        for (var i = 0; i < this.lines.Count; i++)
            canvas.DrawText(this.lines[i], x, top + i * lineHeight - metrics.Ascent, this.textPaint);
    }

    float ResolveTextSize(float density)
    {
        if (this.fontSize > 0)
            return (float)(this.fontSize * density);

        const float reference = 100f;
        this.textPaint.TextSize = reference;
        var metrics = this.textPaint.GetFontMetrics()!;
        var width = this.lines.Max(l => this.textPaint.MeasureText(l));
        var height = (metrics.Descent - metrics.Ascent) * this.lines.Count;

        return (float)VideoTextLayout.FitFontSize(width, height, reference, this.Width, this.Height, this.marquee);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            this.textPaint.Dispose();
            this.maskPaint.Dispose();
            this.knockout.Dispose();
            this.transform.Dispose();
        }

        base.Dispose(disposing);
    }
}
