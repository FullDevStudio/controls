namespace Sample.UITests.Features;

/// <summary>
/// DockHostView draws its own tab strips, so these drive the page's Buttons/CheckBox and read the
/// status line the page writes from the host's PanelActivated / LayoutChanged events. Shell keeps the
/// page alive between tests, so nothing here assumes the layout is pristine on arrival.
/// </summary>
public class DockingTests : UITest
{
    async Task OpenDocking()
    {
        await Open("docking", "DockingPage");
        await Driver.WaitUntilExists("DockingHost");
    }

    Task<string?> Status() => Driver.GetProperty("Text", automationId: "DockingStatus");

    [Test]
    public async Task Show_Output_Activates_The_Output_Panel()
    {
        await OpenDocking();

        await Driver.Tap("DockingShowOutput");

        await Driver.WaitForProperty("DockingStatus", "Text", "Activated: output");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Show_Explorer_Activates_The_Explorer_Panel()
    {
        await OpenDocking();

        await Driver.Tap("DockingShowExplorer");

        await Driver.WaitForProperty("DockingStatus", "Text", "Activated: solution-explorer");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Reset_Raises_LayoutChanged()
    {
        await OpenDocking();

        await Driver.Tap("DockingReset");

        await Driver.WaitForProperty("DockingStatus", "Text", "Layout changed: reset");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Lock_CheckBox_Locks_The_Host()
    {
        await OpenDocking();

        try
        {
            await Driver.SetProperty("IsChecked", "True", automationId: "DockingLock");
            await Driver.WaitForProperty("DockingHost", "IsLocked", "True");

            await Driver.SetProperty("IsChecked", "False", automationId: "DockingLock");
            await Driver.WaitForProperty("DockingHost", "IsLocked", "False");
        }
        finally
        {
            await Driver.SetProperty("IsChecked", "False", automationId: "DockingLock");
        }
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Panel_Keeps_Its_State_And_Reset_Rebuilds_It()
    {
        await OpenDocking();

        // start from a fresh editor panel: Reset resolves every panel again (they are transient)
        await Driver.Tap("DockingReset");
        await Driver.WaitForProperty("DockEditorCounter", "Text", "Clicks: 0");

        await Driver.Tap("DockEditorClickMe");
        await Driver.Tap("DockEditorClickMe");
        await Driver.WaitForProperty("DockEditorCounter", "Text", "Clicks: 2");

        // activating another panel does not rebuild the editor
        await Driver.Tap("DockingShowOutput");
        await Driver.WaitForProperty("DockingStatus", "Text", "Activated: output");
        await Driver.WaitForProperty("DockEditorCounter", "Text", "Clicks: 2");

        await Driver.Tap("DockingReset");
        await Driver.WaitForProperty("DockEditorCounter", "Text", "Clicks: 0");
        await AssertNoAppErrors();
    }
}
