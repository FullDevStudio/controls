namespace Sample.UITests.Features;

/// <summary>
/// FontPicker / FontSizePicker and their popup buttons: tapping an inline row selects it, and the
/// selection travels through the view model to the other picker, the button caption and the preview
/// label's FontFamily/FontSize. Only the first two families (the bundled OpenSans fonts) are used —
/// the rest of the list is platform-specific.
/// </summary>
public class FontPickerTests : UITest
{
    const string Route = "fontpicker";
    const string PageType = "FontPickerPage";

    static readonly double[] Sizes = [10, 11, 12, 14, 16, 18, 20, 24, 28, 32, 36, 42, 48, 56, 64];

    [Test]
    public async Task Tapping_A_Family_Row_Selects_It_Everywhere()
    {
        await Open(Route, PageType);
        var current = await Driver.GetProperty("SelectedFont", automationId: "font-picker-inline");
        var (index, font) = current == "OpenSansSemibold" ? (0, "OpenSansRegular") : (1, "OpenSansSemibold");

        await Driver.TapCss("#font-picker-inline Border", index);

        await Driver.WaitForValue("font-picker-inline", "SelectedFont", font);
        await Driver.WaitForValue("font-picker-button", "SelectedFont", font);
        await Driver.WaitForValue("font-preview", "FontFamily", font);
        await Driver.WaitForValue("font-family-label", "Text", $"Family: {font}");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Tapping_A_Size_Row_Selects_It_Everywhere()
    {
        await Open(Route, PageType);
        var current = await Driver.GetDouble("SelectedFontSize", "font-size-picker-inline");
        var size = current == 48 ? 24d : 48d;

        await Driver.TapCss("#font-size-picker-inline Border", Array.IndexOf(Sizes, size));

        var text = size.ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Driver.WaitForValue("font-size-picker-inline", "SelectedFontSize", text);
        await Driver.WaitForValue("font-size-picker-button", "SelectedFontSize", text);
        await Driver.WaitForValue("font-preview", "FontSize", text);
        await Driver.WaitForValue("font-size-label", "Text", $"Size: {text}");
    }

    [Test]
    public async Task Button_Value_Flows_Back_To_The_Inline_Pickers()
    {
        await Open(Route, PageType);
        var current = await Driver.GetProperty("SelectedFont", automationId: "font-picker-button");
        var font = current == "OpenSansRegular" ? "OpenSansSemibold" : "OpenSansRegular";

        await Driver.SetProperty("SelectedFont", font, automationId: "font-picker-button");
        await Driver.SetProperty("SelectedFontSize", "36", automationId: "font-size-picker-button");

        await Driver.WaitForValue("font-picker-inline", "SelectedFont", font);
        await Driver.WaitForValue("font-size-picker-inline", "SelectedFontSize", "36");
        await Driver.WaitForValue("font-preview", "FontSize", "36");
    }

    [Test]
    public async Task Button_Caption_Shows_The_Selected_Family()
    {
        await Open(Route, PageType);
        var current = await Driver.GetProperty("SelectedFont", automationId: "font-picker-inline");
        var font = current == "OpenSansRegular" ? "OpenSansSemibold" : "OpenSansRegular";

        await Driver.SetProperty("SelectedFont", font, automationId: "font-picker-inline");

        await Driver.WaitForCssValue("#font-picker-button Label", "Text", font);
    }
}
