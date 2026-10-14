namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The ImageEditor's ribbon: tool toggles, rotate feeding undo/redo, and the View tab's zoom
/// commands reporting back through ZoomLevelChanged.
/// </summary>
public class ImageEditorTests : BlazorTest
{
    ILocator Editor => Page.Locator(".shiny-imgeditor");
    ILocator Ribbon => Editor.Locator(".shiny-ribbon");

    ILocator RibbonButton(string name) => Ribbon.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    [Test]
    public async Task Tool_Toggle_Selects_And_Puts_Down()
    {
        await Open("/imageeditor");
        await Expect(RibbonButton("Move")).ToHaveAttributeAsync("aria-pressed", "true");

        await RibbonButton("Draw").ClickAsync();
        await Expect(RibbonButton("Draw")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(RibbonButton("Move")).ToHaveAttributeAsync("aria-pressed", "false");

        // pressing the tool in hand puts it down again
        await RibbonButton("Draw").ClickAsync();
        await Expect(RibbonButton("Move")).ToHaveAttributeAsync("aria-pressed", "true");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Ink_Tool_Opens_Its_Contextual_Tab()
    {
        await Open("/imageeditor");
        var tabs = Ribbon.GetByRole(AriaRole.Tab);
        var before = await tabs.CountAsync();

        await RibbonButton("Draw").ClickAsync();

        await Expect(tabs).ToHaveCountAsync(before + 1);

        await RibbonButton("Move").ClickAsync();
        await Expect(tabs).ToHaveCountAsync(before);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Rotate_Enables_Undo_And_Undo_Enables_Redo()
    {
        await Open("/imageeditor");
        await Expect(RibbonButton("Undo")).ToBeDisabledAsync();
        await Expect(RibbonButton("Redo")).ToBeDisabledAsync();

        await RibbonButton("Rotate").ClickAsync();
        await Expect(RibbonButton("Undo")).ToBeEnabledAsync();
        await Expect(RibbonButton("Reset")).ToBeEnabledAsync();

        await RibbonButton("Undo").ClickAsync();
        await Expect(RibbonButton("Undo")).ToBeDisabledAsync();
        await Expect(RibbonButton("Redo")).ToBeEnabledAsync();

        await RibbonButton("Redo").ClickAsync();
        await Expect(RibbonButton("Undo")).ToBeEnabledAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Zoom_Commands_Report_Through_ZoomLevelChanged()
    {
        await Open("/imageeditor");
        var readout = Page.Locator("span", new() { HasTextRegex = new("^Zoom: \\d+%$") });
        await Expect(readout).ToHaveTextAsync("Zoom: 100%");

        await Ribbon.GetByRole(AriaRole.Tab, new() { Name = "View" }).ClickAsync();
        await RibbonButton("Zoom in").ClickAsync();
        await Expect(readout).ToHaveTextAsync("Zoom: 150%");

        await RibbonButton("Fit to view").ClickAsync();
        await Expect(readout).ToHaveTextAsync("Zoom: 100%");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Zoom_Is_Capped_At_MaxZoom()
    {
        await Open("/imageeditor");
        var readout = Page.Locator("span", new() { HasTextRegex = new("^Zoom: \\d+%$") });
        await Ribbon.GetByRole(AriaRole.Tab, new() { Name = "View" }).ClickAsync();

        for (var i = 0; i < 8; i++)
            await RibbonButton("Zoom in").ClickAsync();

        // MaxZoom="8" on the sample
        await Expect(readout).ToHaveTextAsync("Zoom: 800%");
        await AssertNoConsoleErrors();
    }
}
