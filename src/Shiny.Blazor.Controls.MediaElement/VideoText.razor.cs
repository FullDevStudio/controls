using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Shiny.Controls.Media;

namespace Shiny.Blazor.Controls.Media;

/// <summary>
/// Display text whose letters are windows onto a looping video. The video plays at full strength inside the
/// glyphs; around them it sits under an <see cref="OverlayColor"/> sheet (black by default) at
/// <see cref="OverlayOpacity"/>. With no overlay colour everything around the letters is transparent. The
/// Blazor counterpart of the MAUI <c>Shiny.Maui.Controls.Media.VideoText</c>.
/// </summary>
/// <remarks>
/// <para>
/// Decorative by design: no transport bar, no seek. The video autoplays and loops; sound is off unless
/// <see cref="IsMuted"/> is <c>false</c>, and even then browsers only allow it after the user has interacted
/// with the page — until then the loop plays silently rather than freezing.
/// </para>
/// <para>
/// With an overlay, the sheet is an inline SVG with the text knocked out of it, so <see cref="FontFamily"/>
/// can name any font the page has loaded, web fonts included. Without one, the text is an SVG used as a CSS
/// <c>mask-image</c> — and an SVG loaded as an image cannot see the page's web fonts, so the family must then
/// be installed on the viewer's system (or be generic, such as <c>sans-serif</c>).
/// </para>
/// </remarks>
public partial class VideoText : IAsyncDisposable
{
    [Inject] IJSRuntime JS { get; set; } = default!;

    IJSObjectReference? module;
    DotNetObjectReference<VideoText>? selfRef;
    ElementReference containerEl;
    ElementReference videoEl;
    bool disposed;

    readonly string maskId = "shiny-video-text-" + Guid.NewGuid().ToString("N")[..12];

    // the element's size, reported by a ResizeObserver; a marquee's travel is in pixels
    double width;
    double height;

    // last values pushed to JS / measured, so a re-render only crosses interop on a real change
    bool appliedMuted;
    string? measuredKey;
    double[]? lineWidths;


    // ── parameters ───────────────────────────────────────────────────────────────────────────────

    /// <summary>URL of the video playing inside the letters — absolute, or relative to the app's base path.</summary>
    [Parameter] public string? Source { get; set; }

    /// <summary>The text cut out of the video. A <c>\n</c> starts a new line; nothing else wraps it.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>
    /// A CSS font family. Default <c>sans-serif</c>. Web fonts work with an overlay; without one the mask
    /// can only see fonts installed on the viewer's system.
    /// </summary>
    [Parameter] public string? FontFamily { get; set; } = "sans-serif";

    /// <summary>The font size in CSS pixels. <c>0</c> (the default) auto-fits the text to 90% of the control.</summary>
    [Parameter] public double FontSize { get; set; }

    /// <summary>A CSS <c>font-weight</c>. Default <c>bold</c> — thin strokes show very little video.</summary>
    [Parameter] public string? FontWeight { get; set; } = "bold";

    /// <summary>A CSS <c>font-style</c>, e.g. <c>italic</c>.</summary>
    [Parameter] public string? FontStyle { get; set; }

    /// <summary>Silence the video's audio. Default <c>true</c>.</summary>
    [Parameter] public bool IsMuted { get; set; } = true;

    /// <summary>
    /// The colour of the sheet over the video around the letters — any CSS colour. Default <c>black</c>.
    /// <c>null</c>, <c>transparent</c> or <c>none</c> drops the sheet and clips the video to the letters,
    /// leaving the rest of the control transparent.
    /// </summary>
    [Parameter] public string? OverlayColor { get; set; } = "black";

    /// <summary>The overlay sheet's opacity, 0–1. Default 1 (solid). Lower it to let the video show faintly around the letters.</summary>
    [Parameter] public double OverlayOpacity { get; set; } = 1;

    /// <summary>
    /// Scroll the text continuously across the control. Default <see cref="MarqueeDirection.None"/>. Auto-fit
    /// then sizes the text to the cross axis only (90% of the height for left/right, of the width for up/down).
    /// </summary>
    [Parameter] public MarqueeDirection Marquee { get; set; }

    /// <summary>How long one marquee pass takes, from entering one edge to leaving the other. Default 8 seconds.</summary>
    [Parameter] public TimeSpan MarqueeDuration { get; set; } = TimeSpan.FromSeconds(8);

    /// <summary>The <c>preload</c> hint for the video. Default <c>auto</c>, since it plays immediately anyway.</summary>
    [Parameter] public string Preload { get; set; } = "auto";

    /// <summary>Extra CSS classes for the container.</summary>
    [Parameter] public string? CssClass { get; set; }

    /// <summary>Inline styles for the container — typically its height (default 200px) or an aspect ratio.</summary>
    [Parameter] public string? Style { get; set; }


