namespace Sample.UITests.Features;

/// <summary>
/// Keyboard shortcuts on MAUI. DevFlow has no hardware-key injection, so the matching itself is
/// covered by the Blazor suite (and the engine's unit tests); here: that every head has a key source
/// wired, that Primary is displayed per platform, and the modal scope the page opens.
/// </summary>
public class KeyboardShortcutsTests : UITest
{
    Task OpenShortcuts() => Open("keyboardshortcuts", "KeyboardShortcutsPage");


    [Test]
    public async Task A_Key_Source_Is_Wired_On_This_Head()
    {
        await OpenShortcuts();

        // Every head registers one (UseDesktopKeyboardShortcuts on macOS/Linux/Catalyst), so the
        // service never reports itself unsupported.
        var support = await Driver.GetProperty("Text", automationId: "KeyboardShortcutsSupport");
        await Assert.That(support).StartsWith("Listening for keys on ");
    }


    [Test]
    public async Task Primary_Is_Displayed_Per_Platform()
    {
        await OpenShortcuts();

        var hint = await Driver.GetProperty("Text", automationId: "PaletteHintLabel");

        // Primary+Shift+P: ⇧⌘P on Apple platforms, Ctrl+Shift+P everywhere else.
        if (Platform is UIPlatform.iOS or UIPlatform.MacOS)
            await Assert.That(hint).IsEqualTo("⇧⌘P opens the command palette");
        else
            await Assert.That(hint).IsEqualTo("Ctrl+Shift+P opens the command palette");
    }


    [Test]
    public async Task Button_Opens_The_Modal_Scope()
    {
        await OpenShortcuts();

        // The modal only closes with Escape, which DevFlow cannot press; if an earlier run left it
        // open on the cached page there is nothing further to prove here.
        if (await Driver.GetProperty("IsVisible", automationId: "KeyboardShortcutsModal") != "True")
            await Driver.Tap(automationId: "OpenModalButton");

        await Driver.WaitForProperty("KeyboardShortcutsModal", "IsVisible", "True");
        await Driver.WaitUntilTextExists("A modal scope");
        await AssertNoAppErrors();
    }
}
