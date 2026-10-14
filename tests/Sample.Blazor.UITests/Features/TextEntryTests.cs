using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>
/// TextEntry on the web: typing binds back to the page, the floating label lifts, the clear tool
/// appears with text and empties the field, the counter tracks length, validation shows its hint,
/// steppers step, and read-only refuses typing.
/// </summary>
public class TextEntryTests : BlazorTest
{
    ILocator Section(string heading)
        => Page.Locator(".demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });

    ILocator Field(string heading, int index = 0)
        => Section(heading).Locator(".shiny-te").Nth(index);

    [Test]
    public async Task Typing_Updates_The_Bound_Value()
    {
        await Open("textentry");
        var basic = Section("Basic");

        await basic.GetByPlaceholder("First Name").FillAsync("Jane");
        await basic.GetByPlaceholder("Last Name").PressSequentiallyAsync("Doe");

        await Expect(basic.Locator(".state")).ToHaveTextAsync("First: Jane | Last: Doe");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Floating_Label_Lifts_On_Focus_And_Stays_With_Text()
    {
        await Open("textentry");
        var field = Field("Floating Label (M3 notched outline)");
        var label = field.Locator(".shiny-te-placeholder");
        var input = field.Locator("input");

        await Expect(label).Not.ToHaveClassAsync(new Regex("shiny-te-placeholder--up"));

        await input.FocusAsync();
        await Expect(label).ToHaveClassAsync(new Regex("shiny-te-placeholder--up"));
        await Expect(field).ToHaveClassAsync(new Regex("shiny-te--focused"));

        await input.PressSequentiallyAsync("me@example.com");
        await input.BlurAsync();
        await Expect(field).Not.ToHaveClassAsync(new Regex("shiny-te--focused"));
        await Expect(label).ToHaveClassAsync(new Regex("shiny-te-placeholder--up"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clear_Button_Appears_With_Text_And_Empties_The_Field()
    {
        await Open("textentry");
        var section = Section("With Clear Button");
        var input = section.GetByPlaceholder("Search");
        var clear = section.Locator(".shiny-te-tools--right .shiny-te-tool-btn");

        await Expect(clear).ToHaveCountAsync(0);

        await input.PressSequentiallyAsync("shiny");
        await Expect(section.Locator(".state")).ToHaveTextAsync("Search: shiny");
        await Expect(clear).ToHaveCountAsync(1);

        await clear.ClickAsync();
        await Expect(input).ToHaveValueAsync("");
        await Expect(section.Locator(".state")).ToHaveTextAsync("Search:");
        await Expect(clear).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Character_Count_Tracks_Length_And_MaxLength_Caps_It()
    {
        await Open("textentry");
        var field = Field("Character Count");
        var input = field.GetByPlaceholder("Bio (max 100)");
        var hint = field.Locator(".shiny-te-hint");

        await Expect(hint).ToHaveTextAsync("0/100");
        await input.PressSequentiallyAsync("Hello");
        await Expect(hint).ToHaveTextAsync("5/100");

        await input.FillAsync(new string('x', 140));
        await Expect(input).ToHaveValueAsync(new string('x', 100));
        await Expect(hint).ToHaveTextAsync("100/100");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Invalid_Email_Shows_Error_Until_Corrected()
    {
        await Open("textentry");
        var field = Field("Validation");
        var input = field.GetByPlaceholder("Email");

        await input.PressSequentiallyAsync("not-an-email");
        await input.BlurAsync();
        await Expect(field).ToHaveClassAsync(new Regex("shiny-te--error"));
        await Expect(field.Locator(".shiny-te-hint")).ToHaveTextAsync("Please enter a valid email address");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Correcting_The_Email_Clears_The_Error()
    {
        await Open("textentry");
        var field = Field("Validation");
        var input = field.GetByPlaceholder("Email");
        await input.PressSequentiallyAsync("not-an-email");
        await input.BlurAsync();
        await Expect(field).ToHaveClassAsync(new Regex("shiny-te--error"));

        await input.FillAsync("someone@example.com");
        await input.BlurAsync();

        await Expect(field).Not.ToHaveClassAsync(new Regex("shiny-te--error"));
        await Expect(field.Locator(".shiny-te-hint")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Stepper_Tools_Step_By_Their_Amount()
    {
        await Open("textentry");

        var quantity = Section("Stepper Tool");
        await quantity.GetByRole(AriaRole.Button, new() { Name = "+1" }).ClickAsync();
        await quantity.GetByRole(AriaRole.Button, new() { Name = "+1" }).ClickAsync();
        await Expect(quantity.Locator(".state")).ToHaveTextAsync("Quantity: 2");
        await quantity.GetByRole(AriaRole.Button, new() { Name = "-1" }).ClickAsync();
        await Expect(quantity.Locator(".state")).ToHaveTextAsync("Quantity: 1");
        await Expect(quantity.GetByPlaceholder("Quantity")).ToHaveValueAsync("1");

        var score = Section("Stepper Tool (Step 5)");
        await score.GetByRole(AriaRole.Button, new() { Name = "-5" }).ClickAsync();
        await Expect(score.Locator(".state")).ToHaveTextAsync("Score: 45");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task ReadOnly_Field_Refuses_Typing()
    {
        await Open("textentry");
        var input = Field("Read Only").Locator("input");

        await Expect(input).ToHaveValueAsync("This field is read only");
        await Expect(input).Not.ToBeEditableAsync();

        await input.ClickAsync();
        await Page.Keyboard.TypeAsync("xyz");
        await Expect(input).ToHaveValueAsync("This field is read only");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Password_Field_Masks_Input()
    {
        await Open("textentry");
        var input = Section("Password").GetByPlaceholder("Password");

        await Expect(input).ToHaveAttributeAsync("type", "password");
        await input.PressSequentiallyAsync("hunter2");
        await Expect(input).ToHaveValueAsync("hunter2");
        await Expect(Section("Password").Locator(".shiny-te-tools--right .shiny-te-tool-btn")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }
}
