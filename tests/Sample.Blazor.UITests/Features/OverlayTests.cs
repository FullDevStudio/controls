namespace Sample.Blazor.UITests.Features;

/// <summary>Overlay + LoadingOverlay on <c>/overlay</c>.</summary>
public class OverlayTests : BlazorTest
{
    ILocator VisibleContent => Page.Locator(".shiny-overlay-content--visible");

    [Test]
    public async Task Custom_Overlay_Shows_Its_Content_And_Dismisses()
    {
        await Open("overlay");
        await Expect(VisibleContent).ToHaveCountAsync(0);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Show Custom Overlay", Exact = true }).ClickAsync();
        await Expect(VisibleContent).ToContainTextAsync("Custom Overlay Content");
        await Expect(Page.Locator(".shiny-overlay-backdrop--visible")).ToHaveCountAsync(1);

        await VisibleContent.GetByRole(AriaRole.Button, new() { Name = "Dismiss", Exact = true }).ClickAsync();
        await Expect(VisibleContent).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Indeterminate_Loading_Shows_Spinner_And_Message_Then_Hides()
    {
        await Open("overlay");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show Loading (Spinner)", Exact = true }).ClickAsync();

        await Expect(VisibleContent.Locator(".shiny-loading-spinner")).ToBeVisibleAsync();
        await Expect(VisibleContent.Locator(".shiny-loading-message")).ToHaveTextAsync("Loading, please wait...");

        // the page hides it after 3s
        await Expect(VisibleContent).ToHaveCountAsync(0, new() { Timeout = 8000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Determinate_Loading_Fills_To_Complete_Then_Hides()
    {
        await Open("overlay");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show Loading (Progress Bar)", Exact = true }).ClickAsync();

        await Expect(VisibleContent.Locator(".shiny-loading-message")).ToHaveTextAsync("Downloading...");
        await Expect(VisibleContent.Locator(".shiny-loading-spinner")).ToHaveCountAsync(0);
        await Expect(VisibleContent.Locator(".shiny-loading-progress")).ToBeVisibleAsync();

        await Expect(VisibleContent.Locator(".shiny-loading-message")).ToHaveTextAsync("Complete!", new() { Timeout = 8000 });
        await Expect(VisibleContent.Locator(".shiny-loading-progress")).ToContainTextAsync("100");
        await Expect(VisibleContent).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await AssertNoConsoleErrors();
    }
}
