using Microsoft.Maui.DevFlow.Driver;

namespace Sample.UITests.Features;

[SkipOn(UIPlatform.MacOS, "gantt page not in the macOS head")]
[SkipOn(UIPlatform.Linux, "gantt page not in the Linux head")]
public class GanttTests : UITest
{
    const int AllTasks = 12;

    /// <summary>
    /// Opens the page and undoes what other tests leave behind (Shell keeps the page for the session):
    /// a collapsed project, the option switches, the work-week calendar.
    /// </summary>
    async Task OpenPage()
    {
        await Open("gantt", "GanttPage");
        await SetSwitch("GanttCriticalSwitch", true);
        await SetSwitch("GanttLinkSwitch", false);
        await SetSwitch("GanttCalendarSwitch", true);
        if (await PaneRows() != AllTasks)
            await Driver.Tap("GanttExpandAll");
        await WaitForPaneRows(AllTasks);
    }

    async Task SetSwitch(string automationId, bool on)
    {
        if (await Driver.GetProperty("IsToggled", automationId: automationId) != on.ToString())
            await Driver.SetProperty("IsToggled", on.ToString(), automationId: automationId);
    }

    async Task<int> PaneRows()
        => (await Driver.Query(type: "GanttTaskPaneRow")).Count;

    async Task WaitForPaneRows(int count)
        => await Driver.WaitUntil(async () => await PaneRows() == count, $"{count} task pane rows");


    [Test]
    public async Task Zoom_In_And_Out_Change_Pixels_Per_Day()
    {
        await OpenPage();
        await WaitForPaneRows(AllTasks);
        var start = await Driver.GetDouble("EffectivePixelsPerDay", "Gantt") ?? 0;

        await Driver.Tap("GanttZoomIn");
        await Driver.WaitUntil(async () => (await Driver.GetDouble("EffectivePixelsPerDay", "Gantt") ?? 0) > start * 1.2,
            "zoom in to widen each day");

        await Driver.Tap("GanttZoomOut");
        await Driver.Tap("GanttZoomOut");
        await Driver.WaitUntil(async () => (await Driver.GetDouble("EffectivePixelsPerDay", "Gantt") ?? 0) < start * 0.9,
            "zoom out to narrow each day");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Option_Switches_Reach_The_Control()
    {
        await OpenPage();
        await Driver.WaitForProperty("Gantt", "ShowCriticalPath", "True");
        await Driver.WaitForProperty("Gantt", "AllowDependencyEdit", "False");

        await Driver.Tap("GanttCriticalSwitch");
        await Driver.WaitForProperty("Gantt", "ShowCriticalPath", "False");

        await Driver.Tap("GanttLinkSwitch");
        await Driver.WaitForProperty("Gantt", "AllowDependencyEdit", "True");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Work_Week_Switch_Drops_The_Working_Calendar()
    {
        await OpenPage();
        await Driver.WaitUntil(async () => await Driver.GetProperty("Calendar", automationId: "Gantt") != null,
            "the working calendar to be bound");

        // The view model hands the control null - continuous time - when the switch is off.
        await Driver.Tap("GanttCalendarSwitch");
        await Driver.WaitUntil(async () => await Driver.GetProperty("Calendar", automationId: "Gantt") == null,
            "the calendar to be cleared");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Collapse_All_And_Expand_All()
    {
        await OpenPage();
        await WaitForPaneRows(AllTasks);

        await Driver.Tap("GanttCollapseAll");
        await WaitForPaneRows(1);
        await Assert.That((await Driver.Query(text: "User interviews")).Count).IsEqualTo(0);

        await Driver.Tap("GanttExpandAll");
        await WaitForPaneRows(AllTasks);
        await Driver.WaitUntilTextExists("User interviews");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Chevron_Collapses_The_Project()
    {
        await OpenPage();
        await WaitForPaneRows(AllTasks);

        // The first expanded chevron in tree order is the root project's.
        await TapTappableAncestorOf("▾");

        await WaitForPaneRows(1);
        await Driver.WaitUntil(async () => (await Driver.Query(text: "▸")).Any(e => e.Text == "▸"), "the chevron to flip to collapsed");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Tapping_A_Row_Selects_The_Task()
    {
        await OpenPage();
        await WaitForPaneRows(AllTasks);

        await TapTappableAncestorOf("Wireframes");

        await Driver.WaitForProperty("Gantt", "SelectedTask.Name", "Wireframes");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Undo_Starts_Disabled_And_Reset_Reports()
    {
        await OpenPage();
        await Driver.WaitForProperty("GanttUndo", "IsEnabled", "False");

        await Driver.Tap("GanttReset");

        await Driver.WaitForProperty("GanttStatus", "Text", "Plan reset");
        await WaitForPaneRows(AllTasks);
        await Driver.WaitForProperty("GanttUndo", "IsEnabled", "False");
        await AssertNoAppErrors();
    }


    /// <summary>
    /// Task pane rows and chevrons carry their TapGestureRecognizer on a container, not on the label.
    /// Finds the label by exact text and taps the nearest ancestor with a tap gesture.
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
        var tree = await Driver.Tree(60);
        Walk(tree);

        // Only inside the Gantt page: Shell keeps other pages alive, and Kanban has chevrons too.
        var page = all.Values.FirstOrDefault(e => e.Type == "GanttPage")
            ?? throw new InvalidOperationException("GanttPage is not in the visual tree");
        all.Clear();
        Walk([page]);

        var current = all.Values.FirstOrDefault(e => e.Text == exactText)
            ?? throw new InvalidOperationException($"No element with text '{exactText}' on the Gantt page");

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
