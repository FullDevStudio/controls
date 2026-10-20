using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Shiny.Blazor.Controls;

/// <summary>
/// A Dynamic Island / camera-notch style surface that holds any content and morphs its width, height
/// and corner radius to fit it.
/// </summary>
/// <remarks>
/// There are no compact/expanded states: put anything in <see cref="ChildContent"/> and the notch sizes
/// itself to it, then follows it — a <c>ResizeObserver</c> watches the content, so text that grows, a
/// row that appears or a swapped fragment all spring the notch to the new size. The content is laid out
/// at its own size and the surface clips it while it animates, so nothing re-wraps mid-morph.
/// <para>
/// By default it overlays the very top of the screen: it is promoted to the browser's top layer (a manual
/// popover), so no parent's <c>overflow</c>, <c>transform</c> or <c>z-index</c> can trap it, and it hangs
/// from the top edge of the viewport above everything else on the page. Set <see cref="Inline"/> to lay it
/// out in the page flow instead.
/// </para>
/// </remarks>
public partial class NotchView
{
    [Inject] IJSRuntime JS { get; set; } = default!;

    /// <summary>Anything you like. The notch sizes itself to it.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Change this when you swap to different content (a "state" name works) and the new content fades
    /// in as the notch reshapes. Content that only changes size needs no key.
    /// </summary>
    [Parameter] public object? ContentKey { get; set; }

    /// <summary>
    /// <see cref="NotchShape.Notch"/> (default: square top corners, flush with the top of the screen) or
    /// <see cref="NotchShape.Island"/> (every corner rounded, floating just below the top edge).
    /// </summary>
    [Parameter] public NotchShape Shape { get; set; } = NotchShape.Notch;

    /// <summary>
    /// When false (default) the notch overlays the top-centre of the screen, above everything on the page.
    /// When true it is laid out in the page flow like any other element (centred in its parent) — for a
    /// notch inside a card, a mock device frame or a docs page.
    /// </summary>
    [Parameter] public bool Inline { get; set; }

    /// <summary>
    /// The fill (any CSS colour). When null the stylesheet paints black on every theme — the look being
    /// imitated is a hole in the screen — which <c>--shiny-notch-surface</c> overrides. Give the content light text.
    /// </summary>
    [Parameter] public string? SurfaceColor { get; set; }

    /// <summary>Optional outline colour (any CSS colour). None by default.</summary>
    [Parameter] public string? Stroke { get; set; }

    /// <summary>Outline thickness in px. Only drawn when <see cref="Stroke"/> is set.</summary>
    [Parameter] public double StrokeThickness { get; set; } = 1;

    /// <summary>Space between the edge of the notch and its content, as CSS padding.</summary>
    [Parameter] public string ContentPadding { get; set; } = "8px 14px";

    /// <summary>The narrowest the notch gets, in px — and its width when there is no content.</summary>
    [Parameter] public double MinNotchWidth { get; set; } = 120;

    /// <summary>The shortest the notch gets, in px — and its height when there is no content.</summary>
    [Parameter] public double MinNotchHeight { get; set; } = 36;

    /// <summary>
    /// The widest the notch gets, in px. Unbounded by default, in which case the parent's width is the
    /// limit and wider content wraps.
    /// </summary>
    [Parameter] public double? MaxNotchWidth { get; set; }

    /// <summary>The tallest the notch gets, in px. Taller content is clipped.</summary>
    [Parameter] public double? MaxNotchHeight { get; set; }

    /// <summary>
    /// Corner radius in px. The default, <c>-1</c>, is automatic: a full pill while short, capped at 28
    /// as it grows tall. Any value is clamped to half the notch's height.
    /// </summary>
    [Parameter] public double CornerRadius { get; set; } = -1;

    /// <summary>How long a resize takes, in milliseconds. <c>0</c> snaps.</summary>
    [Parameter] public int MorphDuration { get; set; } = 420;

    /// <summary>Draws a soft drop shadow under the notch.</summary>
    [Parameter] public bool HasShadow { get; set; } = true;

    /// <summary>
    /// Raised when the notch is clicked. Clicks on content inside it bubble up to here as well, so give an
    /// inner button <c>@onclick:stopPropagation</c> when it should not also count as a click on the notch.
    /// </summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    /// <summary>Raised as a morph starts, with the size it is heading to.</summary>
    [Parameter] public EventCallback<NotchSize> OnNotchSizeChanged { get; set; }