    // ── rendering ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Whether to draw the overlay sheet rather than clip the video to the letters.</summary>
    internal bool HasOverlay => !String.IsNullOrWhiteSpace(this.OverlayColor)
        && !this.OverlayColor.Trim().Equals("transparent", StringComparison.OrdinalIgnoreCase)
        && !this.OverlayColor.Trim().Equals("none", StringComparison.OrdinalIgnoreCase);

    MarqueeDirection ActiveMarquee => this.MarqueeDuration > TimeSpan.Zero ? this.Marquee : MarqueeDirection.None;

    IReadOnlyList<double>? MeasuredWidths => this.measuredKey == this.MeasureKey ? this.lineWidths : null;

    /// <summary>The mask SVG used when there is no overlay, before URL-encoding.</summary>
    internal string MaskSvg => VideoTextLayout.BuildSvgMask(
        VideoTextLayout.SplitLines(this.Text),
        this.FontSize,
        this.FontFamily,
        this.FontWeight,
        this.FontStyle,
        this.MeasuredWidths,
        this.ActiveMarquee,
        this.MarqueeDuration.TotalSeconds,
        this.width,
        this.height);

    /// <summary>The inline overlay SVG used when there is an overlay.</summary>
    internal string OverlaySvg => VideoTextLayout.BuildSvgOverlay(
        VideoTextLayout.SplitLines(this.Text),
        this.FontSize,
        this.FontFamily,
        this.FontWeight,
        this.FontStyle,
        this.OverlayColor!,
        this.OverlayOpacity,
        this.maskId,
        this.MeasuredWidths,
        this.ActiveMarquee,
        this.MarqueeDuration.TotalSeconds,
        this.width,
        this.height);

    // The caller's Style is appended to, never replaces, the mask — a splatted style attribute would
    // overwrite the component's own and silently drop it.
    string? ContainerStyle
    {
        get
        {
            if (this.HasOverlay)
                return this.Style;

            var url = VideoTextLayout.ToDataUrl(this.MaskSvg);
            return $"-webkit-mask-image:url(\"{url}\");mask-image:url(\"{url}\");{this.Style}";
        }
    }

    string MeasureKey => $"{this.Text}\u0001{this.FontFamily}\u0001{this.FontWeight}\u0001{this.FontStyle}";

    /// <summary>Called by the module's ResizeObserver.</summary>
    [JSInvokable]
    public Task OnResize(double width, double height)
    {
        if (Math.Abs(width - this.width) < 0.5 && Math.Abs(height - this.height) < 0.5)
            return Task.CompletedTask;

        this.width = width;
        this.height = height;

        // only a marquee lays out in pixels; static text scales itself
        return this.ActiveMarquee == MarqueeDirection.None || this.disposed
            ? Task.CompletedTask
            : this.InvokeAsync(this.StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var imported = await this.JS
                .InvokeAsync<IJSObjectReference>("import", "./_content/Shiny.Blazor.Controls.MediaElement/video-text.js")
                .ConfigureAwait(true);

            if (this.disposed)
            {
                await imported.DisposeAsync().ConfigureAwait(true);
                return;
            }

            this.module = imported;
            this.appliedMuted = this.IsMuted;
            this.selfRef = DotNetObjectReference.Create(this);
            await this.module.InvokeVoidAsync("start", this.videoEl, this.containerEl, this.selfRef, this.IsMuted).ConfigureAwait(true);
        }

        if (this.module is null)
            return;

        if (this.appliedMuted != this.IsMuted)
        {
            this.appliedMuted = this.IsMuted;
            await this.module.InvokeVoidAsync("setMuted", this.videoEl, this.IsMuted).ConfigureAwait(true);
        }

        // Auto-fit and a marquee's travel need real glyph widths; a still, fixed size lays out without them.
        var key = this.MeasureKey;
        if ((this.FontSize <= 0 || this.ActiveMarquee != MarqueeDirection.None) && this.measuredKey != key)
        {
            var lines = VideoTextLayout.SplitLines(this.Text).ToArray();
            this.lineWidths = lines.Length == 0
                ? []
                : await this.module
                    .InvokeAsync<double[]>("measure", lines, this.FontFamily, this.FontWeight, this.FontStyle)
                    .ConfigureAwait(true);
            this.measuredKey = key;

            if (!this.disposed)
                this.StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        this.disposed = true;

        if (this.module is not null)
        {
            try
            {
                await this.module.InvokeVoidAsync("stop", this.containerEl).ConfigureAwait(true);
                await this.module.DisposeAsync().ConfigureAwait(true);
            }
            catch (JSDisconnectedException)
            {
                // circuit already gone (Blazor Server)
            }
        }

        this.selfRef?.Dispose();
        GC.SuppressFinalize(this);
    }
}
