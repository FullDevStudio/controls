namespace Sample.UITests.Features;

/// <summary>
/// DurationPicker: Duration binds two-way and renders through Format (h:mm); the trigger opens a
/// FloatingPanel with hour/minute Pickers whose Done commits (clamped to Min/MaxDuration) and whose
/// Cancel leaves the value alone. Only the constrained picker's panel is ever opened, so the single
/// FloatingPanel on the page is unambiguous.
/// </summary>
public class DurationPickerTests : UITest
{
    const string Route = "durationpicker";
    const string PageType = "DurationPickerPage";

    async Task OpenConstrainedPanel()
    {
        await Driver.TapCss("#ConstrainedDuration Border");
        await Driver.WaitForCssValue("FloatingPanel", "IsOpen", "True");
        await Driver.Css("FloatingPanel Picker", minCount: 2);
    }

    async Task WaitForPanelClosed()
        => await Driver.WaitForCssValue("FloatingPanel", "IsOpen", "False");

    [Test]
    public async Task Setting_Duration_Updates_Display_And_Binding()
    {
        await Open(Route, PageType);

        await Driver.SetProperty("Duration", "01:30:00", automationId: "BasicDuration");

        await Driver.WaitForCssValue("#BasicDuration Label", "Text", "1:30");
        await Driver.WaitForValue("BasicDurationLabel", "Text", "Selected: 01:30:00");

        await Driver.SetProperty("Duration", "10:05:00", automationId: "BasicDuration");
        await Driver.WaitForCssValue("#BasicDuration Label", "Text", "10:05");
        await Driver.WaitForValue("BasicDurationLabel", "Text", "Selected: 10:05:00");
    }

    [Test]
    public async Task Done_Commits_The_Picked_Hours_And_Minutes()
    {
        await Open(Route, PageType);
        await OpenConstrainedPanel();

        // hours 0..8 (MaxDuration 8h), minutes in steps of the default 5-minute interval: index 6 = 30
        await Driver.SetCss("FloatingPanel Picker", "SelectedIndex", "2", index: 0);
        await Driver.SetCss("FloatingPanel Picker", "SelectedIndex", "6", index: 1);
        await Driver.TapCss("FloatingPanel Button[text='Done']");

        await Driver.WaitForValue("ConstrainedDuration", "Duration", "02:30:00");
        await Driver.WaitForCssValue("#ConstrainedDuration Label", "Text", "2:30");
        await WaitForPanelClosed();
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Done_Clamps_To_MinDuration()
    {
        await Open(Route, PageType);
        await OpenConstrainedPanel();

        await Driver.SetCss("FloatingPanel Picker", "SelectedIndex", "0", index: 0);
        await Driver.SetCss("FloatingPanel Picker", "SelectedIndex", "0", index: 1);
        await Driver.TapCss("FloatingPanel Button[text='Done']");

        await Driver.WaitForValue("ConstrainedDuration", "Duration", "00:15:00");
        await Driver.WaitForCssValue("#ConstrainedDuration Label", "Text", "0:15");
        await WaitForPanelClosed();
    }

    [Test]
    public async Task Cancel_Leaves_The_Duration_Alone()
    {
        await Open(Route, PageType);
        await Driver.SetProperty("Duration", "04:00:00", automationId: "ConstrainedDuration");
        await Driver.WaitForCssValue("#ConstrainedDuration Label", "Text", "4:00");

        await OpenConstrainedPanel();
        await Driver.SetCss("FloatingPanel Picker", "SelectedIndex", "7", index: 0);
        await Driver.TapCss("FloatingPanel Button[text='Cancel']");

        await WaitForPanelClosed();
        await Driver.WaitForValue("ConstrainedDuration", "Duration", "04:00:00");
        await Driver.WaitForCssValue("#ConstrainedDuration Label", "Text", "4:00");
    }

    [Test]
    public async Task Panel_Opens_On_The_Current_Value()
    {
        await Open(Route, PageType);
        await Driver.SetProperty("Duration", "03:45:00", automationId: "ConstrainedDuration");

        await OpenConstrainedPanel();

        await Driver.WaitForCssValue("FloatingPanel Picker", "SelectedIndex", "3", index: 0);
        await Driver.WaitForCssValue("FloatingPanel Picker", "SelectedIndex", "9", index: 1);

        await Driver.TapCss("FloatingPanel Button[text='Cancel']");
        await WaitForPanelClosed();
    }
}
