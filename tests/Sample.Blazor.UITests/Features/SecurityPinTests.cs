namespace Sample.Blazor.UITests.Features;

/// <summary>
/// SecurityPin: a hidden input behind a row of cells. Clicking the cells focuses the input, typing
/// fills the cells, and filling the last one raises Completed.
/// </summary>
public class SecurityPinTests : BlazorTest
{
    ILocator Section(string heading)
        => Page.Locator(".demo-section").Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });

    ILocator DefaultPin => Section("Default (4-digit numeric)");
    ILocator MaskedPin => Section("Six digits, masked, custom colors");
    ILocator LastCompleted => Section("Last completion").Locator("code");


    [Test]
    public async Task Clicking_The_Pin_Focuses_The_First_Cell()
    {
        await Open("securitypin");

        await DefaultPin.Locator(".shiny-pin-hidden").ClickAsync();

        await Expect(DefaultPin.Locator(".shiny-pin-hidden")).ToBeFocusedAsync();
        await Expect(DefaultPin.Locator(".shiny-pin-cell").Nth(0)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-current"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Typing_Fills_Cells_And_Advances_The_Current_Cell()
    {
        await Open("securitypin");

        await DefaultPin.Locator(".shiny-pin-hidden").ClickAsync();
        await Page.Keyboard.TypeAsync("12");

        await Expect(DefaultPin.Locator(".shiny-pin-cell span")).ToHaveTextAsync(["1", "2", "", ""]);
        await Expect(DefaultPin.Locator(".state code")).ToHaveTextAsync("12");
        await Expect(DefaultPin.Locator(".shiny-pin-cell").Nth(2)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-current"));
        await Expect(LastCompleted).ToHaveTextAsync("");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Numeric_Pin_Drops_Non_Digits()
    {
        await Open("securitypin");

        await DefaultPin.Locator(".shiny-pin-hidden").ClickAsync();
        await Page.Keyboard.TypeAsync("1a2b");

        await Expect(DefaultPin.Locator(".state code")).ToHaveTextAsync("12");
        await Expect(DefaultPin.Locator(".shiny-pin-cell span")).ToHaveTextAsync(["1", "2", "", ""]);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Filling_Every_Cell_Raises_Completed_And_Stops_At_Length()
    {
        await Open("securitypin");

        await DefaultPin.Locator(".shiny-pin-hidden").ClickAsync();
        await Page.Keyboard.TypeAsync("123456");

        await Expect(DefaultPin.Locator(".state code")).ToHaveTextAsync("1234");
        await Expect(LastCompleted).ToHaveTextAsync("1234");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Backspace_Removes_The_Last_Digit()
    {
        await Open("securitypin");

        await DefaultPin.Locator(".shiny-pin-hidden").ClickAsync();
        await Page.Keyboard.TypeAsync("987");
        await Page.Keyboard.PressAsync("Backspace");

        await Expect(DefaultPin.Locator(".state code")).ToHaveTextAsync("98");
        await Expect(DefaultPin.Locator(".shiny-pin-cell span")).ToHaveTextAsync(["9", "8", "", ""]);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Masked_Pin_Shows_The_Hide_Character_But_Binds_The_Digits()
    {
        await Open("securitypin");

        await MaskedPin.Locator(".shiny-pin-hidden").ClickAsync();
        await Page.Keyboard.TypeAsync("135790");

        await Expect(MaskedPin.Locator(".shiny-pin-cell span")).ToHaveTextAsync(["●", "●", "●", "●", "●", "●"]);
        await Expect(MaskedPin.Locator(".state code")).ToHaveTextAsync("135790");
        await Expect(LastCompleted).ToHaveTextAsync("135790");

        // The other pin is untouched.
        await Expect(DefaultPin.Locator(".state code")).ToHaveTextAsync("");
        await AssertNoConsoleErrors();
    }
}
