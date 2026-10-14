namespace Sample.Blazor.UITests.Features;

/// <summary>IDialogService on <c>/dialogs</c>: alert, confirm, prompt and action sheet results reach the page.</summary>
public class DialogsTests : BlazorTest
{
    ILocator Dialog => Page.Locator(".shiny-dialog");
    ILocator Status => Page.Locator("p.status-message");

    Task Show(string button) => Page.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();

    Task Choose(string button) => Dialog.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();

    [Test]
    public async Task Alert_Shows_Title_And_Message_And_Resolves_On_Dismiss()
    {
        await Open("dialogs");
        await Show("Show Alert");

        await Expect(Dialog).ToHaveAttributeAsync("role", "alertdialog");
        await Expect(Dialog.Locator(".shiny-dialog-title")).ToHaveTextAsync("Heads up");
        await Expect(Dialog.Locator(".shiny-dialog-message")).ToHaveTextAsync("Your changes have been saved.");
        await Expect(Status).ToHaveTextAsync("Tap a button to show a dialog");

        await Choose("Got it");
        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync("Alert dismissed");
        await AssertNoConsoleErrors();
    }

    [Test]
    [Arguments("Delete", "Confirmed: item deleted")]
    [Arguments("Cancel", "Cancelled")]
    public async Task Confirm_Returns_The_Choice(string button, string expected)
    {
        await Open("dialogs");
        await Show("Confirm Action");
        await Expect(Dialog.Locator(".shiny-dialog-title")).ToHaveTextAsync("Delete item?");

        await Choose(button);
        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync(expected);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Prompt_Returns_The_Typed_Value()
    {
        await Open("dialogs");
        await Show("Prompt for Name");

        var input = Dialog.Locator("input");
        await Expect(input).ToHaveAttributeAsync("placeholder", "e.g. Allan");
        await input.FillAsync("Grace");
        await Choose("Save");

        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync("Hello, Grace!");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Prompt_Cancel_Reports_Cancelled()
    {
        await Open("dialogs");
        await Show("Prompt for Name");
        await Dialog.Locator("input").FillAsync("ignored");
        await Choose("Skip");

        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync("Prompt cancelled");
        await AssertNoConsoleErrors();
    }

    [Test]
    [Arguments("Choose from Library", "Picked: Choose from Library")]
    [Arguments("Delete Photo", "Picked: Delete Photo")]
    [Arguments("Cancel", "Action sheet cancelled")]
    public async Task Action_Sheet_Returns_The_Picked_Option(string option, string expected)
    {
        await Open("dialogs");
        await Show("Show Action Sheet");
        await Expect(Dialog.Locator(".shiny-dialog-title")).ToHaveTextAsync("Photo");

        await Choose(option);
        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync(expected);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Styled_Dialog_Applies_Per_Call_Colours()
    {
        await Open("dialogs");
        await Show("Styled Confirm");

        await Expect(Dialog).ToHaveCSSAsync("background-color", "rgb(49, 46, 129)");   // #312E81
        await Expect(Dialog).ToHaveCSSAsync("border-top-left-radius", "24px");
        await Expect(Dialog.Locator(".shiny-dialog-title")).ToHaveCSSAsync("color", "rgb(255, 255, 255)");

        await Choose("Nice");
        await Expect(Status).ToHaveTextAsync("Styled dialog dismissed");
        await AssertNoConsoleErrors();
    }
}
