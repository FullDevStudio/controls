using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>
/// VideoText draws an inline SVG overlay with the text knocked out (or, with no overlay colour, a CSS
/// mask on the wrapper). These assert on that SVG and the &lt;video&gt;'s properties; nothing waits on
/// the remote clip to load.
/// </summary>
public class VideoTextTests : BlazorTest
{
    ILocator Hero => Page.Locator(".shiny-video-text").Nth(0);
    ILocator MarqueeDemo => Page.Locator(".shiny-video-text").Nth(1);
    ILocator PlainDemo => Page.Locator(".shiny-video-text").Nth(2);

    ILocator Sheet(ILocator control) => control.Locator(".shiny-video-text-overlay svg > rect[mask]");
    ILocator TextGroup(ILocator control) => control.Locator(".shiny-video-text-overlay mask g");

    ILocator Select(string label) => Page.Locator("label", new() { HasTextString = label }).Locator("select");
    ILocator Option(string label) => Page.Locator("label", new() { HasTextString = label }).Locator("input");

    Task<string> Transform(ILocator group) => group.EvaluateAsync<string>("g => getComputedStyle(g).transform");


    [Test]
    public async Task Default_Is_A_Solid_Black_Overlay_Over_A_Muted_Loop()
    {
        await Open("videotext");

        await Expect(Sheet(Hero)).ToHaveAttributeAsync("fill", "black");
        await Expect(Sheet(Hero)).ToHaveAttributeAsync("opacity", "1");
        await Expect(TextGroup(Hero).Locator("text")).ToHaveTextAsync("BLOOM");

        var video = Hero.Locator("video");
        await Assert.That(await video.EvaluateAsync<bool>("v => v.muted && v.loop && v.autoplay")).IsTrue();
        // a decorative loop has no controls
        await Assert.That(await video.EvaluateAsync<bool>("v => v.controls")).IsFalse();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Overlay_Colour_And_Opacity_Reach_The_Sheet()
    {
        await Open("videotext");

        await Select("Overlay colour").SelectOptionAsync("#312e81");
        await Expect(Sheet(Hero)).ToHaveAttributeAsync("fill", "#312e81");

        await Option("Overlay opacity").FillAsync("0.5");
        await Expect(Sheet(Hero)).ToHaveAttributeAsync("opacity", "0.5");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task No_Overlay_Switches_To_A_Css_Mask()
    {
        await Open("videotext");
        await Expect(Hero).Not.ToHaveCSSAsync("mask-image", new Regex("svg"));

        await Select("Overlay colour").SelectOptionAsync("none");

        await Expect(Hero.Locator(".shiny-video-text-overlay")).ToHaveCountAsync(0);
        await Expect(Hero).ToHaveCSSAsync("mask-image", new Regex("data:image/svg"));

        // the page's third demo is built this way from the start
        await Expect(PlainDemo.Locator(".shiny-video-text-overlay")).ToHaveCountAsync(0);
        await Expect(PlainDemo).ToHaveCSSAsync("mask-image", new Regex("BLOOM"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Marquee_Demo_Scrolls()
    {
        await Open("videotext");
        var group = TextGroup(MarqueeDemo);

        // the marquee lays out in pixels, so it only starts once the element's size has been reported
        await Expect(group).ToHaveAttributeAsync("id", new Regex("-text$"));
        var first = await Transform(group);
        await Page.WaitForTimeoutAsync(500);
        var second = await Transform(group);

        await Assert.That(second).IsNotEqualTo(first);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Marquee_Keeps_Running_After_An_Overlay_Change()
    {
        await Open("videotext");
        await Select("Marquee").SelectOptionAsync("Up");
        await Option("Overlay opacity").FillAsync("0.4");

        var group = TextGroup(Hero);
        await Expect(group).ToHaveAttributeAsync("id", new Regex("-text$"));
        var first = await Transform(group);
        await Page.WaitForTimeoutAsync(500);
        var second = await Transform(group);

        // Up moves the text along y only
        await Assert.That(second).IsNotEqualTo(first);
        await Assert.That(await group.EvaluateAsync<double>("g => new DOMMatrix(getComputedStyle(g).transform).e")).IsEqualTo(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Marquee_Also_Scrolls_Inside_The_Css_Mask()
    {
        await Open("videotext");
        await Select("Overlay colour").SelectOptionAsync("none");
        await Select("Marquee").SelectOptionAsync("Left");

        await Expect(Hero).ToHaveCSSAsync("mask-image", new Regex("keyframes"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Font_Reaches_The_Text()
    {
        await Open("videotext");
        await Select("Font").SelectOptionAsync("Georgia, serif");

        await Expect(TextGroup(Hero)).ToHaveAttributeAsync("font-family", "Georgia, serif");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Muted_Checkbox_Sets_The_Video_Property()
    {
        await Open("videotext");
        var video = Hero.Locator("video");

        await Option("Muted").UncheckAsync();
        // the property, not just the attribute; the browser may still re-mute a refused audible autoplay
        await Page.WaitForFunctionAsync("() => document.querySelector('.shiny-video-text video').defaultMuted === false");

        await Option("Muted").CheckAsync();
        await Page.WaitForFunctionAsync("() => document.querySelector('.shiny-video-text video').muted === true");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Text_Changes_Redraw_The_Knockout()
    {
        await Open("videotext");
        await Page.Locator("label", new() { HasTextString = "Text" }).Locator("input").First.FillAsync("SURF");

        await Expect(TextGroup(Hero).Locator("text")).ToHaveTextAsync("SURF");
        await Expect(Hero).ToHaveAttributeAsync("aria-label", "SURF");
        await AssertNoConsoleErrors();
    }
}
