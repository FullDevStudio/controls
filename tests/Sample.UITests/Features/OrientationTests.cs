namespace Sample.UITests.Features;

/// <summary>
/// Layout that responds to the space it is given. The image editor's ribbon simplifies itself below
/// 600 wide (SimplifyBelowWidth), so it is a direct read-out of whether a size change reached layout.
/// Desktop heads resize the window; phones have no rotation API through the agent, so there the rule is
/// checked against whatever width the device has.
/// Replaces the old screenshot-only OrientationTests.
/// </summary>
public class OrientationTests : UITest
{
    const double SimplifyBelow = 600;

    async Task<double?> RibbonWidth()
    {
        var ribbon = await Driver.FindByType("Ribbon");
        var raw = await Driver.GetProperty("Width", elementId: ribbon.Id);
        return Double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : null;
    }

    async Task<string?> RibbonMode()
        => await Driver.GetProperty("DisplayMode", elementId: (await Driver.FindByType("Ribbon")).Id);

    async Task OpenEditor()
    {
        await Open("imageeditor", "ImageEditorPage");
        await Driver.WaitUntil(async () => await RibbonWidth() > 0, "the editor ribbon to be laid out");
    }


    [Test]
    public async Task Ribbon_Mode_Matches_The_Width_It_Has()
    {
        await OpenEditor();

        var width = await RibbonWidth();
        var expected = width < SimplifyBelow ? "Simplified" : "Expanded";
        await Driver.WaitUntil(async () => await RibbonMode() == expected, $"a {width}-wide ribbon to be {expected}");
        await AssertNoAppErrors();
    }


    [Test, OnlyOn(UIPlatform.MacOS, UIPlatform.Linux, UIPlatform.Windows)]
    public async Task Narrowing_The_Window_Simplifies_The_Ribbon_And_Widening_Restores_It()
    {
        await OpenEditor();
        try
        {
            await Driver.Resize(1400, 900);
            await Driver.WaitUntil(async () => await RibbonWidth() >= SimplifyBelow, "a wide window to give the ribbon room", timeoutSeconds: 15);
            await Driver.WaitUntil(async () => await RibbonMode() == "Expanded", "the wide ribbon to expand", timeoutSeconds: 15);

            await Driver.Resize(520, 800);
            await Driver.WaitUntil(async () => await RibbonWidth() < SimplifyBelow, "a narrow window to squeeze the ribbon", timeoutSeconds: 15);
            await Driver.WaitUntil(async () => await RibbonMode() == "Simplified", "the narrow ribbon to simplify", timeoutSeconds: 15);

            await Driver.Resize(1400, 900);
            await Driver.WaitUntil(async () => await RibbonMode() == "Expanded", "widening to expand the ribbon again", timeoutSeconds: 15);
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.Resize(1280, 900);
        }
    }


    [Test, OnlyOn(UIPlatform.MacOS, UIPlatform.Linux, UIPlatform.Windows)]
    public async Task Page_Width_Follows_The_Window()
    {
        await Open("home", "HomePage");
        try
        {
            await Driver.Resize(1400, 900);
            double? wide = null;
            await Driver.WaitUntil(async () => (wide = await Driver.GetDouble("Width", "HomePage")) > 0, "the page to lay out wide", timeoutSeconds: 15);

            await Driver.Resize(800, 900);
            await Driver.WaitUntil(async () => await Driver.GetDouble("Width", "HomePage") is { } w && w > 0 && w < wide, "the page to narrow with the window", timeoutSeconds: 15);

            // and the search still works at the narrower size
            await Driver.SetProperty("Text", "slider", automationId: "CatalogSearch");
            await Driver.WaitForProperty("CatalogResults", "IsVisible", "True");
            await Driver.SetProperty("Text", "", automationId: "CatalogSearch");
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.Resize(1280, 900);
        }
    }
}
