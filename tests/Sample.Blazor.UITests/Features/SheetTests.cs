namespace Sample.Blazor.UITests.Features;

/// <summary>SheetView on <c>/sheet</c>: open/close, backdrop dismissal, detents, and the locked selector sheet.</summary>
public class SheetTests : BlazorTest
{
    ILocator OpenSheets => Page.Locator(".shiny-sheet-root.is-open");

    // The detents sheet is the first SheetView on the page, the locked country selector the second.
    ILocator DetentSheet => Page.Locator(".shiny-sheet-root").Nth(0);
    ILocator SelectorSheet => Page.Locator(".shiny-sheet-root").Nth(1);

    ILocator IsOpenState => Page.Locator(".demo-section .state code").First;

    [Test]
    public async Task Open_Then_Close_From_Inside_Updates_Binding()
    {
        await Open("sheet");
        await Expect(IsOpenState).ToHaveTextAsync("False");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Open Sheet", Exact = true }).ClickAsync();
        await Expect(DetentSheet).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bis-open\b"));
        await Expect(IsOpenState).ToHaveTextAsync("True");

        await DetentSheet.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await Expect(OpenSheets).ToHaveCountAsync(0);
        await Expect(IsOpenState).ToHaveTextAsync("False");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Backdrop_Click_Dismisses_Unlocked_Sheet()
    {
        await Open("sheet");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open Sheet", Exact = true }).ClickAsync();
        await Expect(OpenSheets).ToHaveCountAsync(1);

        // top-left of the viewport is backdrop - the sheet docks at the bottom
        await DetentSheet.Locator(".shiny-sheet-backdrop").ClickAsync(new() { Position = new() { X = 20, Y = 20 } });

        await Expect(OpenSheets).ToHaveCountAsync(0);
        await Expect(IsOpenState).ToHaveTextAsync("False");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Opens_At_Lowest_Detent_And_Follows_Detent_Changes()
    {
        await Open("sheet");
        var viewport = Page.ViewportSize!.Height;

        // default detents are Quarter/Half/Full - it opens at the lowest
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open Sheet", Exact = true }).ClickAsync();
        var container = DetentSheet.Locator(".shiny-sheet-container");
        await Expect(container).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex($@"--sheet-visible-height:\s*{viewport / 4}px"));

        await DetentSheet.GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();
        await Expect(OpenSheets).ToHaveCountAsync(0);

        // Half + Full only: the lowest detent is now half the viewport
        await Page.GetByRole(AriaRole.Button, new() { Name = "Half + Full only", Exact = true }).ClickAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open Sheet", Exact = true }).ClickAsync();
        await Expect(container).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex($@"--sheet-visible-height:\s*{viewport / 2}px"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Locked_Sheet_Ignores_Backdrop_And_Closes_On_Selection()
    {
        await Open("sheet");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Choose a Country", Exact = true }).ClickAsync();
        await Expect(SelectorSheet).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bis-open\b"));

        await SelectorSheet.Locator(".shiny-sheet-backdrop").ClickAsync(new() { Position = new() { X = 20, Y = 20 }, Force = true });
        await Page.WaitForTimeoutAsync(500);
        await Expect(SelectorSheet).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bis-open\b"));

        await SelectorSheet.Locator(".selector-item", new() { HasTextString = "Canada" }).ClickAsync();
        await Expect(OpenSheets).ToHaveCountAsync(0);
        await Expect(Page.Locator(".demo-section .state", new() { HasTextString = "Selected:" })).ToContainTextAsync("Canada");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Locked_Sheet_Cancel_Closes_Without_Selecting()
    {
        await Open("sheet");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Choose a Country", Exact = true }).ClickAsync();
        await Expect(OpenSheets).ToHaveCountAsync(1);

        await SelectorSheet.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
        await Expect(OpenSheets).ToHaveCountAsync(0);
        await Expect(Page.Locator(".demo-section .state", new() { HasTextString = "Selected:" })).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }
}
