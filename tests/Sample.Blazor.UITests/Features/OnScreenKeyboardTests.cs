namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The Blazor on-screen keyboard: auto-shows when a field takes focus, types through the caret
/// (the field keeps focus), has momentary Shift and a symbols layer, and is driven by
/// IOnScreenKeyboardService from code.
/// </summary>
public class OnScreenKeyboardTests : BlazorTest
{
    ILocator Keyboard => Page.Locator(".shiny-osk");
    ILocator PlainInput => Page.GetByPlaceholder("A plain input");
    ILocator BoundValues => Page.Locator("p", new() { HasText = "Bound values:" });

    ILocator Key(string name) => Keyboard.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    async Task FocusPlainInput()
    {
        await Open("onscreenkeyboard");
        await PlainInput.ClickAsync();
        await Expect(Keyboard).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-closed"));
    }


    [Test]
    public async Task Focusing_A_Field_Shows_The_Keyboard()
    {
        await Open("onscreenkeyboard");
        await Expect(Keyboard).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-closed"));
        await Expect(Page.Locator(".shiny-pill", new() { HasText = "Hidden" })).ToBeVisibleAsync();

        await PlainInput.ClickAsync();

        await Expect(Keyboard).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-closed"));
        await Expect(Keyboard).Not.ToHaveAttributeAsync("aria-hidden", "true");
        await Expect(Page.Locator(".shiny-pill", new() { HasText = "Visible" })).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Keys_Type_Into_The_Focused_Field_Without_Taking_Focus()
    {
        await FocusPlainInput();

        await Key("h").ClickAsync();
        await Key("i").ClickAsync();

        await Expect(PlainInput).ToHaveValueAsync("hi");
        await Expect(PlainInput).ToBeFocusedAsync();
        await Expect(BoundValues.Locator("code").First).ToHaveTextAsync("hi");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Shift_Is_Momentary()
    {
        await FocusPlainInput();

        await Key("Shift").First.ClickAsync();
        await Key("A").ClickAsync();
        await Key("b").ClickAsync();

        await Expect(PlainInput).ToHaveValueAsync("Ab");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Backspace_And_Space_Edit_Through_The_Caret()
    {
        await FocusPlainInput();

        foreach (var k in new[] { "a", "b", "c" })
            await Key(k).ClickAsync();
        await Key("Backspace").ClickAsync();
        await Key("Space").ClickAsync();
        await Key("d").ClickAsync();

        await Expect(PlainInput).ToHaveValueAsync("ab d");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Numbers_Layer_Swaps_The_Letters_Out()
    {
        await FocusPlainInput();
        await Expect(Key("q")).ToBeVisibleAsync();

        await Key("Numbers and symbols").ClickAsync();

        await Expect(Key("q")).ToHaveCountAsync(0);
        await Expect(Key("Letters")).ToHaveAttributeAsync("aria-pressed", "true");
        await Key("?").ClickAsync();
        await Expect(PlainInput).ToHaveValueAsync("?");

        await Key("Letters").ClickAsync();
        await Expect(Key("q")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Hide_Key_Closes_The_Keyboard()
    {
        await FocusPlainInput();

        await Key("Hide keyboard").ClickAsync();

        await Expect(Keyboard).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-closed"));
        await Expect(Page.Locator(".shiny-pill", new() { HasText = "Hidden" })).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Service_Shows_And_Hides_From_Code()
    {
        await Open("onscreenkeyboard");

        // Clicking a button moves focus off any field, so auto-hide-on-blur has nothing to fight here.
        await Page.GetByRole(AriaRole.Button, new() { Name = "Show", Exact = true }).ClickAsync();
        await Expect(Keyboard).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-closed"));
        await Expect(Page.Locator(".shiny-pill", new() { HasText = "Visible" })).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Hide", Exact = true }).ClickAsync();
        await Expect(Keyboard).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-closed"));

        await Page.GetByRole(AriaRole.Button, new() { Name = "Toggle", Exact = true }).ClickAsync();
        await Expect(Keyboard).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-closed"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Auto_Show_Can_Be_Turned_Off()
    {
        await Open("onscreenkeyboard");

        await Page.GetByLabel("Auto-show on focus").UncheckAsync();
        await PlainInput.ClickAsync();
        await Page.WaitForTimeoutAsync(400);

        await Expect(Keyboard).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-closed"));
        await AssertNoConsoleErrors();
    }
}
