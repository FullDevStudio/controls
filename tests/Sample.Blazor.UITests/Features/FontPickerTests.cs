namespace Sample.Blazor.UITests.Features;

/// <summary>
/// FontPicker / FontSizePicker and their popup buttons on the web (/fontpicker): choosing a row
/// selects it (aria-selected) and binds through the page to the other pickers and the preview; the
/// font button's popover closes on pick; the size button accepts a typed size.
/// </summary>
public class FontPickerTests : BlazorTest
{
    ILocator Summary => Page.GetByText(new System.Text.RegularExpressions.Regex(@"^Family: .* · Size: "));

    ILocator FontList => Page.Locator(".demo-section .shiny-fontpicker").Last;

    ILocator SizeList => Page.Locator(".demo-section .shiny-fontsizepicker").Last;

    ILocator FontRow(ILocator list, string font)
        => list.GetByRole(AriaRole.Option).Filter(new() { Has = Page.Locator(".shiny-fontpicker-name", new() { HasTextString = font }) });

    ILocator SizeRow(ILocator list, string size)
        => list.GetByRole(AriaRole.Option).Filter(new() { Has = Page.Locator(".shiny-fontsizepicker-value", new() { HasTextRegex = new System.Text.RegularExpressions.Regex($"^{size}$") }) });

    [Test]
    public async Task Choosing_A_Family_Row_Selects_It_Everywhere()
    {
        await Open("fontpicker");
        await Expect(Summary).ToHaveTextAsync("Family: Georgia · Size: 20");

        await FontRow(FontList, "Verdana").ClickAsync();

        await Expect(Summary).ToHaveTextAsync("Family: Verdana · Size: 20");
        await Expect(FontRow(FontList, "Verdana")).ToHaveAttributeAsync("aria-selected", "");
        await Expect(FontRow(FontList, "Georgia")).Not.ToHaveAttributeAsync("aria-selected", "");
        await Expect(Page.Locator(".shiny-fontpickerbutton-label")).ToHaveTextAsync("Verdana");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Choosing_A_Size_Row_Selects_It_Everywhere()
    {
        await Open("fontpicker");

        await SizeRow(SizeList, "36").ClickAsync();

        await Expect(Summary).ToHaveTextAsync("Family: Georgia · Size: 36");
        await Expect(SizeRow(SizeList, "36")).ToHaveAttributeAsync("aria-selected", "");
        await Expect(Page.GetByLabel("Font size", new() { Exact = true }).And(Page.Locator("input"))).ToHaveValueAsync("36");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Font_Button_Popover_Closes_On_Pick()
    {
        await Open("fontpicker");
        var popover = Page.Locator(".shiny-fontpickerbutton-popover");

        await Page.Locator(".shiny-fontpickerbutton-trigger").ClickAsync();
        await Expect(popover).ToBeVisibleAsync();
        await Expect(FontRow(popover, "Georgia")).ToHaveAttributeAsync("aria-selected", "");

        await FontRow(popover, "Courier New").ClickAsync();

        await Expect(popover).ToHaveCountAsync(0);
        await Expect(Page.Locator(".shiny-fontpickerbutton-label")).ToHaveTextAsync("Courier New");
        await Expect(Summary).ToHaveTextAsync("Family: Courier New · Size: 20");
        await Expect(FontRow(FontList, "Courier New")).ToHaveAttributeAsync("aria-selected", "");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Typed_Size_Commits_On_Enter()
    {
        await Open("fontpicker");
        var input = Page.Locator(".shiny-fontsizepickerbutton-input");

        await input.FillAsync("28");
        await input.PressAsync("Enter");

        await Expect(Summary).ToHaveTextAsync("Family: Georgia · Size: 28");
        await Expect(SizeRow(SizeList, "28")).ToHaveAttributeAsync("aria-selected", "");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Size_Button_Popover_Picks_A_Size()
    {
        await Open("fontpicker");
        var popover = Page.Locator(".shiny-fontsizepickerbutton-popover");

        await Page.Locator(".shiny-fontsizepickerbutton-trigger").ClickAsync();
        await Expect(popover).ToBeVisibleAsync();
        await SizeRow(popover, "48").ClickAsync();

        await Expect(popover).ToHaveCountAsync(0);
        await Expect(Summary).ToHaveTextAsync("Family: Georgia · Size: 48");
        await Expect(Page.Locator(".shiny-fontsizepickerbutton-input")).ToHaveValueAsync("48");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Toolbar_Pickers_On_The_Pickers_Page_Drive_The_Preview()
    {
        await Open("pickers");

        await Page.Locator(".shiny-fontpickerbutton-trigger").ClickAsync();
        await FontRow(Page.Locator(".shiny-fontpickerbutton-popover"), "Arial").ClickAsync();
        var input = Page.Locator(".shiny-fontsizepickerbutton-input");
        await input.FillAsync("32");
        await input.PressAsync("Enter");

        var preview = Page.GetByText("The quick brown fox jumps over the lazy dog");
        await Expect(preview).ToHaveCSSAsync("font-family", new System.Text.RegularExpressions.Regex("Arial"));
        await Expect(preview).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex(@"font-size:\s*32pt"));
        await AssertNoConsoleErrors();
    }
}
