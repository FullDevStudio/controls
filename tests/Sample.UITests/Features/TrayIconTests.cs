namespace Sample.UITests.Features;

/// <summary>
/// The system tray icon page. A tray is a desktop feature, so on a phone the page has to say so
/// instead of offering dead controls; on desktop the icon is started, updated and disposed.
/// </summary>
public class TrayIconTests : UITest
{
    [Test, OnlyOn(UIPlatform.iOS, UIPlatform.Android)]
    public async Task TrayIcon_Explains_It_Is_Unsupported_On_Mobile()
    {
        await Open("trayicon", "TrayIconPage");

        await Driver.WaitForProperty("TrayUnsupportedLabel", "IsVisible", "True");
        await Assert.That(await Driver.GetProperty("Text", automationId: "TrayUnsupportedLabel")).Contains("only available on Windows");
    }


    [Test, OnlyOn(UIPlatform.MacOS, UIPlatform.Linux, UIPlatform.Windows)]
    public async Task TrayIcon_Start_Increment_Stop()
    {
        await Open("trayicon", "TrayIconPage");

        // Shell keeps the page alive, so a previous visit may have left an icon running
        if (await Driver.GetProperty("IsEnabled", automationId: "TrayStopButton") == "True")
            await Driver.Tap(automationId: "TrayStopButton");
        await Driver.WaitForProperty("TrayStopButton", "IsEnabled", "False");
        await Driver.WaitForProperty("TrayStartButton", "IsEnabled", "True");

        try
        {
            await Driver.Tap(automationId: "TrayStartButton");
            await Driver.WaitUntil(async () =>
                (await Driver.GetProperty("Text", automationId: "TrayStatusLabel"))?.StartsWith("Running") == true,
                "the tray icon to start");
            await Driver.WaitForProperty("TrayStartButton", "IsEnabled", "False");
            await Driver.WaitForProperty("TrayStopButton", "IsEnabled", "True");

            var before = await Driver.GetProperty("Text", automationId: "TrayCounterLabel");
            var count = Int32.Parse(before!.Split(' ')[1]);
            await Driver.Tap(automationId: "TrayIncrementButton");
            await Driver.WaitForProperty("TrayCounterLabel", "Text", $"Counter: {count + 1} (also updates tooltip)");
            await Driver.WaitUntilTextExists($"Counter → {count + 1}");
        }
        finally
        {
            // the icon outlives the page, so never leave one behind in the menu bar
            if (await Driver.GetProperty("IsEnabled", automationId: "TrayStopButton") == "True")
                await Driver.Tap(automationId: "TrayStopButton");
        }

        await Driver.WaitForProperty("TrayStatusLabel", "Text", "Stopped");
        await Driver.WaitForProperty("TrayStartButton", "IsEnabled", "True");
        await AssertNoAppErrors();
    }
}
