using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Shiny.Blazor.Controls;

/// <summary>
/// A block - used like a <c>div</c> - that draws an animated <see cref="Effect"/> (aurora, particles,
/// snow, rain, meteors, a dot pattern with a spotlight, film grain, ...) behind its content.
/// </summary>
/// <example>
/// <code>
/// &lt;AnimatedBackground Effect="snow" Background="#0b1020" CornerRadius="16" Style="padding:24px"&gt;
///     &lt;h2&gt;Let it snow&lt;/h2&gt;
/// &lt;/AnimatedBackground&gt;
///
/// @code {
///     readonly SnowEffect snow = new() { Density = 7, Wind = 0.3 };
/// }
/// </code>
/// </example>
/// <remarks>
/// The effect is drawn on a canvas by a script, so nothing round-trips through .NET per frame. It
/// pauses while scrolled out of view or in a hidden tab, and holds a still frame under
/// <c>prefers-reduced-motion: reduce</c>. Clicks go straight through the canvas to the content.
/// </remarks>
public partial class AnimatedBackground
{
    internal const string ModulePath = "./_content/Shiny.Blazor.Controls/animated-background.js";

    readonly string id = Guid.NewGuid().ToString("N");
    ElementReference root;
    ElementReference canvas;
    IJSObjectReference? module;
    bool disposed;
    bool initialized;
    string? lastSignature;

    /// <summary>What sits on top of the effect.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The effect drawn behind the content. Null draws nothing but the background.</summary>
    [Parameter] public BackgroundEffect? Effect { get; set; }

    /// <summary>The CSS background behind the effect - a colour or a gradient. Most effects are made for a dark one.</summary>
    [Parameter] public string? Background { get; set; }

    /// <summary>The radius of the corners, in CSS pixels. The background, effect and content are clipped to it. Default 0.</summary>
    [Parameter] public double CornerRadius { get; set; }

    /// <summary>Whether it animates. False holds the effect where it is.</summary>
    [Parameter] public bool IsRunning { get; set; } = true;

    /// <summary>Holds a still frame under <c>prefers-reduced-motion: reduce</c>. On by default.</summary>
    [Parameter] public bool RespectReducedMotion { get; set; } = true;

    /// <summary>
    /// Pins the effect to the viewport, behind the page, instead of to this block - a full-page
    /// background that stays put while the content scrolls over it.
    /// </summary>
    [Parameter] public bool Fixed { get; set; }

    [Parameter] public string? Class { get; set; }

    [Parameter] public string? Style { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }


    internal string StateClasses => this.Fixed ? "shiny-animated-background--fixed" : string.Empty;

    internal string RootStyle
    {
        get
        {
            var style = string.Empty;
            if (this.CornerRadius > 0)
                style += $"border-radius:{this.CornerRadius.ToString("0.##", CultureInfo.InvariantCulture)}px;";
            if (!string.IsNullOrWhiteSpace(this.Background))
                style += $"background:{this.Background};";

            return string.IsNullOrWhiteSpace(this.Style) ? style : style + this.Style;
        }
    }

    /// <summary>Everything the script is told, so a re-render that changes nothing sends nothing.</summary>
    internal string Signature => $"{this.IsRunning}|{this.RespectReducedMotion}|{this.Fixed}|{this.Effect?.ToJson()}";


    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        try
        {
            if (firstRender)
            {
                var loaded = await this.JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
                if (this.disposed) { await loaded.ReleaseLateAsync(); return; }
                this.module = loaded;
            }
            if (this.module is null)
                return;

            var signature = this.Signature;
            if (signature == this.lastSignature)
                return;

            this.lastSignature = signature;
            var json = this.Effect?.ToJson();

            if (this.initialized)
            {
                await this.module.InvokeVoidAsync("update", this.id, json, this.IsRunning, this.RespectReducedMotion, this.Fixed);
            }
            else
            {
                await this.module.InvokeVoidAsync("init", this.id, this.root, this.canvas, json, this.IsRunning, this.RespectReducedMotion, this.Fixed);
                this.initialized = true;
            }
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
        catch (TaskCanceledException) { }
        catch (JSException) { }
    }


    public async ValueTask DisposeAsync()
    {
        this.disposed = true;
        if (this.module is null)
            return;

        try
        {
            await this.module.InvokeVoidAsync("dispose", this.id);
            await this.module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        catch (ObjectDisposedException) { }
        catch (TaskCanceledException) { }
        catch (JSException) { }
        GC.SuppressFinalize(this);
    }
}
