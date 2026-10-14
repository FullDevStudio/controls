namespace Sample.UITests.Features;

/// <summary>
/// Quick Entry: the presentation setting and what it resolves to, the in-app popup's open/close
/// lifecycle reported through Opened/Closed, and the global-hotkey affordance being honest about
/// platforms that have none.
/// </summary>
public class QuickEntryTests : UITest
{
    Task OpenPage() => Open("quickentry", "QuickEntryPage");

    /// <summary>The page logs every event to a CollectionView as "HH:mm:ss  message".</summary>
    Task WaitForLog(string message, int timeoutSeconds = 10)
        => Driver.WaitUntil(async () => (await Driver.Query(type: "Label", text: message)).Count > 0, $"'{message}' in the event log", timeoutSeconds);

    /// <summary>InApp is the one presentation every head supports, so the popup tests pin it.</summary>
    async Task UseInAppPresentation()
    {
        await Driver.SetProperty("SelectedIndex", "1", automationId: "PresentationPicker");
        await Driver.WaitForProperty("PresentationPicker", "SelectedItem", "InApp");
        await WaitForLog("Presentation = InApp (resolves to InApp)");
    }


    [Test]
    public async Task Presentation_Picker_Resolves_Explicit_Choices()
    {
        await OpenPage();

        await UseInAppPresentation();

        await Driver.SetProperty("SelectedIndex", "0", automationId: "PresentationPicker");
        await Driver.WaitForProperty("PresentationPicker", "SelectedItem", "Auto");
        // Auto picks per platform, but it always resolves to a concrete presentation
        await Driver.WaitUntil(async () =>
            (await Driver.Query(type: "Label", text: "Presentation = Auto (resolves to ")).Count > 0
                && (await Driver.Query(type: "Label", text: "resolves to Auto)")).Count == 0,
            "Auto to resolve to InApp or Desktop");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task InApp_Popup_Show_And_Hide_Raise_Opened_And_Closed()
    {
        await OpenPage();
        await UseInAppPresentation();

        await Driver.Tap(automationId: "ShowPopup");
        await WaitForLog("Popup opened");

        await Driver.Tap(automationId: "HidePopup");
        await WaitForLog("Popup closed");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task InApp_Popup_Toggle_Opens_Then_Closes()
    {
        await OpenPage();
        await UseInAppPresentation();
        var closedBefore = (await Driver.Query(type: "Label", text: "Popup closed")).Count;

        await Driver.Tap(automationId: "TogglePopup");
        await WaitForLog("Popup opened");

        await Driver.Tap(automationId: "TogglePopup");
        await Driver.WaitUntil(async () => (await Driver.Query(type: "Label", text: "Popup closed")).Count > closedBefore, "a second toggle to close the popup");
        await AssertNoAppErrors();
    }


    [Test, OnlyOn(UIPlatform.iOS, UIPlatform.Android)]
    public async Task Global_Hotkeys_Are_Unavailable_On_Mobile()
    {
        await OpenPage();

        await Driver.WaitForProperty("RegisterHotKey", "IsEnabled", "False");
        await Assert.That(await Driver.GetProperty("Text", automationId: "HotKeyStatus")).Contains("need the desktop add-on");
    }


    [Test, OnlyOn(UIPlatform.MacOS, UIPlatform.Windows)]
    public async Task Global_Hotkey_Registers_On_Desktop()
    {
        await OpenPage();
        await Driver.WaitForProperty("RegisterHotKey", "IsEnabled", "True");

        // an unusual chord, so another app is unlikely to own it already
        await Driver.SetProperty("Text", Platform == UIPlatform.MacOS ? "Cmd+Opt+Shift+F9" : "Ctrl+Alt+Shift+F9", automationId: "HotKeyEntry");
        await Driver.Tap(automationId: "RegisterHotKey");

        await Driver.WaitUntil(async () =>
            (await Driver.GetProperty("Text", automationId: "HotKeyStatus"))?.Contains("is registered") == true,
            "the hotkey to register");
        await AssertNoAppErrors();
    }
}
