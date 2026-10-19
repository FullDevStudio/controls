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
/// What a screen can change about the picker without picking anything: photos it hands in (some of
/// them locked), how far apart the thumbnails sit, and what the add button looks like.
/// </summary>
public class MediaPickerButtonTests
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

            var output = await renderer.RenderComponentAsync<MediaPickerButton>(ParameterView.FromDictionary(values));
            return System.Net.WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    static MediaPickerItem Saved(string url, bool canRemove = true)
        => new() { DataUri = url, ContentType = "image/jpeg", CanRemove = canRemove };

    static int Count(string html, string text)
    {
        var count = 0;
        for (var i = html.IndexOf(text, StringComparison.Ordinal); i >= 0; i = html.IndexOf(text, i + text.Length, StringComparison.Ordinal))
            count++;
        return count;
    }


    [Fact]
    public async Task PhotosHandedInAreShown()
    {
        var html = await RenderAsync(
            ("MaxPhotos", 5),
            ("Photos", new[] { Saved("/a.jpg"), Saved("/b.jpg") })
        );

        html.ShouldContain("src=\"/a.jpg\"");
        html.ShouldContain("src=\"/b.jpg\"");
        html.ShouldNotContain("No photos yet");
    }


    [Fact]
    public async Task ALockedPhotoHasNoRemoveButton()
    {
        var html = await RenderAsync(
            ("MaxPhotos", 5),
            ("Photos", new[] { Saved("/locked.jpg", canRemove: false), Saved("/loose.jpg") })
        );

        Count(html, "shiny-mediapicker-remove").ShouldBe(1);

        var loose = html.IndexOf("/loose.jpg", StringComparison.Ordinal);
        html.IndexOf("shiny-mediapicker-remove", StringComparison.Ordinal).ShouldBeGreaterThan(loose);
    }


    [Fact]
    public async Task ItemSpacingSetsTheStripGap()
    {
        var standard = await RenderAsync(("Photos", new[] { Saved("/a.jpg") }));
        var spaced = await RenderAsync(("Photos", new[] { Saved("/a.jpg") }), ("ItemSpacing", 4.5));

        standard.ShouldContain("gap:12px");
        spaced.ShouldContain("gap:4.5px");
    }


    [Fact]
    public async Task AddButtonTemplateReplacesTheDashedButton_AndKeepsTheTextAsItsName()
    {
        RenderFragment template = b => b.AddMarkupContent(0, "<span class=\"mine\">Snap one</span>");

        var html = await RenderAsync(("AddButtonTemplate", template), ("AddButtonText", "Add a photo"));

        html.ShouldContain("class=\"shiny-mediapicker-add-custom\"");
        html.ShouldContain("aria-label=\"Add a photo\"");
        html.ShouldContain("<span class=\"mine\">Snap one</span>");
        html.ShouldNotContain("class=\"shiny-mediapicker-add\"");
    }


    [Fact]
    public async Task ACustomAddButtonStillHidesAtMaxPhotos()
    {
        RenderFragment template = b => b.AddContent(0, "Snap one");

        var html = await RenderAsync(
            ("AddButtonTemplate", template),
            ("MaxPhotos", 1),
            ("Photos", new[] { Saved("/a.jpg") })
        );

        html.ShouldNotContain("Snap one");
    }
}
