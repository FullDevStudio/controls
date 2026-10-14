namespace Sample.UITests.Features;

/// <summary>
/// The window-level file drop page. A real OS drag can't be synthesized through the agent, so this
/// covers the unsupported notice on phones and the live filter/log on desktop heads that ship the page.
/// </summary>
public class FileDropTests : UITest
{
    [Test, OnlyOn(UIPlatform.iOS, UIPlatform.Android)]
    public async Task FileDrop_Explains_It_Is_Unsupported_On_Mobile()
    {
        await Open("filedrop", "FileDropPage");

        await Driver.WaitForProperty("FileDropUnsupported", "IsVisible", "True");
        await Assert.That(await Driver.GetProperty("Text", automationId: "FileDropUnsupported")).Contains("desktop window manager");
    }


    [Test, OnlyOn(UIPlatform.MacOS, UIPlatform.Windows)]
    public async Task FileDrop_Filter_Toggle_Is_Logged_And_Clear_Empties_The_Log()
    {
        // (the Linux head does not ship the filedrop page)
        await Open("filedrop", "FileDropPage");
        // The label's own IsVisible is always True - the Border around it is what the page hides when
        // the service is supported, so check what is actually on screen.
        await Driver.WaitUntil(async () => await Driver.IsDisplayed("FileDropStatus") && !await Driver.IsDisplayed("FileDropUnsupported"), "the supported-platform status panel");
        await Driver.Tap(automationId: "FileDropClearButton");

        await Driver.SetProperty("IsToggled", "True", automationId: "FileDropImagesOnly");
        await Driver.WaitUntilTextExists("filter — images only");

        await Driver.SetProperty("IsToggled", "False", automationId: "FileDropImagesOnly");
        await Driver.WaitUntilTextExists("filter — all files");

        await Driver.Tap(automationId: "FileDropClearButton");
        await Driver.WaitUntil(async () => (await Driver.Query(text: "filter — ")).Count == 0, "Clear to empty the event log");
        await AssertNoAppErrors();
    }
}
