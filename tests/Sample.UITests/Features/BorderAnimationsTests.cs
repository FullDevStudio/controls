namespace Sample.UITests.Features;

/// <summary>
/// BorderBeam and ShineBorder are drawn by a canvas laid over their content, so these assert on the
/// bindables the page drives and on the content still being laid out inside the clipped border.
/// </summary>
public class BorderAnimationsTests : UITest
{
    Task OpenPage() => Open("borderanimations", "BorderAnimationsPage");

    Task<double?> Height(string automationId) => Driver.GetDouble("Height", automationId);


    [Test]
    public async Task Running_Switch_Stops_And_Starts_Both_Borders()
    {
        await OpenPage();
        await Driver.SetProperty("IsToggled", "True", automationId: "RunningSwitch");
        await Driver.WaitForProperty("RunningBeam", "IsRunning", "True");
        await Driver.WaitForProperty("RunningShine", "IsRunning", "True");

        await Driver.SetProperty("IsToggled", "False", automationId: "RunningSwitch");
        await Driver.WaitForProperty("RunningBeam", "IsRunning", "False");
        await Driver.WaitForProperty("RunningShine", "IsRunning", "False");

        await Driver.SetProperty("IsToggled", "True", automationId: "RunningSwitch");
        await Driver.WaitForProperty("RunningBeam", "IsRunning", "True");
        await Driver.WaitForProperty("RunningShine", "IsRunning", "True");
        await AssertNoAppErrors();
    }


    // Android once clipped the cards to a stale size (a clip first set mid-layout), cutting the content off.
    [Test]
    public async Task Cards_Are_Laid_Out_Around_Their_Content()
    {
        await OpenPage();
        await Driver.WaitUntil(async () => await Height("BeamCard") > 0, "the beam card to be laid out");

        await Driver.WaitUntil(
            async () => await Height("BeamCard") > await Height("BeamCardButton"),
            "the beam card to be taller than the button inside it");
        await Driver.WaitUntil(
            async () => await Height("ShineCard") > await Height("ShineCardText"),
            "the shine card to be taller than the text inside it");

        await Assert.That(await Driver.IsDisplayed("BeamCardButton")).IsTrue();
        await Assert.That(await Driver.IsDisplayed("ShineCardText")).IsTrue();
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Beam_Settings_Change_Live()
    {
        await OpenPage();
        var restore = await Snapshot("BeamCard", "Reverse", "Size", "Duration", "BorderWidth");
        try
        {
            await Driver.SetProperty("Reverse", "True", automationId: "BeamCard");
            await Driver.SetProperty("Size", "120", automationId: "BeamCard");
            await Driver.SetProperty("Duration", "00:00:03", automationId: "BeamCard");
            await Driver.SetProperty("BorderWidth", "4", automationId: "BeamCard");

            await Driver.WaitForProperty("BeamCard", "Reverse", "True");
            await Driver.WaitForProperty("BeamCard", "Duration", "00:00:03");
            await Driver.WaitUntil(async () => await Driver.GetDouble("Size", "BeamCard") == 120, "the beam size to apply");
            await Driver.WaitUntil(async () => await Driver.GetDouble("BorderWidth", "BeamCard") == 4, "the border width to apply");
        }
        finally
        {
            await Restore("BeamCard", restore); // Shell keeps the page alive between visits
        }
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Shine_Settings_Change_Live()
    {
        await OpenPage();
        var restore = await Snapshot("ShineCard", "BorderWidth", "IsRunning");
        try
        {
            await Driver.SetProperty("BorderWidth", "5", automationId: "ShineCard");
            await Driver.SetProperty("IsRunning", "False", automationId: "ShineCard");

            await Driver.WaitForProperty("ShineCard", "IsRunning", "False");
            await Driver.WaitUntil(async () => await Driver.GetDouble("BorderWidth", "ShineCard") == 5, "the border width to apply");

            await Driver.SetProperty("IsRunning", "True", automationId: "ShineCard");
            await Driver.WaitForProperty("ShineCard", "IsRunning", "True");
        }
        finally
        {
            await Restore("ShineCard", restore);
        }
        await AssertNoAppErrors();
    }


    async Task<Dictionary<string, string>> Snapshot(string automationId, params string[] properties)
    {
        var values = new Dictionary<string, string>();
        foreach (var property in properties)
            values[property] = await Driver.GetProperty(property, automationId: automationId) ?? "";
        return values;
    }

    async Task Restore(string automationId, Dictionary<string, string> values)
    {
        foreach (var (property, value) in values)
            await Driver.SetProperty(property, value, automationId: automationId);
    }
}
