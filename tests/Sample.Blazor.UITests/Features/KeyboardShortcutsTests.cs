namespace Sample.Blazor.UITests.Features;

/// <summary>
/// Keyboard shortcuts, driven with real key presses: page shortcuts, CanExecute, chords, the
/// text-input rule, element scopes, modal blocking, and press/release pairs. Primary is ⌘ where the
/// page reports an Apple platform and Ctrl everywhere else - worked out from the page, not the host,
/// since Playwright's own ControlOrMeta follows the host and a Mac running an emulated Android phone
/// would press ⌘ at a page that expects Ctrl.
/// </summary>
public class KeyboardShortcutsTests : BlazorTest
{
    ILocator Saves => Page.Locator("#kbs-saves");
    ILocator Log => Page.Locator("#kbs-log");

    async Task OpenAndFocusPage()
    {
        await Open("keyboardshortcuts");
        // Shortcuts are matched in the browser once the module has loaded; the platform name in the
        // status line is only filled in after that, so it doubles as a readiness signal.
        await Expect(Page.Locator("#kbs-status strong")).Not.ToHaveTextAsync("");
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Keyboard Shortcuts", Level = 1 }).ClickAsync();
        primary = await Page.EvaluateAsync<bool>(
            "() => { const p = (navigator.userAgentData && navigator.userAgentData.platform) || navigator.platform || ''; return /mac|iphone|ipad|ipod/i.test(p) || /iPhone|iPad|Macintosh/.test(navigator.userAgent); }")
            ? "Meta" : "Control";
    }

    /// <summary>The page's Primary modifier, the same detection keyboard-shortcuts.js does.</summary>
    string primary = "Control";


    [Test]
    public async Task Primary_S_Runs_The_Save_Shortcut()
    {
        await OpenAndFocusPage();
        await Expect(Saves).ToHaveTextAsync("0");

        await Page.Keyboard.PressAsync($"{primary}+s");
        await Page.Keyboard.PressAsync($"{primary}+s");

        await Expect(Saves).ToHaveTextAsync("2");
        await Expect(Log.Locator("li").First).ToContainTextAsync("Saved (2)");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Disabled_Shortcut_Does_Not_Fire()
    {
        await OpenAndFocusPage();

        await Page.Locator("label", new() { HasText = "enabled" }).GetByRole(AriaRole.Checkbox).UncheckAsync();
        await Page.GetByRole(AriaRole.Heading, new() { Name = "Keyboard Shortcuts", Level = 1 }).ClickAsync();
        await Page.Keyboard.PressAsync($"{primary}+s");
        await Page.WaitForTimeoutAsync(300);

        await Expect(Saves).ToHaveTextAsync("0");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Question_Mark_Toggles_The_Cheat_Sheet()
    {
        await OpenAndFocusPage();

        await Page.Keyboard.PressAsync("?");
        var sheet = Page.Locator("#kbs-cheatsheet");
        await Expect(sheet).ToBeVisibleAsync();
        await Expect(sheet).ToContainTextAsync("Command palette");
        await Expect(sheet).ToContainTextAsync("Push to talk (hold)");

        await Page.Keyboard.PressAsync("?");
        await Expect(sheet).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Arrow_Keys_Nudge_The_Box()
    {
        await OpenAndFocusPage();
        var box = Page.Locator("#kbs-box");

        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowRight");
        await Page.Keyboard.PressAsync("ArrowDown");

        await Expect(box).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex(@"left:16px;top:8px"));

        // Clamped at the left edge.
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(box).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex(@"left:0px;top:8px"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Two_Step_Chord_Fires_On_The_Second_Key()
    {
        await OpenAndFocusPage();

        await Page.Keyboard.PressAsync($"{primary}+k");
        await Expect(Page.Locator("#kbs-status")).ToContainTextAsync("waiting for the next key");
        await Expect(Log).Not.ToContainTextAsync("Commented the selection");

        await Page.Keyboard.PressAsync($"{primary}+c");
        await Expect(Log.Locator("li").First).ToContainTextAsync("Commented the selection (chord)");
        await Expect(Page.Locator("#kbs-status")).Not.ToContainTextAsync("waiting for the next key");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Plain_Keys_Stay_With_A_Text_Field_But_Modified_Ones_Still_Fire()
    {
        await OpenAndFocusPage();
        var input = Page.Locator("#kbs-input");
        await input.ClickAsync();

        await Page.Keyboard.TypeAsync("a? b");
        await Expect(input).ToHaveValueAsync("a? b");
        await Expect(Page.Locator("#kbs-cheatsheet")).ToHaveCountAsync(0);
        await Expect(Page.Locator("#kbs-talking")).ToHaveCountAsync(0);

        await Page.Keyboard.PressAsync($"{primary}+s");
        await Expect(Saves).ToHaveTextAsync("1");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Element_Scope_Only_Fires_With_Focus_Inside()
    {
        await OpenAndFocusPage();
        var editor = Page.Locator("#kbs-editor");

        // Outside the box: nothing.
        await Page.Keyboard.PressAsync($"{primary}+b");
        await Page.WaitForTimeoutAsync(300);
        await Expect(Log).Not.ToContainTextAsync("Bold on");

        await editor.ClickAsync();
        await Page.Keyboard.PressAsync($"{primary}+b");

        await Expect(Log.Locator("li").First).ToContainTextAsync("Bold on (element scope)");
        await Expect(editor).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex("font-weight:bold"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Modal_Scope_Blocks_Page_Shortcuts_Until_Escape()
    {
        await OpenAndFocusPage();

        await Page.Keyboard.PressAsync("m");
        var modal = Page.Locator("#kbs-modal");
        await Expect(modal).ToBeVisibleAsync();

        // The page's Save is blocked while the modal is up; the modal's own keys work.
        await Page.Keyboard.PressAsync($"{primary}+s");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Log.Locator("li").First).ToContainTextAsync("Modal's own Enter shortcut");
        await Expect(Saves).ToHaveTextAsync("0");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(modal).ToHaveCountAsync(0);
        await Expect(Log.Locator("li").First).ToContainTextAsync("Modal closed with Escape");

        // The modal's scope is unregistered after the render that removes it, so under load the very
        // next key can still land while it is going. Press until the page's Save is reachable again.
        for (var i = 0; i < 10 && await Saves.InnerTextAsync() == "0"; i++)
        {
            await Page.Keyboard.PressAsync($"{primary}+s");
            await Page.WaitForTimeoutAsync(300);
        }
        await Expect(Saves).Not.ToHaveTextAsync("0");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Holding_Space_Is_Push_To_Talk()
    {
        await OpenAndFocusPage();

        await Page.Keyboard.DownAsync("Space");
        await Expect(Page.Locator("#kbs-talking")).ToBeVisibleAsync();

        await Page.Keyboard.UpAsync("Space");
        await Expect(Page.Locator("#kbs-talking")).ToHaveCountAsync(0);
        await Expect(Log.Locator("li").First).ToContainTextAsync("Push-to-talk: released");
        await AssertNoConsoleErrors();
    }
}
