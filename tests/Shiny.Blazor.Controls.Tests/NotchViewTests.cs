using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

/// <summary>
/// notch.js owns the measuring; what the component decides is the markup the script measures and the
/// custom properties the stylesheet animates with.
/// </summary>
public class NotchViewTests
{
    sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => default;
    }

    static async Task<string> RenderAsync(params (string Name, object? Value)[] parameters)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime>(new NoJs());
        var provider = services.BuildServiceProvider();

        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var values = new Dictionary<string, object?>();
            foreach (var (name, value) in parameters)
                values[name] = value;

            var output = await renderer.RenderComponentAsync<NotchView>(ParameterView.FromDictionary(values));
            return System.Net.WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    static RenderFragment Text(string text) => b => b.AddContent(0, text);


    [Fact]
    public async Task RendersTheContentInsideTheSurface()
    {
        var html = await RenderAsync(("ChildContent", Text("Now playing")));

        html.ShouldContain("class=\"shiny-notch shiny-notch--island");
        html.ShouldContain("shiny-notch__content");
        html.ShouldContain("Now playing");
    }


    [Fact]
    public async Task NotchShapeGetsItsOwnClass()
    {
        var html = await RenderAsync(("Shape", NotchShape.Notch));

        html.ShouldContain("shiny-notch--notch");
    }


    [Fact]
    public async Task MinimumSizeAndTimingAreInlined()
    {
        var html = await RenderAsync(("MinNotchWidth", 200d), ("MinNotchHeight", 40d), ("MorphDuration", 250));

        html.ShouldContain("min-width:200px");
        html.ShouldContain("min-height:40px");
        html.ShouldContain("--shiny-notch-duration:250ms");
    }


    [Fact]
    public async Task NoBorderUnlessAStrokeIsGiven()
    {
        (await RenderAsync()).ShouldNotContain("border:");
        (await RenderAsync(("Stroke", "#333"), ("StrokeThickness", 2d))).ShouldContain("border:2px solid #333");
    }


    [Fact]
    public async Task ACallersStyleIsMergedNotSwapped()
    {
        var html = await RenderAsync(("style", "margin-top:12px"));

        html.ShouldContain("--shiny-notch-padding:");
        html.ShouldContain("margin-top:12px");
    }


    [Fact]
    public async Task ShadowCanBeTurnedOff()
    {
        (await RenderAsync()).ShouldContain("shiny-notch--shadow");
        (await RenderAsync(("HasShadow", false))).ShouldNotContain("shiny-notch--shadow");
    }


    [Fact]
    public async Task OnlyClickableWhenSomeoneListens()
    {
        (await RenderAsync()).ShouldNotContain("shiny-notch--clickable");

        var html = await RenderAsync(("OnClick", EventCallback.Factory.Create<MouseEventArgs>(this, () => { })));
        html.ShouldContain("shiny-notch--clickable");
    }
}
