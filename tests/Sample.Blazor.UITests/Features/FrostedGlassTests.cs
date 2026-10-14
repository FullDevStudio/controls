namespace Sample.Blazor.UITests.Features;

/// <summary>FrostedGlass on <c>/frostedglass</c>: blur, corner radius and tint parameters reach the element, live.</summary>
public class FrostedGlassTests : BlazorTest
{
    // the Live Tuning section owns the sliders, the tint buttons and the card they drive
    ILocator Tuning => Page.Locator(".demo-section").Filter(new() { HasTextString = "Live Tuning" });
    ILocator Card => Tuning.Locator(".shiny-frosted-glass");

    static Task<string> Css(ILocator element, string property)
        => element.EvaluateAsync<string>($"e => getComputedStyle(e).getPropertyValue('{property}')");

    [Test]
    public async Task Static_Cards_Apply_Their_Blur_Tint_And_Corners()
    {
        await Open("frostedglass");
        var first = Page.Locator(".shiny-frosted-glass").First;

        await Assert.That(await Css(first, "backdrop-filter")).IsEqualTo("blur(20px)");
        await Expect(first).ToHaveCSSAsync("background-color", "rgba(255, 255, 255, 0.6)");
        await Expect(first).ToHaveCSSAsync("border-top-left-radius", "16px");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Blur_Slider_Updates_The_Live_Card()
    {
        await Open("frostedglass");
        await Assert.That(await Css(Card, "backdrop-filter")).IsEqualTo("blur(20px)");

        await Tuning.Locator("input[type=range]").Nth(0).FillAsync("35");
        await Expect(Card.Locator("h3")).ToContainTextAsync("Blur 35");
        await Assert.That(await Css(Card, "backdrop-filter")).IsEqualTo("blur(35px)");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Corner_Slider_Updates_The_Live_Card()
    {
        await Open("frostedglass");
        await Tuning.Locator("input[type=range]").Nth(1).FillAsync("4");
        await Expect(Card.Locator("h3")).ToContainTextAsync("Radius 4");
        await Expect(Card).ToHaveCSSAsync("border-top-left-radius", "4px");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Tint_Buttons_Retint_The_Live_Card()
    {
        await Open("frostedglass");
        await Tuning.GetByRole(AriaRole.Button, new() { Name = "Dark", Exact = true }).ClickAsync();
        await Expect(Card).ToHaveCSSAsync("background-color", "rgba(0, 0, 0, 0.5)");

        await Tuning.GetByRole(AriaRole.Button, new() { Name = "Purple", Exact = true }).ClickAsync();
        await Expect(Card).ToHaveCSSAsync("background-color", "rgba(124, 58, 237, 0.45)");
        await AssertNoConsoleErrors();
    }
}
