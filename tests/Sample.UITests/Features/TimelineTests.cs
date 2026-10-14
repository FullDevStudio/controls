using Microsoft.Maui.DevFlow.Driver;

namespace Sample.UITests.Features;

[SkipOn(UIPlatform.MacOS, "timeline page not in the macOS head")]
[SkipOn(UIPlatform.Linux, "timeline page not in the Linux head")]
public class TimelineTests : UITest
{
    /// <summary>
    /// Opens the page back in its declared state - third entry active, rail left, not all active.
    /// Shell keeps the page for the session, and the status label only refreshes when a page button
    /// asks, so the reset finishes with a Back tap from the fourth entry.
    /// </summary>
    async Task OpenPage()
    {
        await Open("timeline", "TimelinePage");
        await Driver.SetProperty("AllActive", "False", automationId: "Timeline");
        await Driver.SetProperty("RailPosition", "Left", automationId: "Timeline");
        await Driver.SetProperty("ActiveIndex", "3", automationId: "Timeline");
        await Driver.Tap("TimelineBack");
        await Driver.WaitForProperty("TimelineStatus", "Text", "active: In transit · rail left");
    }


    [Test]
    public async Task Forward_And_Back_Move_The_Active_Index()
    {
        await OpenPage();
        await Driver.WaitForProperty("Timeline", "ActiveIndex", "2");

        await Driver.Tap("TimelineForward");
        await Driver.WaitForProperty("Timeline", "ActiveIndex", "3");
        await Driver.WaitForProperty("TimelineStatus", "Text", "active: Out for delivery · rail left");

        await Driver.Tap("TimelineBack");
        await Driver.Tap("TimelineBack");
        await Driver.WaitForProperty("Timeline", "ActiveIndex", "1");
        await Driver.WaitForProperty("TimelineStatus", "Text", "active: Picked · rail left");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Back_Past_The_First_Entry_Leaves_Nothing_Active()
    {
        await OpenPage();
        await Driver.WaitForProperty("Timeline", "ActiveIndex", "2");

        for (var i = 0; i < 4; i++)
            await Driver.Tap("TimelineBack");

        // The page clamps at -1 - nothing active - rather than wrapping.
        await Driver.WaitForProperty("Timeline", "ActiveIndex", "-1");
        await Driver.WaitForProperty("TimelineStatus", "Text", "nothing active · rail left");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task All_Active_Toggles()
    {
        await OpenPage();

        await Driver.Tap("TimelineAllActive");
        await Driver.WaitForProperty("Timeline", "AllActive", "True");
        await Driver.WaitForProperty("TimelineStatus", "Text", "all active · rail left");

        await Driver.Tap("TimelineAllActive");
        await Driver.WaitForProperty("Timeline", "AllActive", "False");
        await Driver.WaitForProperty("TimelineStatus", "Text", "active: In transit · rail left");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Side_Moves_The_Rail()
    {
        await OpenPage();
        await Driver.WaitForProperty("Timeline", "RailPosition", "Left");

        await Driver.Tap("TimelineSide");

        await Driver.WaitForProperty("Timeline", "RailPosition", "Right");
        await Driver.WaitForProperty("TimelineStatus", "Text", "active: In transit · rail right");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Setting_ActiveIndex_Directly_Is_Accepted()
    {
        await OpenPage();

        await Driver.SetProperty("ActiveIndex", "4", automationId: "Timeline");
        await Driver.WaitForProperty("Timeline", "ActiveIndex", "4");

        // The page's status reflects the control the next time a button asks for it.
        await Driver.Tap("TimelineBack");
        await Driver.WaitForProperty("TimelineStatus", "Text", "active: Out for delivery · rail left");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Tapping_A_Node_Raises_NodeTapped()
    {
        await OpenPage();
        await Driver.WaitUntilTextExists("Picked");

        await TapTappableAncestorOf("Picked");

        await Driver.WaitForProperty("TimelineStatus", "Text", "tapped #2 — Picked");
        await AssertNoAppErrors();
    }


    /// <summary>
    /// Each timeline row carries the TapGestureRecognizer, not the label inside it. Finds the label by
    /// its exact text and taps the nearest ancestor that has a tap gesture.
    /// </summary>
    async Task TapTappableAncestorOf(string exactText)
    {
        var all = new Dictionary<string, ElementInfo>();
        void Walk(IEnumerable<ElementInfo>? items)
        {
            foreach (var e in items ?? [])
            {
                all[e.Id] = e;
                Walk(e.Children);
            }
        }
        Walk(await Driver.Tree(60));

        // only inside the timeline page - Shell keeps other pages alive
        var page = all.Values.FirstOrDefault(e => e.Type == "TimelinePage")
            ?? throw new InvalidOperationException("TimelinePage is not in the visual tree");
        all.Clear();
        Walk([page]);

        var current = all.Values.FirstOrDefault(e => e.Text == exactText)
            ?? throw new InvalidOperationException($"No element with text '{exactText}'");

        while (current != null)
        {
            if (current.Gestures?.Contains("tap") == true)
            {
                await Driver.Tap(elementId: current.Id);
                return;
            }
            current = current.ParentId != null && all.TryGetValue(current.ParentId, out var parent) ? parent : null;
        }
        throw new InvalidOperationException($"No ancestor of '{exactText}' has a tap gesture");
    }
}
