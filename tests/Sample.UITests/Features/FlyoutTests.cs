namespace Sample.UITests.Features;

/// <summary>
/// ShinyFlyoutPage on <c>flyout</c>: the start panel's states and presentation, the end inspector,
/// and the declare-once drawer on <c>flyoutdrawer</c>. Shell caches the page, so every test sets the
/// state it starts from instead of assuming a fresh one.
/// </summary>
[SkipOn(UIPlatform.MacOS, "flyout pages are not in the macOS head")]
[SkipOn(UIPlatform.Linux, "flyout pages are not in the Linux head")]
public class FlyoutTests : UITest
{
    const string Panel = "FlyoutStartPanel";
    const string Inspector = "FlyoutInspector";
    const string Status = "FlyoutStatus";

    Task OpenPage() => Open("flyout", "FlyoutDemoPage");

    async Task WaitForStatus(string contains)
        => await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: Status))?.Contains(contains, StringComparison.Ordinal) == true,
            $"flyout status to contain '{contains}'"
        );

    [Test]
    public async Task State_Buttons_Move_The_Start_Panel_And_The_Status_Follows()
    {
        await OpenPage();

        await Driver.Tap(automationId: "FlyoutStartExpanded");
        await Driver.WaitForProperty(Panel, "State", "Expanded");
        await WaitForStatus("Start: Expanded");

        await Driver.Tap(automationId: "FlyoutStartRail");
        await Driver.WaitForProperty(Panel, "State", "Collapsed");
        await WaitForStatus("Start: Collapsed");

        await Driver.Tap(automationId: "FlyoutStartHidden");
        await Driver.WaitForProperty(Panel, "State", "Hidden");
        await WaitForStatus("Start: Hidden");

        await AssertNoAppErrors();
    }

    [Test]
    public async Task Setting_State_Directly_Raises_StateChanged()
    {
        await OpenPage();
        await Driver.Tap(automationId: "FlyoutStartRail");
        await Driver.WaitForProperty(Panel, "State", "Collapsed");

        // what a swipe or a view-model binding would do - the page only learns of it through StateChanged
        await Driver.SetProperty("State", "Expanded", automationId: Panel);
        await WaitForStatus("Start: Expanded");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Presentation_Buttons_Switch_Push_And_Overlay()
    {
        await OpenPage();

        await Driver.Tap(automationId: "FlyoutPresentationPush");
        await Driver.WaitForProperty(Panel, "Presentation", "Push");
        await WaitForStatus("- Push (currently Push)");

        await Driver.Tap(automationId: "FlyoutPresentationOverlay");
        await Driver.WaitForProperty(Panel, "Presentation", "Overlay");
        await WaitForStatus("- Overlay (currently Overlay)");

        // Auto resolves against CompactWidth (700) - it reports what it resolved to
        await Driver.Tap(automationId: "FlyoutPresentationAuto");
        await Driver.WaitForProperty(Panel, "Presentation", "Auto");
        await WaitForStatus("- Auto (currently ");
        var status = await Driver.GetProperty("Text", automationId: Status);
        await Assert.That(status!.Contains("(currently Push)") || status.Contains("(currently Overlay)")).IsTrue();

        await AssertNoAppErrors();
    }

    [Test]
    [Arguments("PushResize", "Resize")]
    [Arguments("PushShift", "Shift")]
    public async Task Push_Mode_Buttons_Set_The_Mode_And_Switch_To_Push(string button, string mode)
    {
        await OpenPage();
        await Driver.Tap(automationId: "FlyoutPresentationOverlay");
        await Driver.WaitForProperty(Panel, "Presentation", "Overlay");

        await Driver.Tap(automationId: button);
        await Driver.WaitForProperty("FlyoutDemoPage", "PushMode", mode);
        await Driver.WaitForProperty(Panel, "Presentation", "Push");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Inspector_Toggles_Between_Hidden_And_Expanded()
    {
        await OpenPage();
        await Driver.SetProperty("State", "Hidden", automationId: Inspector);
        await Driver.WaitForProperty(Inspector, "State", "Hidden");

        await Driver.Tap(automationId: "FlyoutToggleInspector");
        await Driver.WaitForProperty(Inspector, "State", "Expanded");

        // CollapsedState is Hidden, so toggling an open inspector hides it rather than leaving a rail
        await Driver.Tap(automationId: "FlyoutToggleInspector");
        await Driver.WaitForProperty(Inspector, "State", "Hidden");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Drawer_Installed_From_The_Template_Opens_Through_The_Service()
    {
        await Open("flyoutdrawer", "FlyoutDrawerPage");
        await Driver.WaitUntilExists("DrawerPanel");
        if (await Driver.GetProperty("State", automationId: "DrawerPanel") != "Hidden")
            await Driver.SetProperty("State", "Hidden", automationId: "DrawerPanel");
        await Driver.WaitForProperty("DrawerPanel", "State", "Hidden");

        await Driver.Tap(automationId: "DrawerToggle");
        await Driver.WaitForProperty("DrawerPanel", "State", "Expanded");

        // CollapsedState="Hidden": the drawer leaves nothing behind when toggled shut
        await Driver.Tap(automationId: "DrawerToggle");
        await Driver.WaitForProperty("DrawerPanel", "State", "Hidden");
        await AssertNoAppErrors();
    }
}
