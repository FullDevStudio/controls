namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The home page's catalogue search: typing swaps the sectioned browse for one ranked result grid,
/// Escape / ✕ put the browse back, and Enter opens the best match.
/// </summary>
public class HomeTests : BlazorTest
{
    ILocator Search => Page.GetByRole(AriaRole.Searchbox, new() { Name = "Search controls" });
    ILocator ResultsHeading => Page.Locator("h2.catalog-heading", new() { HasText = "Results" });

    [Test]
    public async Task Typing_Swaps_Browse_For_Ranked_Results()
    {
        await Open("/");
        var sectionsBefore = await Page.Locator("section.catalog-section").CountAsync();
        await Assert.That(sectionsBefore).IsGreaterThan(1);

        await Search.FillAsync("slider");

        await Expect(ResultsHeading).ToBeVisibleAsync();
        await Expect(Page.Locator("section.catalog-section")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".catalog-card .catalog-name").First).ToContainTextAsync("Slider");

        // the count badge on the heading reports exactly the cards shown
        var shown = await Page.Locator(".catalog-grid .catalog-card").CountAsync();
        await Expect(ResultsHeading.Locator(".catalog-count")).ToHaveTextAsync(shown.ToString());
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task No_Match_Says_So()
    {
        await Open("/");
        await Search.FillAsync("zzqqxx");

        await Expect(Page.Locator(".catalog-empty")).ToContainTextAsync("Nothing matches");
        await Expect(ResultsHeading.Locator(".catalog-count")).ToHaveTextAsync("0");
        await Expect(Page.Locator(".catalog-card")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clear_Button_Restores_Browse()
    {
        await Open("/");
        var clear = Page.GetByRole(AriaRole.Button, new() { Name = "Clear search" });
        await Expect(clear).ToHaveCountAsync(0);

        await Search.FillAsync("grid");
        await Expect(clear).ToBeVisibleAsync();

        await clear.ClickAsync();

        await Expect(Search).ToHaveValueAsync("");
        await Expect(ResultsHeading).ToHaveCountAsync(0);
        await Expect(clear).ToHaveCountAsync(0);
        await Assert.That(await Page.Locator("section.catalog-section").CountAsync()).IsGreaterThan(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Escape_Clears_The_Query()
    {
        await Open("/");
        await Search.FillAsync("office");
        await Expect(ResultsHeading).ToBeVisibleAsync();

        await Search.PressAsync("Escape");

        await Expect(Search).ToHaveValueAsync("");
        await Expect(ResultsHeading).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Enter_Opens_The_Best_Match()
    {
        await Open("/");
        await Search.FillAsync("slider");
        var firstHref = await Page.Locator(".catalog-grid .catalog-card").First.GetAttributeAsync("href");
        await Assert.That(firstHref).IsNotNull();

        await Search.PressAsync("Enter");

        await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath.TrimEnd('/') == "/" + firstHref!.Trim('/'));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clicking_A_Result_Navigates_To_Its_Demo()
    {
        await Open("/");
        await Search.FillAsync("zoom pan");
        var card = Page.Locator(".catalog-grid .catalog-card").First;
        var href = await card.GetAttributeAsync("href");

        await card.ClickAsync();

        await Page.WaitForURLAsync(url => new Uri(url).AbsolutePath.TrimEnd('/') == "/" + href!.Trim('/'));
        await AssertNoConsoleErrors();
    }
}
