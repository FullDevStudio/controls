namespace Sample.UITests.Features;

[SkipOn(UIPlatform.MacOS, "progressline page not in the macOS head")]
[SkipOn(UIPlatform.Linux, "progressline page not in the Linux head")]
public class ProgressLineTests : UITest
{
    Task OpenPage() => Open("progressline", "ProgressLinePage");

    Task<double?> BoundValue() => Driver.GetDouble("Value", "BoundProgressLine");

    Task WaitForStatus(string status, int timeoutSeconds = 15)
        => Driver.WaitForProperty("ProgressLineStatus", "Text", status, timeoutSeconds);

    async Task WaitForValue(double expected)
    {
        await Driver.WaitUntil(async () => await BoundValue() == expected, $"BoundProgressLine.Value == {expected}");
        await Driver.WaitUntil(async () => await Driver.GetDouble("Value", "InlineProgressLine") == expected, $"InlineProgressLine.Value == {expected}");
        await Driver.WaitForProperty("BoundValueLabel", "Text", $"{expected:0}%");
    }


    [Test]
    public async Task Increment_Moves_Both_Bound_Lines()
    {
        await OpenPage();

        if (await BoundValue() >= 100)
            await Driver.Tap("DecrementButton");

        var start = (await BoundValue())!.Value;
        await Driver.Tap("IncrementButton");
        await WaitForValue(Math.Min(100, start + 15));
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Value_Clamps_At_Both_Ends()
    {
        await OpenPage();

        for (var i = 0; i < 8; i++)
            await Driver.Tap("DecrementButton");
        await WaitForValue(0);

        for (var i = 0; i < 8; i++)
            await Driver.Tap("IncrementButton");
        await WaitForValue(100);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task IsActive_False_Hides_The_Docked_Line()
    {
        await OpenPage();

        await Driver.SetProperty("IsActive", "False", automationId: "BoundProgressLine");
        await Driver.WaitForProperty("BoundProgressLine", "IsVisible", "False");

        await Driver.SetProperty("IsActive", "True", automationId: "BoundProgressLine");
        await Driver.WaitForProperty("BoundProgressLine", "IsVisible", "True");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Reported_Run_Steps_Through_Percentages()
    {
        await OpenPage();

        await Driver.Tap("ReportedButton");
        await Driver.WaitUntil(async () => (await Driver.GetProperty("Text", automationId: "ProgressLineStatus"))?.StartsWith("Uploading") == true, "status reports an upload percentage");
        await WaitForStatus("Uploaded");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Trickle_Run_Completes()
    {
        await OpenPage();

        await Driver.Tap("TrickleButton");
        await WaitForStatus("Trickling…");
        await WaitForStatus("Done");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Overlapping_Runs_Finish_Together()
    {
        await OpenPage();

        await Driver.Tap("OverlappingButton");
        await WaitForStatus("Two operations…");

        // the quick run ends at 0.8s; the slow one holds the operation open to 4s
        await Task.Delay(2000);
        await Driver.WaitForProperty("ProgressLineStatus", "Text", "Two operations…", 2);

        await WaitForStatus("Both finished");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Bottom_And_Indeterminate_Runs_Complete()
    {
        await OpenPage();

        await Driver.Tap("BottomButton");
        await WaitForStatus("Working along the bottom…");
        await WaitForStatus("Done");

        await Driver.Tap("IndeterminateButton");
        await WaitForStatus("No idea how long this takes…");
        await WaitForStatus("Done");
        await AssertNoAppErrors();
    }
}
