using Microsoft.Maui.DevFlow.Driver;

namespace Sample.UITests.Features;

/// <summary>
/// IToaster on <c>toast</c>. Toasts are ToastViews in the page overlay; their label carries the
/// message, and only labels inside a ToastView count (the agent also walks every cached page).
/// </summary>
public class ToastTests : UITest
{
    const string Status = "ToastStatus";

    async Task OpenPage()
    {
        await Open("toast", "ToastPage");
        // a persistent toast left up by an earlier failure would sit over everything
        if (await ToastShowing("Uploading file..."))
            await Driver.Tap(automationId: "DismissToastButton");
        await Driver.WaitUntil(async () => (await Driver.Query(type: "ToastView")).Count == 0, "no toasts on screen", 15);
    }

    async Task<ElementInfo?> FindToastLabel(string text)
        => (await Driver.QuerySelector($"ToastView Label[Text={MauiDevFlowDriver.Quote(text)}]")).FirstOrDefault();

    async Task<bool> ToastShowing(string text) => await FindToastLabel(text) != null;

    Task WaitForToast(string text, int timeoutSeconds = 10)
        => Driver.WaitUntil(() => ToastShowing(text), $"toast '{text}'", timeoutSeconds);

    Task WaitForToastGone(string text, int timeoutSeconds = 10)
        => Driver.WaitUntil(async () => !await ToastShowing(text), $"toast '{text}' to dismiss", timeoutSeconds);

    [Test]
    [Arguments("InfoToastButton", "New update available", "Info toast shown")]
    [Arguments("SuccessToastButton", "File uploaded successfully", "Success toast shown")]
    [Arguments("WarningToastButton", "Storage almost full", "Warning toast shown")]
    [Arguments("DangerToastButton", "Failed to save changes", "Danger toast shown")]
    [Arguments("CriticalToastButton", "System error - contact support", "Critical toast shown")]
    public async Task Themed_Toast_Shows_Then_Auto_Dismisses(string button, string message, string status)
    {
        await OpenPage();
        await Driver.Tap(automationId: button);

        await WaitForToast(message);
        await Driver.WaitForProperty(Status, "Text", status);

        // the default duration is 3s
        await WaitForToastGone(message, 10);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Persistent_Toast_Stays_Until_Dismissed_By_Code()
    {
        await OpenPage();
        await Driver.Tap(automationId: "PersistentToastButton");
        await WaitForToast("Uploading file...");
        await Driver.WaitForProperty(Status, "Text", "Persistent toast shown (tap Dismiss to close)");

        // Duration zero: still there well past the default 3s
        await Task.Delay(4500);
        await Assert.That(await ToastShowing("Uploading file...")).IsTrue();

        await Driver.Tap(automationId: "DismissToastButton");
        await WaitForToastGone("Uploading file...");
        await Driver.WaitForProperty(Status, "Text", "Manual toast dismissed");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Configured_Toast_Shows_The_Entered_Text()
    {
        await OpenPage();
        var original = await Driver.GetProperty("Text", automationId: "ToastTextEntry");
        try
        {
            await Driver.SetProperty("Text", "Configured from the test", automationId: "ToastTextEntry");
            await Driver.Tap(automationId: "ShowToastButton");

            await WaitForToast("Configured from the test");
            await Driver.WaitForProperty(Status, "Text", "Configured toast shown");
            await WaitForToastGone("Configured from the test", 15);
            await AssertNoAppErrors();
        }
        finally
        {
            if (original != null)
                await Driver.SetProperty("Text", original, automationId: "ToastTextEntry");
        }
    }

    [Test]
    public async Task Duration_Setting_Controls_How_Long_It_Stays()
    {
        await OpenPage();
        var original = await Driver.GetProperty("Value", automationId: "ToastDurationStepper");
        try
        {
            await Driver.SetProperty("Text", "One second toast", automationId: "ToastTextEntry");
            await Driver.SetProperty("Value", "1", automationId: "ToastDurationStepper");
            await Driver.Tap(automationId: "ShowToastButton");

            await WaitForToast("One second toast");
            await WaitForToastGone("One second toast", 5);
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.SetProperty("Value", original ?? "3", automationId: "ToastDurationStepper");
        }
    }

    [Test]
    public async Task Top_Position_Toast_Still_Shows_And_Dismisses()
    {
        await OpenPage();
        try
        {
            await Driver.SetProperty("SelectedIndex", "0", automationId: "ToastPositionPicker"); // Top
            await Driver.SetProperty("Text", "Toast at the top", automationId: "ToastTextEntry");
            await Driver.Tap(automationId: "ShowToastButton");

            await WaitForToast("Toast at the top");
            await WaitForToastGone("Toast at the top", 15);
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.SetProperty("SelectedIndex", "1", automationId: "ToastPositionPicker"); // Bottom, the page default
        }
    }

    [Test]
    public async Task Styled_Toast_Shows()
    {
        await OpenPage();
        await Driver.Tap(automationId: "StyledToastButton");
        await WaitForToast("Custom styled toast");
        await Driver.WaitForProperty(Status, "Text", "Styled toast shown");
        await WaitForToastGone("Custom styled toast");
        await AssertNoAppErrors();
    }
}
