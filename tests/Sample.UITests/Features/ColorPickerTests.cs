namespace Sample.UITests.Features;

/// <summary>
/// ColorPickerButton: SelectedColor binds two-way to the view model (which formats it for the
/// preview and the button caption), ShowOpacity follows the page's switch, and the trigger opens a
/// ColorPicker popup seeded with the current colour whose edits flow back before Done closes it.
/// </summary>
public class ColorPickerTests : UITest
{
    const string Route = "colorpicker";
    const string PageType = "ColorPickerPage";

    /// <summary>A colour different from whatever the cached page is showing now.</summary>
    async Task<string> NextColor(params string[] candidates)
    {
        var current = await Driver.GetProperty("Text", automationId: "ColorDisplay");
        return candidates.First(c => !String.Equals(c, current, StringComparison.OrdinalIgnoreCase));
    }

    async Task ClosePopupIfOpen()
    {
        if ((await Driver.CssNow("ColorPicker")).Count > 0)
            await Driver.TapCss("ColorPicker + Button");
    }

    [Test]
    public async Task SelectedColor_Updates_Preview_And_Caption()
    {
        await Open(Route, PageType);
        await ClosePopupIfOpen();
        var color = await NextColor("#FF0000", "#00FF00");

        await Driver.SetProperty("SelectedColor", color, automationId: "ColorPickerButton");

        await Driver.WaitForValue("ColorDisplay", "Text", color);
        await Driver.WaitForValue("ColorPickerButton", "Text", $"Pick Color ({color})");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Translucent_Color_Shows_Alpha_In_Hex()
    {
        await Open(Route, PageType);
        await ClosePopupIfOpen();

        await Driver.SetProperty("SelectedColor", "#00FF0000", automationId: "ColorPickerButton");

        await Driver.WaitForValue("ColorDisplay", "Text", "#00FF0000");
        await Driver.WaitForValue("ColorPickerButton", "Text", "Pick Color (#00FF0000)");
    }

    [Test]
    public async Task Opacity_Switch_Drives_ShowOpacity()
    {
        await Open(Route, PageType);
        await ClosePopupIfOpen();
        var before = await Driver.GetProperty("ShowOpacity", automationId: "ColorPickerButton");
        var flipped = before == "True" ? "False" : "True";

        await Driver.Tap(automationId: "ShowOpacitySwitch");
        await Driver.WaitForValue("ColorPickerButton", "ShowOpacity", flipped);

        await Driver.Tap(automationId: "ShowOpacitySwitch");
        await Driver.WaitForValue("ColorPickerButton", "ShowOpacity", before);
    }

    [Test]
    public async Task Trigger_Opens_Popup_Seeded_With_Current_Color()
    {
        await Open(Route, PageType);
        await ClosePopupIfOpen();
        await Driver.SetProperty("SelectedColor", "#FF00FF", automationId: "ColorPickerButton");
        await Driver.WaitForValue("ColorDisplay", "Text", "#FF00FF");

        await Driver.TapCss("#ColorPickerButton Border");

        await Driver.Css("ColorPicker");
        await Driver.WaitForCssValue("ColorPicker Entry", "Text", "#FF00FF");

        await Driver.TapCss("ColorPicker + Button");
        await Driver.WaitUntil(async () => (await Driver.CssNow("ColorPicker")).Count == 0, "the popup to close");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Popup_Edits_Flow_Back_To_The_Binding()
    {
        await Open(Route, PageType);
        await ClosePopupIfOpen();
        var color = await NextColor("#0000FF", "#FFFF00");

        await Driver.TapCss("#ColorPickerButton Border");
        await Driver.SetCss("ColorPicker", "SelectedColor", color);

        await Driver.WaitForValue("ColorDisplay", "Text", color);
        await Driver.WaitForCssValue("ColorPicker Entry", "Text", color);

        await Driver.TapCss("ColorPicker + Button");
        await Driver.WaitUntil(async () => (await Driver.CssNow("ColorPicker")).Count == 0, "the popup to close");
        await Driver.WaitForValue("ColorDisplay", "Text", color);
        await AssertNoAppErrors();
    }
}