    [Parameter] public string? CssClass { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>The size the notch is at, or is morphing towards.</summary>
    public NotchSize Size { get; private set; }

    ElementReference root;
    IJSObjectReference? module;
    DotNetObjectReference<NotchView>? selfRef;
    bool disposed;
    bool initialized;
    bool fadeIn;
    object? lastKey;
    bool hasRendered;
    NotchJsOptions? lastOptions;

    string ShapeClass => Shape == NotchShape.Notch ? "notch" : "island";

    // A caller's style attribute would otherwise replace this one outright and drop the custom
    // properties below, so it is merged in last instead.
    IDictionary<string, object>? PassThroughAttributes
        => AdditionalAttributes is { } attrs && attrs.ContainsKey("style")
            ? attrs.Where(kv => kv.Key != "style").ToDictionary(kv => kv.Key, kv => kv.Value)
            : AdditionalAttributes;

    string RootStyle
    {
        get
        {
            var ci = CultureInfo.InvariantCulture;
            var parts = new List<string>
            {
                $"--shiny-notch-padding:{ContentPadding}",
                $"--shiny-notch-duration:{Math.Max(0, MorphDuration).ToString(ci)}ms",
                $"min-width:{MinNotchWidth.ToString(ci)}px",
                $"min-height:{MinNotchHeight.ToString(ci)}px"
            };
            if (!String.IsNullOrWhiteSpace(SurfaceColor))
                parts.Add($"--shiny-notch-surface:{SurfaceColor}");
            if (!String.IsNullOrWhiteSpace(Stroke))
                parts.Add($"border:{StrokeThickness.ToString(ci)}px solid {Stroke}");
            if (AdditionalAttributes?.TryGetValue("style", out var style) == true && style is not null)
                parts.Add(style.ToString()!.TrimEnd(';'));
            return String.Join(";", parts);
        }
    }

    protected override void OnParametersSet()
    {
        // A new key after the first render is a content swap: the new element fades in. The first
        // content does not — a notch that is there when the page opens should just be there.
        fadeIn = hasRendered && !Equals(ContentKey, lastKey);
        lastKey = ContentKey;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            hasRendered = true;
            var loaded = await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Shiny.Blazor.Controls/notch.js");
            if (disposed) { await loaded.ReleaseLateAsync(); return; }
            module = loaded;
            selfRef = DotNetObjectReference.Create(this);
        }

        if (module is null || selfRef is null)
            return;

        var options = BuildOptions();
        try
        {
            if (!initialized)
            {
                // Flagged before the await, not after: init measures and reports the size straight back
                // through OnJsSizeChanged, which on WebAssembly runs - and re-renders the parent, and so
                // this component - before the init call returns. A flag set afterwards let that render
                // call init again, which reported again, and the page froze in the loop.
                initialized = true;
                lastOptions = options;
                await module.InvokeVoidAsync("init", root, selfRef, options);
                return;
            }
            else if (options != lastOptions)
            {
                await module.InvokeVoidAsync("update", root, options);
            }
            else
            {
                // Same options, new content: the observer catches size changes on its own, but a
                // re-keyed element is a new node it has never seen.
                await module.InvokeVoidAsync("refresh", root);
            }
            lastOptions = options;
        }
        catch (JSDisconnectedException) { }
        catch (JSException) { }
    }

    // A named DTO, not an anonymous type: trimmed/AOT publish strips anonymous-type constructor
    // parameter names, which the interop serializer needs.
    NotchJsOptions BuildOptions() => new()
    {
        MinWidth = MinNotchWidth,
        MinHeight = MinNotchHeight,
        MaxWidth = MaxNotchWidth ?? -1,
        MaxHeight = MaxNotchHeight ?? -1,
        CornerRadius = CornerRadius,
        Notch = Shape == NotchShape.Notch,
        Screen = !Inline
    };

    async Task HandleClick(MouseEventArgs e)
    {
        if (OnClick.HasDelegate)
            await OnClick.InvokeAsync(e);
    }

    /// <summary>Called by notch.js as a morph starts.</summary>
    [JSInvokable]
    public async Task OnJsSizeChanged(double width, double height)
    {
        Size = new NotchSize(width, height);
        if (OnNotchSizeChanged.HasDelegate)
            await OnNotchSizeChanged.InvokeAsync(Size);
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        var loaded = module;
        module = null;
        if (loaded is not null)
        {
            // The module is released whether or not the JS teardown got through: a failed dispose call
            // must not leave the module reference tracked for the life of the circuit.
            try
            {
                await loaded.InvokeVoidAsync("dispose", root);
            }
            catch (JSDisconnectedException) { }
            catch (JSException) { }
            catch (ObjectDisposedException) { }
            catch (OperationCanceledException) { }

            await loaded.ReleaseLateAsync();
        }
        selfRef?.Dispose();
        selfRef = null;
    }
}

sealed record NotchJsOptions
{
    public double MinWidth { get; init; }
    public double MinHeight { get; init; }
    public double MaxWidth { get; init; }
    public double MaxHeight { get; init; }
    public double CornerRadius { get; init; }
    public bool Notch { get; init; }
    public bool Screen { get; init; }
}
