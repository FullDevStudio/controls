using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Shiny.Controls.Media;

namespace Shiny.Blazor.Controls.Media;

/// <summary>
/// Display text whose letters are windows onto a looping video — the video plays only inside the glyphs and
/// everything around them is transparent. The Blazor counterpart of the MAUI
/// <c>Shiny.Maui.Controls.Media.VideoText</c>.
/// </summary>
/// <remarks>
/// <para>
/// Decorative by design: no transport bar, no seek. The video autoplays and loops; sound is off unless
/// <see cref="IsMuted"/> is <c>false</c>, and even then browsers only allow it after the user has interacted
/// with the page — until then the loop plays silently rather than freezing.
/// </para>
/// <para>
/// The text is an SVG used as a CSS <c>mask-image</c>. An SVG loaded as an image cannot see the page's web
/// fonts, so <see cref="FontFamily"/> must name a font installed on the viewer's system (or a generic
/// family such as <c>sans-serif</c>); a <c>@font-face</c> font falls back silently.
/// </para>
/// </remarks>
public partial class VideoText : IAsyncDisposable
{
    [Inject] IJSRuntime JS { get; set; } = default!;

    IJSObjectReference? module;
    ElementReference videoEl;
    bool disposed;

    // last values pushed to JS / measured, so a re-render only crosses interop on a real change
    bool appliedMuted;
    string? measuredKey;
    double[]? lineWidths;


    // ── parameters ───────────────────────────────────────────────────────────────────────────────

    /// <summary>URL of the video playing inside the letters — absolute, or relative to the app's base path.</summary>
    [Parameter] public string? Source { get; set; }

    /// <summary>The text cut out of the video. A <c>\n</c> starts a new line; nothing else wraps it.</summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>A system font family or generic family. Default <c>sans-serif</c>. Web fonts are not visible to the mask.</summary>
    [Parameter] public string? FontFamily { get; set; } = "sans-serif";

    /// <summary>The font size in CSS pixels. <c>0</c> (the default) auto-fits the text to 90% of the control.</summary>
    [Parameter] public double FontSize { get; set; }

    /// <summary>A CSS <c>font-weight</c>. Default <c>bold</c> — thin strokes show very little video.</summary>
    [Parameter] public string? FontWeight { get; set; } = "bold";

    /// <summary>A CSS <c>font-style</c>, e.g. <c>italic</c>.</summary>
    [Parameter] public string? FontStyle { get; set; }

    /// <summary>Silence the video's audio. Default <c>true</c>.</summary>
    [Parameter] public bool IsMuted { get; set; } = true;

    /// <summary>The <c>preload</c> hint for the video. Default <c>auto</c>, since it plays immediately anyway.</summary>
    [Parameter] public string Preload { get; set; } = "auto";

    /// <summary>Extra CSS classes for the container.</summary>
    [Parameter] public string? CssClass { get; set; }

    /// <summary>Inline styles for the container — typically its height (default 200px) or an aspect ratio.</summary>
    [Parameter] public string? Style { get; set; }


    // ── rendering ────────────────────────────────────────────────────────────────────────────────

    /// <summary>The mask SVG for the current parameters (and the last measurement), before URL-encoding.</summary>
    internal string MaskSvg => VideoTextLayout.BuildSvgMask(
        VideoTextLayout.SplitLines(this.Text),
        this.FontSize,
        this.FontFamily,
        this.FontWeight,
        this.FontStyle,
        this.measuredKey == this.MeasureKey ? this.lineWidths : null);

    // The caller's Style is appended to, never replaces, the mask — a splatted style attribute would
    // overwrite the component's own and silently drop it.
    string ContainerStyle
    {
        get
        {
            var url = VideoTextLayout.ToDataUrl(this.MaskSvg);
            return $"-webkit-mask-image:url(\"{url}\");mask-image:url(\"{url}\");{this.Style}";
        }
    }

    string MeasureKey => $"{this.Text}\u0001{this.FontFamily}\u0001{this.FontWeight}\u0001{this.FontStyle}";

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
            await this.module.InvokeVoidAsync("start", this.videoEl, this.IsMuted).ConfigureAwait(true);
        }

        if (this.module is null)
            return;

        if (this.appliedMuted != this.IsMuted)
        {
            this.appliedMuted = this.IsMuted;
            await this.module.InvokeVoidAsync("setMuted", this.videoEl, this.IsMuted).ConfigureAwait(true);
        }

        // Only auto-fit needs real glyph widths; a fixed size lays out without them.
        var key = this.MeasureKey;
        if (this.FontSize <= 0 && this.measuredKey != key)
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
                await this.module.DisposeAsync().ConfigureAwait(true);
            }
            catch (JSDisconnectedException)
            {
                // circuit already gone (Blazor Server)
            }
        }

        GC.SuppressFinalize(this);
    }
}
