using Microsoft.Maui.DevFlow.Driver;

namespace Sample.UITests.Features;

/// <summary>
/// IDialogService on <c>dialogs</c>. The dialogs are owned MAUI views (no native alert), so their
/// buttons are real elements. They carry no AutomationId, and the agent walks every page Shell has
/// cached, so a button is only accepted when it sits inside the DialogView (a descendant selector —
/// the element endpoint reports no parent, so the chain can't be walked upward).
/// </summary>
public class DialogsTests : UITest
{
    const string Status = "DialogStatus";

    async Task OpenPage()
    {
        await Open("dialogs", "DialogsPage");
        // a dialog left up by an earlier failure would swallow the next one
        await Driver.WaitUntil(async () => (await Driver.Query(type: "DialogView")).Count == 0, "no dialog on screen", 15);
    }

    /// <summary>
    /// Shell keeps the view-model, so the status may already read what a test expects. Run a quick
    /// alert first in that case, so the assertion can only pass on a fresh result.
    /// </summary>
    async Task EnsureStatusIsNot(string expected)
    {
        if (await Driver.GetProperty("Text", automationId: Status) != expected)
            return;

        await Driver.Tap(automationId: "AlertButton");
        await WaitForDialogShown("Your changes have been saved.");
        await TapInDialog("Got it");
        await WaitForDialogGone();
        await Driver.WaitForProperty(Status, "Text", "Alert dismissed");
    }

    async Task<ElementInfo?> FindInDialog(string type, string? text = null)
    {
        var selector = "DialogView " + type + (text == null ? "" : $"[Text={MauiDevFlowDriver.Quote(text)}]");
        return (await Driver.QuerySelector(selector)).FirstOrDefault();
    }

    async Task TapInDialog(string buttonText)
    {
        ElementInfo? button = null;
        await Driver.WaitUntil(async () => (button = await FindInDialog("Button", buttonText)) != null, $"dialog button '{buttonText}'");
        await Driver.Tap(elementId: button!.Id);
    }

    Task WaitForDialogShown(string text)
        => Driver.WaitUntil(async () => await FindInDialog("Label", text) != null, $"dialog text '{text}'");

    Task WaitForDialogGone()
        => Driver.WaitUntil(async () => (await Driver.Query(type: "DialogView")).Count == 0, "the dialog to animate out");

    [Test]
    public async Task Alert_Shows_Its_Message_And_Resolves_On_Dismiss()
    {
        await OpenPage();
        if (await Driver.GetProperty("Text", automationId: Status) == "Alert dismissed")
        {
            await Driver.Tap(automationId: "ConfirmButton");
            await TapInDialog("Cancel");
            await WaitForDialogGone();
            await Driver.WaitForProperty(Status, "Text", "Cancelled");
        }
        await Driver.Tap(automationId: "AlertButton");
        await WaitForDialogShown("Your changes have been saved.");

        await TapInDialog("Got it");
        await WaitForDialogGone();
        await Driver.WaitForProperty(Status, "Text", "Alert dismissed");
        await AssertNoAppErrors();
    }

    [Test]
    [Arguments("Delete", "Confirmed: item deleted")]
    [Arguments("Cancel", "Cancelled")]
    public async Task Confirm_Returns_The_Choice(string button, string expected)
    {
        await OpenPage();
        await EnsureStatusIsNot(expected);
        await Driver.Tap(automationId: "ConfirmButton");
        await WaitForDialogShown("Delete item?");

        await TapInDialog(button);
        await WaitForDialogGone();
        await Driver.WaitForProperty(Status, "Text", expected);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Prompt_Returns_The_Entered_Text()
    {
        await OpenPage();
        await Driver.Tap(automationId: "PromptButton");
        await WaitForDialogShown("What's your name?");

        ElementInfo? entry = null;
        await Driver.WaitUntil(async () => (entry = await FindInDialog("Entry")) != null, "the prompt's entry");
        await Driver.SetProperty("Text", "Grace", elementId: entry!.Id);

        await TapInDialog("Save");
        await WaitForDialogGone();
        await Driver.WaitForProperty(Status, "Text", "Hello, Grace!");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Prompt_Cancel_Discards_The_Text()
    {
        await OpenPage();
        await EnsureStatusIsNot("Prompt cancelled");
        await Driver.Tap(automationId: "PromptButton");
        await WaitForDialogShown("What's your name?");

        await TapInDialog("Skip");
        await WaitForDialogGone();
        await Driver.WaitForProperty(Status, "Text", "Prompt cancelled");
        await AssertNoAppErrors();
    }

    [Test]
    [Arguments("Choose from Library", "Picked: Choose from Library")]
    [Arguments("Delete Photo", "Picked: Delete Photo")]
    [Arguments("Cancel", "Action sheet cancelled")]
    public async Task Action_Sheet_Returns_The_Picked_Option(string option, string expected)
    {
        await OpenPage();
        await EnsureStatusIsNot(expected);
        await Driver.Tap(automationId: "ActionSheetButton");
        await WaitForDialogShown("Photo");

        await TapInDialog(option);
        await WaitForDialogGone();
        await Driver.WaitForProperty(Status, "Text", expected);
        await AssertNoAppErrors();
    }

    [Test]
    [Arguments(6)] // Zoom
    [Arguments(1)] // Fade
    [Arguments(0)] // None
    public async Task Each_Animation_Shows_And_Dismisses(int animationIndex)
    {
        await OpenPage();
        try
        {
            await Driver.SetProperty("SelectedIndex", animationIndex.ToString(), automationId: "DialogAnimationPicker");
            await Driver.WaitForProperty("DialogAnimationPicker", "SelectedIndex", animationIndex.ToString());

            await Driver.Tap(automationId: "AlertButton");
            await WaitForDialogShown("Your changes have been saved.");
            await TapInDialog("Got it");
            await WaitForDialogGone();
            await Driver.WaitForProperty(Status, "Text", "Alert dismissed");
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.SetProperty("SelectedIndex", "7", automationId: "DialogAnimationPicker"); // Pop, the page default
        }
    }

    [Test]
    public async Task Styled_Dialog_Uses_Its_Own_Button_Text()
    {
        await OpenPage();
        await EnsureStatusIsNot("Styled dialog dismissed");
        await Driver.Tap(automationId: "StyledDialogButton");
        await WaitForDialogShown("Custom styled");

        await TapInDialog("Nice");
        await WaitForDialogGone();
        await Driver.WaitForProperty(Status, "Text", "Styled dialog dismissed");
        await AssertNoAppErrors();
    }
}
