namespace Sample.Blazor.UITests.Features;

/// <summary>
/// ShinyImage always shows something. These assert the states that do not depend on a remote image
/// actually arriving: the failure artwork, and the plain-&lt;img&gt; path DisableProgress takes.
/// </summary>
public class ShinyImageTests : BlazorTest
{
    [Test]
    public async Task Broken_Url_Shows_Error_Glyph_And_Reports_It()
    {
        await Open("/shinyimage");

        // a 404 or no network at all both end here - neither is a successful load
        var glyph = Page.GetByRole(AriaRole.Img, new() { Name = "Deliberately broken image" });
        await Expect(glyph).ToBeVisibleAsync(new() { Timeout = 20_000 });
        await Expect(glyph).ToHaveClassAsync("shiny-img-error-glyph");

        var reported = Page.Locator("p", new() { HasText = "Reported:" });
        await Expect(reported).Not.ToHaveTextAsync("Reported: -");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Disable_Progress_Hands_The_Url_Straight_To_The_Browser()
    {
        await Open("/shinyimage");

        // no streamed fetch, so no blob: url and no ring - the <img> carries the remote url itself
        var thumbs = Page.Locator(".shiny-img img.shiny-img-target[alt=Thumbnail]");
        await Expect(thumbs).ToHaveCountAsync(12, new() { Timeout = 20_000 });
        var src = await thumbs.First.GetAttributeAsync("src");
        await Assert.That(src).StartsWith("https://picsum.photos/seed/shiny-blazor-");
        await AssertNoConsoleErrors();
    }
}
