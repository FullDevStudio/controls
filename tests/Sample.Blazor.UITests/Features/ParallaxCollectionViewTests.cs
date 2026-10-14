using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>/parallaxlist — the Blazor counterpart of MAUI's ParallaxCollectionView.</summary>
public class ParallaxCollectionViewTests : BlazorTest
{
    ILocator Scroller => Page.Locator(".shiny-parallax-scroll");
    ILocator Hero => Page.Locator(".shiny-parallax-hero");
    ILocator VisibleHeader => Page.Locator("p", new() { HasTextString = "Visible header height:" }).Locator("strong");

    Task ScrollTo(int y) => Scroller.EvaluateAsync($"el => el.scrollTop = {y}");

    static Regex Translate(int y) => new(@"translate3d\(0(px)?,\s*" + y + @"px,\s*0(px)?\)");


    [Test]
    public async Task Hero_Translates_At_Half_Speed()
    {
        await Open("parallaxlist");
        await Expect(VisibleHeader).ToHaveTextAsync("260px");

        await ScrollTo(200);
        await Expect(Hero).ToHaveAttributeAsync("style", Translate(-100));
        await Expect(VisibleHeader).ToHaveTextAsync("160px");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Factor_Buttons_Change_The_Speed()
    {
        await Open("parallaxlist");

        await Page.GetByRole(AriaRole.Button, new() { Name = "0.8", Exact = true }).ClickAsync();
        await Expect(Page.Locator("section.demo-section strong").First).ToHaveTextAsync("0.8");

        await ScrollTo(100);
        await Expect(Hero).ToHaveAttributeAsync("style", Translate(-80));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Collapse_To_Sticky_Stops_At_The_Minimum_Height()
    {
        await Open("parallaxlist");
        await Page.Locator("label", new() { HasTextString = "Collapse to sticky" }).Locator("input").CheckAsync();

        await ScrollTo(1000);
        // 260 header − 96 minimum = 164 of travel
        await Expect(Hero).ToHaveAttributeAsync("style", Translate(-164));
        await Expect(VisibleHeader).ToHaveTextAsync("96px");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Fade_Dims_The_Hero_As_It_Scrolls()
    {
        await Open("parallaxlist");
        await Page.Locator("label", new() { HasTextString = "Fade" }).Locator("input").CheckAsync();

        await ScrollTo(130);
        await Expect(Hero).ToHaveAttributeAsync("style", new Regex(@"opacity:\s*0\.5\b"));

        // Turning fade off restores the hero.
        await Page.Locator("label", new() { HasTextString = "Fade" }).Locator("input").UncheckAsync();
        await Expect(Hero).Not.ToHaveAttributeAsync("style", new Regex("opacity"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Clicking_An_Item_Selects_It()
    {
        await Open("parallaxlist");
        await Page.Locator(".shiny-parallax-item", new() { HasTextString = "Coral Reef" }).ClickAsync();
        await Expect(Page.GetByText("Tapped: Coral Reef")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }
}
