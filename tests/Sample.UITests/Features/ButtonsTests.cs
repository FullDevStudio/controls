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

    /// <summary>
    /// Success/Error revert after 1.5-2s. A tap plus the first reads can use most of that on the Android
    /// emulator, so the state was sometimes gone before it was read. Holds it longer for the duration of
    /// <paramref name="body"/> (the revert itself is still asserted where a test cares) and restores it.
    /// </summary>
    async Task WithLongerRevert(string button, Func<Task> body)
    {
        var original = await Driver.GetProperty("StateRevertDelay", automationId: button) ?? "00:00:01.5000000";
        await Driver.SetProperty("StateRevertDelay", "00:00:05", automationId: button);
        try
        {
            await body();
        }
        finally
        {
            await Driver.SetProperty("StateRevertDelay", original, automationId: button);
        }
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

        // The command is busy for 1.8s. Reading State, IsBusy and the status one after another can take
        // longer than that on a slow head (each read is a round trip — on the Android emulator three of them
        // overran the window and the status already said "Saved"), so poll all three together and require
        // each to have been seen during the busy window.
        // Ids are resolved once, before the tap, so each poll is a single round trip.
        var button = (await Driver.Find("BusyReplaceContent")).Id;
        var status = (await Driver.Find(Status)).Id;
        await Driver.Tap(elementId: button);

        bool sawBusy = false, sawIsBusy = false, sawSaving = false;
        await Driver.WaitUntil(async () =>
        {
            sawBusy |= await Driver.GetProperty("State", elementId: button) == "Busy";
            sawIsBusy |= await Driver.GetProperty("IsBusy", elementId: button) == "True";
            sawSaving |= await Driver.GetProperty("Text", elementId: status) == "Saving...";
            return sawBusy && sawIsBusy && sawSaving;
        }, "State=Busy, IsBusy=True and 'Saving...' while the command runs", timeoutSeconds: 5);

        await Driver.WaitForProperty(Status, "Text", "Saved", timeoutSeconds: 10);
        await Driver.WaitForProperty("BusyReplaceContent", "State", "Normal");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Faulted_Command_Shows_The_Error_State()
    {
        await OpenButtons();
        await Driver.SetProperty("IsChecked", "True", automationId: "ShouldFailCheckBox");
        try
        {
            await WithLongerRevert("BusyKeepContent", async () =>
            {
                await Driver.Tap(automationId: "BusyKeepContent");

                await Driver.WaitForProperty("BusyKeepContent", "State", "Busy", timeoutSeconds: 5);
                await Driver.WaitForProperty("BusyKeepContent", "State", "Error", timeoutSeconds: 10);
            });
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

        await WithLongerRevert("SubmitButton", async () =>
        {
            // Busy lasts 1.5s: resolve the id first so each poll is one round trip, and require Busy then
            // Success to both be seen (the emulator's slower reads skipped straight past Busy).
            var button = (await Driver.Find("SubmitButton")).Id;
            await Driver.Tap(elementId: button);

            var sawBusy = false;
            await Driver.WaitUntil(async () =>
            {
                var state = await Driver.GetProperty("State", elementId: button);
                sawBusy |= state == "Busy";
                // The command sets Success itself; the button must keep it rather than resetting to Normal...
                return sawBusy && state == "Success";
            }, "SubmitButton to go Busy and then Success", timeoutSeconds: 10);
            await Driver.WaitForProperty(Status, "Text", "Submitted");
            // ...until StateRevertDelay puts it back.
            await Driver.WaitForProperty("SubmitButton", "State", "Normal", timeoutSeconds: 10);
        });
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Bound_State_Shows_Success_And_Error()
    {
        await OpenButtons();

        await WithLongerRevert("SubmitButton", async () =>
        {
            await Driver.Tap(automationId: "ForceError");
            await Driver.WaitForProperty("SubmitButton", "State", "Error");

            await Driver.Tap(automationId: "ForceSuccess");
            await Driver.WaitForProperty("SubmitButton", "State", "Success");

            await Driver.WaitForProperty("SubmitButton", "State", "Normal", timeoutSeconds: 10);
        });
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
