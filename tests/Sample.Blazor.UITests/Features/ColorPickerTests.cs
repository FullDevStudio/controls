using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>
/// ColorPicker and ColorPickerButton on the web (/colorpicker, also served as /pickers): the hex
/// field, the spectrum and hue canvases all write SelectedColor back to the page, ShowOpacity adds
/// and removes the alpha track, and the button opens the picker in a popover that Done or the
/// backdrop dismisses.
/// </summary>
public class ColorPickerTests : BlazorTest
{
    const string Initial = "#6495ED";

    ILocator InlinePicker => Page.Locator(".demo-section .shiny-colorpicker").Last;

    // the <code> next to the button and the swatch under the inline picker both print the bound value
    ILocator ButtonValue => Page.Locator(".demo-section", new() { Has = Page.Locator(".shiny-colorpickerbutton") }).Locator("code");

    ILocator Trigger => Page.Locator(".shiny-colorpickerbutton-trigger");

    ILocator Popover => Page.Locator(".shiny-colorpickerbutton-popover");

    [Test]
    public async Task Hex_Field_Sets_The_Color()
    {
        await Open("colorpicker");
        await Expect(ButtonValue).ToHaveTextAsync(Initial);

        var hex = InlinePicker.Locator(".shiny-colorpicker-hex");
        await hex.FillAsync("#FF0000");
        await hex.PressAsync("Enter");

        await Expect(ButtonValue).ToHaveTextAsync(new Regex("^#FF0000(FF)?$", RegexOptions.IgnoreCase));
        await Expect(Trigger.Locator(".shiny-colorpickerbutton-fill")).ToHaveAttributeAsync("style", new Regex("#FF0000", RegexOptions.IgnoreCase));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clicking_The_Spectrum_Changes_The_Color()
    {
        await Open("colorpicker");
        var spectrum = InlinePicker.Locator(".shiny-colorpicker-spectrum");
        var box = await spectrum.BoundingBoxAsync();

        // bottom-left of the saturation/brightness field is black whatever the hue. The canvas's
        // pointer listeners are attached by a JS module after first render, so retry until one lands.
        for (var attempt = 0; attempt < 10 && await ButtonValue.TextContentAsync() == Initial; attempt++)
        {
            await Page.Mouse.ClickAsync(box!.X + 3, box.Y + box.Height - 3);
            await Page.WaitForTimeoutAsync(300);
        }

        await Expect(ButtonValue).Not.ToHaveTextAsync(Initial);
        var value = await ButtonValue.TextContentAsync();
        await Assert.That(value).Matches("^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$");
        await Expect(InlinePicker.Locator(".shiny-colorpicker-hex")).ToHaveValueAsync(new Regex("^" + value + "$", RegexOptions.IgnoreCase));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Dragging_The_Hue_Bar_Changes_The_Color()
    {
        await Open("colorpicker");
        var hue = InlinePicker.Locator(".shiny-colorpicker-hue");
        var box = await hue.BoundingBoxAsync();

        // retried for the same reason as the spectrum: the listeners arrive with a JS module
        for (var attempt = 0; attempt < 10 && await ButtonValue.TextContentAsync() == Initial; attempt++)
        {
            await Page.Mouse.MoveAsync(box!.X + box.Width * 0.1f, box.Y + box.Height / 2);
            await Page.Mouse.DownAsync();
            await Page.Mouse.MoveAsync(box.X + box.Width * 0.5f, box.Y + box.Height / 2, new() { Steps = 5 });
            await Page.Mouse.UpAsync();
            await Page.WaitForTimeoutAsync(300);
        }

        await Expect(ButtonValue).Not.ToHaveTextAsync(Initial);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task ShowOpacity_Toggles_The_Alpha_Track()
    {
        await Open("colorpicker");
        var row = InlinePicker.Locator(".shiny-colorpicker-opacity-row");
        var checkbox = Page.GetByLabel("Show Opacity");

        await Expect(row).ToHaveCountAsync(1);
        await checkbox.UncheckAsync();
        await Expect(row).ToHaveCountAsync(0);
        await checkbox.CheckAsync();
        await Expect(row).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Button_Opens_A_Popover_And_Done_Closes_It()
    {
        await Open("colorpicker");
        await Expect(Popover).ToHaveCountAsync(0);

        await Trigger.ClickAsync();
        await Expect(Popover).ToBeVisibleAsync();
        await Expect(Popover.Locator(".shiny-colorpicker-hex")).ToHaveValueAsync(new Regex("^" + Initial + "$", RegexOptions.IgnoreCase));

        var hex = Popover.Locator(".shiny-colorpicker-hex");
        await hex.FillAsync("#00FF00");
        await hex.PressAsync("Enter");
        await Expect(ButtonValue).ToHaveTextAsync(new Regex("^#00FF00(FF)?$", RegexOptions.IgnoreCase));

        await Popover.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();
        await Expect(Popover).ToHaveCountAsync(0);
        await Expect(ButtonValue).ToHaveTextAsync(new Regex("^#00FF00(FF)?$", RegexOptions.IgnoreCase));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Backdrop_Click_Dismisses_The_Popover()
    {
        await Open("pickers");

        await Trigger.ClickAsync();
        await Expect(Popover).ToBeVisibleAsync();

        // a click anywhere outside the panel lands on the full-window backdrop
        var panel = await Popover.BoundingBoxAsync();
        await Page.Mouse.ClickAsync(panel!.X + panel.Width + 200, panel.Y + 20);
        await Expect(Popover).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }
}
