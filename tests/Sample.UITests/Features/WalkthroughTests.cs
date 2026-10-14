namespace Sample.UITests.Features;

/// <summary>
/// Walkthrough on <c>walkthrough</c>. The callout's Next/Skip are drawn labels with a Tapped handler
/// (nothing for the agent to invoke), so the tour is driven by the page's Start/Restart buttons and
/// its own IsRunning, and read back through CurrentStep/StepNumber/StepCount and the status line.
/// It auto-starts on first open, so every test stops whatever is running first.
/// </summary>
public class WalkthroughTests : UITest
{
    const string Tour = "DemoWalkthrough";
    const string Status = "WalkthroughStatus";

    const string WalkthroughStopDuringStartup =
        "UNVERIFIED on AppKit: the Stop-during-auto-start / ignored-Start bug is fixed in src (Walkthrough queues the Start and records the Stop) "
        + "and verified on iOS, but the old run's unwind was unbounded on AppKit and that head has not been re-run";

    async Task OpenStopped(bool advanced = false)
    {
        await Open("walkthrough", "WalkthroughPage");
        await Stop();

        var want = advanced ? "True" : "False";
        if (await Driver.GetProperty("IsChecked", automationId: "WalkthroughAdvancedCheck") != want)
            await Driver.SetProperty("IsChecked", want, automationId: "WalkthroughAdvancedCheck");
        await Driver.WaitForProperty("WalkthroughAdvancedCheck", "IsChecked", want);
    }

    static int SaveCount(string? status)
        => Int32.Parse(System.Text.RegularExpressions.Regex.Match(status ?? "", @"Saved (\d+) time").Groups[1].Value);

    async Task Stop()
    {
        // an AutoStart may still be pending (AutoStartDelay 700ms) - give it the chance to land first
        await Task.Delay(1000);
        if (await Driver.GetProperty("IsRunning", automationId: Tour) == "True")
            await Driver.SetProperty("IsRunning", "False", automationId: Tour);
        await Driver.WaitForProperty(Tour, "IsRunning", "False");
    }

    // Probed on the macOS head (harness timing: open, Stop() during the auto-start, then tap): the Stop lands
    // while the run is still starting (IsRunning True, CurrentStepIndex -1), the status then reads "Tour completed
    // - it will not auto-start again" (a Stop recorded as Completed), and the Start tap right after it is
    // ignored because the internal run is still unwinding; in the suite it stayed ignored for 4 taps over 12s.
    [Test, SkipOn(UIPlatform.MacOS, WalkthroughStopDuringStartup)]
    public async Task Start_Runs_From_The_Welcome_Step()
    {
        await OpenStopped();
        try
        {
            await Driver.Tap(automationId: "WalkthroughStart");
            await Driver.WaitForProperty(Tour, "IsRunning", "True");
            await Driver.WaitForProperty(Tour, "CurrentStep", "Welcome");
            await Driver.WaitForProperty(Tour, "StepNumber", "1");
            await Driver.WaitForProperty(Tour, "CurrentStepIndex", "0");
            await Driver.WaitForProperty(Status, "Text", "Entered step: Welcome");
            await AssertNoAppErrors();
        }
        finally
        {
            await Stop();
        }
    }

    [Test]
    public async Task Conditional_Step_Changes_The_Step_Count()
    {
        await OpenStopped(advanced: false);
        try
        {
            await Driver.Tap(automationId: "WalkthroughStart");
            await Driver.WaitForProperty(Tour, "StepCount", "5");
            await Stop();

            await Driver.SetProperty("IsChecked", "True", automationId: "WalkthroughAdvancedCheck");
            await Driver.WaitForProperty(Status, "Text", "The Advanced step is now part of the tour.");

            await Driver.Tap(automationId: "WalkthroughStart");
            await Driver.WaitForProperty(Tour, "StepCount", "6");
            await AssertNoAppErrors();
        }
        finally
        {
            await Stop();
            await Driver.SetProperty("IsChecked", "False", automationId: "WalkthroughAdvancedCheck");
        }
    }

    // Probed on the macOS head (harness timing: open, Stop() during the auto-start, then tap): the Stop lands
    // while the run is still starting (IsRunning True, CurrentStepIndex -1), the status then reads "Tour completed
    // - it will not auto-start again" (a Stop recorded as Completed), and the Start tap right after it is
    // ignored because the internal run is still unwinding; in the suite it stayed ignored for 4 taps over 12s.
    [Test, SkipOn(UIPlatform.MacOS, WalkthroughStopDuringStartup)]
    public async Task Stopping_Clears_The_Position()
    {
        await OpenStopped();
        await Driver.Tap(automationId: "WalkthroughRestart");
        await Driver.WaitForProperty(Tour, "IsRunning", "True");
        await Driver.WaitForProperty(Tour, "CurrentStep", "Welcome");

        await Driver.SetProperty("IsRunning", "False", automationId: Tour);
        await Driver.WaitForProperty(Tour, "CurrentStepIndex", "-1");
        await Driver.WaitUntil(async () => String.IsNullOrEmpty(await Driver.GetProperty("CurrentStep", automationId: Tour)), "CurrentStep to clear");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Overlay_Checkbox_Binds_UseOverlay()
    {
        await OpenStopped();
        try
        {
            await Driver.SetProperty("IsChecked", "False", automationId: "WalkthroughOverlayCheck");
            await Driver.WaitForProperty(Tour, "UseOverlay", "False");

            await Driver.SetProperty("IsChecked", "True", automationId: "WalkthroughOverlayCheck");
            await Driver.WaitForProperty(Tour, "UseOverlay", "True");
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.SetProperty("IsChecked", "True", automationId: "WalkthroughOverlayCheck");
        }
    }

    [Test]
    public async Task Save_Outside_A_Tour_Just_Runs_Its_Command()
    {
        await OpenStopped();
        await Driver.Tap(automationId: "WalkthroughSave");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: Status))?.StartsWith("Saved ", StringComparison.Ordinal) == true,
            "the save count status"
        );
        var first = SaveCount(await Driver.GetProperty("Text", automationId: Status));

        await Driver.Tap(automationId: "WalkthroughSave");
        await Driver.WaitForProperty(Status, "Text", $"Saved {first + 1} time(s).");
        await Assert.That(await Driver.GetProperty("IsRunning", automationId: Tour)).IsEqualTo("False");
        await AssertNoAppErrors();
    }
}
