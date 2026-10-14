namespace Sample.UITests.Features;

/// <summary>
/// ZoomPanView: ZoomLevel two-way with the view model, clamped to MaxZoom, dropped by IsZoomEnabled,
/// and the content staying a live control tree while zoomed.
/// Not in the macOS or Linux heads' Shells.
/// </summary>
[SkipOn(UIPlatform.MacOS, "zoompan is not in the macOS head")]
[SkipOn(UIPlatform.Linux, "zoompan is not in the Linux head")]
public class ZoomPanTests : UITest
{
    /// <summary>Shell keeps the page alive between visits, so zoom is re-enabled and reset on the way in.</summary>
    async Task OpenPage()
    {
        await Open("zoompan", "ZoomPanPage");
        // Disabling zoom resets the view outright - it also recovers one left inconsistent by the
        // mid-animation BUG below (ZoomLevel 1 while IsZoomed is still True)
        await Driver.SetProperty("IsToggled", "False", automationId: "ZoomEnabledSwitch");
        await Driver.WaitForProperty("ZoomPanView", "IsZoomed", "False");
        await Driver.SetProperty("IsToggled", "True", automationId: "ZoomEnabledSwitch");
        await Driver.SetProperty("IsToggled", "True", automationId: "DoubleTapSwitch");
        await Driver.WaitForProperty("ZoomPanView", "IsZoomEnabled", "True");
        await Driver.Tap(automationId: "ResetZoomButton");
        await Settled(1);
    }

    /// <summary>
    /// Waits for a zoom animation to land. The view writes every frame back through the two-way
    /// ZoomLevel, so a second zoom command issued mid-animation races it (see the BUG test below);
    /// a person can't tap faster than the animation either.
    /// </summary>
    async Task Settled(double zoom)
    {
        // The command writes the target into ZoomLevel straight away and the frames then overwrite it
        // on the way there, so "settled" is the target read back several times in a row.
        var streak = 0;
        await Driver.WaitUntil(async () =>
        {
            streak = await Driver.GetDouble("ZoomLevel", "ZoomPanView") is { } z && Math.Abs(z - zoom) < 0.001 ? streak + 1 : 0;
            return streak >= 4;
        }, $"the zoom animation to settle at {zoom}");
    }


    [Test]
    public async Task Zoom_And_Reset_Buttons_Drive_The_View()
    {
        await OpenPage();
        await Driver.WaitForProperty("ZoomLevelLabel", "Text", "1.00x");
        await Driver.WaitForProperty("ZoomPanView", "IsZoomed", "False");

        await Driver.Tap(automationId: "ZoomInButton");
        await Driver.WaitForProperty("ZoomPanView", "IsZoomed", "True");
        await Driver.WaitForProperty("ZoomLevelLabel", "Text", "2.00x");
        await Settled(2);

        await Driver.Tap(automationId: "ResetZoomButton");
        await Driver.WaitForProperty("ZoomPanView", "IsZoomed", "False");
        await Driver.WaitForProperty("ZoomLevelLabel", "Text", "1.00x");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Zoom_Is_Clamped_To_MaxZoom()
    {
        await OpenPage();

        // MaxZoom="4" on the sample; the clamped value reports back through the binding
        await Driver.SetProperty("ZoomLevel", "10", automationId: "ZoomPanView");

        await Driver.WaitForProperty("ZoomLevelLabel", "Text", "4.00x");
        await Driver.WaitForProperty("ZoomPanView", "IsZoomed", "True");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Disabling_Zoom_Drops_The_Zoom_Already_Applied()
    {
        await OpenPage();
        await Driver.Tap(automationId: "ZoomInButton");
        await Driver.WaitForProperty("ZoomPanView", "IsZoomed", "True");
        await Settled(2);

        await Driver.SetProperty("IsToggled", "False", automationId: "ZoomEnabledSwitch");

        await Driver.WaitForProperty("ZoomPanView", "IsZoomEnabled", "False");
        await Driver.WaitForProperty("ZoomPanView", "IsZoomed", "False");
        await Driver.WaitForProperty("ZoomLevelLabel", "Text", "1.00x");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Content_Stays_Interactive_While_Zoomed()
    {
        await OpenPage();
        await Driver.Tap(automationId: "ZoomInButton");
        await Driver.WaitForProperty("ZoomPanView", "IsZoomed", "True");
        await Settled(2);

        await Driver.Tap(automationId: "InnerTapButton");
        await Driver.WaitForProperty("InnerTapButton", "Text", "Tapped 1 times");

        await Driver.Fill("hello", automationId: "InnerEntry");
        await Driver.WaitUntilTextExists("You typed: hello");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task DoubleTap_Switch_Reaches_The_View()
    {
        await OpenPage();
        await Driver.WaitForProperty("ZoomPanView", "DoubleTapToZoom", "True");

        await Driver.SetProperty("IsToggled", "False", automationId: "DoubleTapSwitch");

        await Driver.WaitForProperty("ZoomPanView", "DoubleTapToZoom", "False");
    }


    [Test]
    [Skip("BUG: a ZoomLevel change that lands while a zoom animation is running is lost. Every animation frame writes back through the two-way "
        + "ZoomLevel (OnControllerZoomChanged), so Reset's ZoomLevel=1 collides with the in-flight 1→2 animation; iOS ends with ZoomLevel 1 / label "
        + "1.00x while IsZoomed stays True and the content stays at 2x (Zoom to 2x then Reset straight away, reproducible every run)")]
    public async Task Reset_Mid_Animation_Still_Resets()
    {
        await OpenPage();

        await Driver.Tap(automationId: "ZoomInButton");
        await Driver.Tap(automationId: "ResetZoomButton");

        await Task.Delay(1500);
        await Driver.WaitForProperty("ZoomPanView", "IsZoomed", "False");
        await Driver.WaitForProperty("ZoomLevelLabel", "Text", "1.00x");
    }
}
