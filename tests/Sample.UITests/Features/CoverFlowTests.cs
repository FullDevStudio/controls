namespace Sample.UITests.Features;

/// <summary>
/// CoverFlowView on the Cover Flow page. Shell keeps the page alive between tests, so every test puts
/// the control in a known position (and Loop off) before it starts.
/// </summary>
public class CoverFlowTests : UITest
{
    const string Flow = "CoverFlow";

    async Task OpenAt(int position)
    {
        await Open("coverflow", "CoverFlowPage");
        await Driver.SetProperty("IsToggled", "False", automationId: "CoverFlowLoop");
        await Driver.WaitForProperty(Flow, "Loop", "False");
        await Driver.SetProperty("CurrentPosition", position.ToString(), automationId: Flow);
        await Driver.WaitForProperty(Flow, "CurrentPosition", position.ToString());
        await Driver.WaitForProperty("CoverFlowPosition", "Text", $"{position + 1} / 12");
    }

    async Task<int> Position()
        => Int32.Parse((await Driver.GetProperty("CurrentPosition", automationId: Flow))!);


    [Test]
    public async Task Next_And_Previous_Step_The_Centre_Cover()
    {
        await OpenAt(3);

        await Driver.Tap("CoverFlowNext");
        await Driver.WaitForProperty(Flow, "CurrentPosition", "4");
        await Driver.WaitForProperty("CoverFlowPosition", "Text", "5 / 12");

        await Driver.Tap("CoverFlowPrevious");
        await Driver.WaitForProperty(Flow, "CurrentPosition", "3");
        await Driver.WaitForProperty("CoverFlowPosition", "Text", "4 / 12");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Setting_CurrentPosition_Moves_The_Caption_With_It()
    {
        await OpenAt(7);

        // the caption is a template bound to the centre album
        await Driver.WaitUntilTextExists("Saltwater Radio");
        await Driver.WaitUntilTextExists("The Tidecallers");

        await Driver.SetProperty("CurrentPosition", "0", automationId: Flow);
        await Driver.WaitForProperty("CoverFlowPosition", "Text", "1 / 12");
        await Driver.WaitUntilTextExists("The Lanterns");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Panning_Left_Moves_Forward_And_Right_Moves_Back()
    {
        await OpenAt(3);

        await Driver.Gesture("pan", automationId: Flow, direction: "left", distance: 260, durationMs: 400);
        await Driver.WaitUntil(async () => await Position() > 3, "a left pan to settle on a later cover");
        var forward = await Position();
        await Driver.WaitForProperty("CoverFlowPosition", "Text", $"{forward + 1} / 12");

        await Driver.Gesture("pan", automationId: Flow, direction: "right", distance: 260, durationMs: 400);
        await Driver.WaitUntil(async () => await Position() < forward, "a right pan to settle on an earlier cover");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Panning_Past_The_First_Cover_Springs_Back()
    {
        await OpenAt(0);

        await Driver.Gesture("pan", automationId: Flow, direction: "right", distance: 400, durationMs: 400);
        await Task.Delay(1500);
        await Assert.That(await Position()).IsEqualTo(0);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Activating_The_Control_Selects_The_Centre_Cover()
    {
        await OpenAt(3);

        // A tap with no position (DevFlow, accessibility activation) selects the centre cover.
        await Driver.Tap(Flow);
        await Driver.WaitForProperty("CoverFlowStatus", "Text", "Selected Glass Orchard by Wren & Ash");
        await Assert.That(await Position()).IsEqualTo(3);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Loop_Wraps_Past_The_Last_Cover()
    {
        await OpenAt(11);
        try
        {
            await Driver.SetProperty("IsToggled", "True", automationId: "CoverFlowLoop");
            await Driver.WaitForProperty(Flow, "Loop", "True");

            await Driver.Tap("CoverFlowNext");
            await Driver.WaitForProperty(Flow, "CurrentPosition", "0");
            await Driver.WaitForProperty("CoverFlowPosition", "Text", "1 / 12");

            await Driver.Tap("CoverFlowPrevious");
            await Driver.WaitForProperty(Flow, "CurrentPosition", "11");
        }
        finally
        {
            await Driver.SetProperty("IsToggled", "False", automationId: "CoverFlowLoop");
        }
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Without_Loop_Next_Stops_At_The_Last_Cover()
    {
        await OpenAt(11);

        await Driver.Tap("CoverFlowNext");
        await Task.Delay(800);
        await Assert.That(await Position()).IsEqualTo(11);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Page_Switches_Drive_Reflection_And_Angle()
    {
        await OpenAt(3);
        try
        {
            await Driver.SetProperty("IsToggled", "False", automationId: "CoverFlowReflection");
            await Driver.WaitForProperty(Flow, "ShowReflection", "False");
            await Driver.SetProperty("IsToggled", "True", automationId: "CoverFlowReflection");
            await Driver.WaitForProperty(Flow, "ShowReflection", "True");

            await Driver.SetProperty("Value", "75", automationId: "CoverFlowAngle");
            await Driver.WaitForProperty(Flow, "SideAngle", "75");
        }
        finally
        {
            await Driver.SetProperty("Value", "60", automationId: "CoverFlowAngle");
        }
        await AssertNoAppErrors();
    }


    // ── real finger input (Android) ─────────────────────────────────────────

    [Test]
    public async Task Real_Swipe_Moves_Through_Covers()
    {
        if (!HasRealTouch)
            Skip.Test("needs real touch input (Android)");

        await OpenAt(3);
        await TouchSwipe(Flow, 0.8, 0.2, 600);
        await Driver.WaitUntil(async () => await Position() > 3, "a real leftward swipe to move forward", timeoutSeconds: 5);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Real_Taps_Bring_A_Side_Cover_To_The_Centre_Then_Select_It()
    {
        if (!HasRealTouch)
            Skip.Test("needs real touch input (Android)");

        await OpenAt(3);

        // the first cover right of centre sits CenterSpacing (120) right of the middle, in the cover band
        var bounds = (await Driver.Find(Flow)).WindowBounds!;
        await TouchTap(Flow, 0.5 + 120 / bounds.Width, 0.3);
        await Driver.WaitForProperty(Flow, "CurrentPosition", "4", timeoutSeconds: 5);

        await TouchTap(Flow, 0.5, 0.3);
        await Driver.WaitForProperty("CoverFlowStatus", "Text", "Selected Slow Thunder by Atlas Drift", timeoutSeconds: 5);
        await AssertNoAppErrors();
    }
}
