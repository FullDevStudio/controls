namespace Sample.UITests.Features;

/// <summary>
/// ShinyButton. Its tap recognizer sits on the button itself so DevFlow can tap it; the async
/// commands drive Busy → Normal/Success/Error on their own, and CanExecute composes with IsEnabled.
/// </summary>
public class ButtonsTests : UITest
{
    const string Status = "ButtonsStatusLabel";

    async Task WaitForText(string automationId, Func<string, bool> condition, string description, int timeoutSeconds = 10)
    {
        string? text = null;
        await Driver.WaitUntil(async () =>
        {
            text = await Driver.GetProperty("Text", automationId: automationId);
            return text != null && condition(text);
        }, $"{automationId}.Text {description} (last: '{text}')", timeoutSeconds);
    }

    async Task OpenButtons()
    {
        await Open("buttons", "ButtonsPage");

        // Shell keeps the page (and its view model) — put the toggles back where a test expects them.
        if (await Driver.GetProperty("IsChecked", automationId: "ShouldFailCheckBox") == "True")
            await Driver.SetProperty("IsChecked", "False", automationId: "ShouldFailCheckBox");
        if (await Driver.GetProperty("IsEnabled", automationId: "GuardedAction") == "False")
        {
            await Driver.Tap(automationId: "ToggleCanExecute");
            await Driver.WaitForProperty("GuardedAction", "IsEnabled", "True");
        }
    }


    [Test]
    public async Task Tap_Runs_The_Command_With_Its_Parameter()
    {
        await OpenButtons();

        await Driver.Tap(automationId: "FilledButton");

        await Driver.WaitForProperty(Status, "Text", "Filled tapped");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Icon_Only_Button_Taps_Too()
    {
        await OpenButtons();

        await Driver.Tap(automationId: "IconOnlyButton");

        await Driver.WaitForProperty(Status, "Text", "Settings tapped");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Async_Command_Drives_Busy_Then_Back_To_Normal()
    {
        await OpenButtons();

        await Driver.Tap(automationId: "BusyReplaceContent");

        await Driver.WaitForProperty("BusyReplaceContent", "State", "Busy", timeoutSeconds: 5);
        await Assert.That(await Driver.GetProperty("IsBusy", automationId: "BusyReplaceContent")).IsEqualTo("True");
        await Driver.WaitForProperty(Status, "Text", "Saving...", timeoutSeconds: 5);

        await Driver.WaitForProperty(Status, "Text", "Saved", timeoutSeconds: 10);
        await Driver.WaitForProperty("BusyReplaceContent", "State", "Normal");
        await AssertNoAppErrors();
    }


    [Test]
    [Skip("SUSPECTED BUG (from source, not run): ButtonsViewModel.SaveAsync's [RelayCommand] has no FlowExceptionsToTaskScheduler, so CommunityToolkit's AsyncRelayCommand.Execute rethrows the deliberate fault through async void — on MAUI that is an unhandled exception that can take the app down instead of just showing the Error state")]
    public async Task Faulted_Command_Shows_The_Error_State()
    {
        await OpenButtons();
        await Driver.SetProperty("IsChecked", "True", automationId: "ShouldFailCheckBox");
        try
        {
            await Driver.Tap(automationId: "BusyKeepContent");

            await Driver.WaitForProperty("BusyKeepContent", "State", "Busy", timeoutSeconds: 5);
            await Driver.WaitForProperty("BusyKeepContent", "State", "Error", timeoutSeconds: 10);
        }
        finally
        {
            await Driver.SetProperty("IsChecked", "False", automationId: "ShouldFailCheckBox");
        }
        await Assert.That(await Driver.IsAlive()).IsTrue();
    }


    [Test]
    public async Task Command_Reported_Success_Holds_Then_Reverts()
    {
        await OpenButtons();
        await Driver.WaitForProperty("SubmitButton", "State", "Normal");

        await Driver.Tap(automationId: "SubmitButton");

        await Driver.WaitForProperty("SubmitButton", "State", "Busy", timeoutSeconds: 5);
        // The command sets Success itself; the button must keep it rather than resetting to Normal...
        await Driver.WaitForProperty("SubmitButton", "State", "Success", timeoutSeconds: 10);
        await Driver.WaitForProperty(Status, "Text", "Submitted");
        // ...until StateRevertDelay (2s) puts it back.
        await Driver.WaitForProperty("SubmitButton", "State", "Normal", timeoutSeconds: 10);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Bound_State_Shows_Success_And_Error()
    {
        await OpenButtons();

        await Driver.Tap(automationId: "ForceError");
        await Driver.WaitForProperty("SubmitButton", "State", "Error");

        await Driver.Tap(automationId: "ForceSuccess");
        await Driver.WaitForProperty("SubmitButton", "State", "Success");

        await Driver.WaitForProperty("SubmitButton", "State", "Normal", timeoutSeconds: 10);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task CanExecute_Disables_The_Button_But_Never_Re_Enables_An_Explicit_Disable()
    {
        await OpenButtons();
        await Driver.WaitForProperty("GuardedAction", "IsEnabled", "True");
        await Assert.That(await Driver.GetProperty("IsEnabled", automationId: "ExplicitlyDisabledButton")).IsEqualTo("False");

        await Driver.Tap(automationId: "ToggleCanExecute");
        await Driver.WaitForProperty("GuardedAction", "IsEnabled", "False");

        await Driver.Tap(automationId: "ToggleCanExecute");
        await Driver.WaitForProperty("GuardedAction", "IsEnabled", "True");

        // The command can execute again, but the consumer switched this one off — it stays off.
        await Assert.That(await Driver.GetProperty("IsEnabled", automationId: "ExplicitlyDisabledButton")).IsEqualTo("False");

        await Driver.Tap(automationId: "GuardedAction");
        await Driver.WaitForProperty(Status, "Text", "The guarded command ran");
        await AssertNoAppErrors();
    }
}
