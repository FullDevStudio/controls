namespace Sample.UITests.Features;

[SkipOn(UIPlatform.MacOS, "kanban page not in the macOS head")]
[SkipOn(UIPlatform.Linux, "kanban page not in the Linux head")]
public class KanbanTests : UITest
{
    const string Expanded = "‹";
    const string Collapsed = "›";

    /// <summary>
    /// Opens the board and undoes what other tests leave behind - Shell keeps the page (and its
    /// view-model) for the session: collapsed columns, the swimlane and WIP switches.
    /// </summary>
    async Task OpenPage()
    {
        await Open("kanban", "KanbanPage");
        await SetSwitch("KanbanSwimlaneSwitch", false);
        await SetSwitch("KanbanWipSwitch", false);
        await Driver.WaitForProperty("KanbanBoard", "SwimlaneMode", "None");
        if (await CountLabels(Collapsed) > 0)
            await Driver.Tap("KanbanExpandAll");
    }

    async Task SetSwitch(string automationId, bool on)
    {
        if (await Driver.GetProperty("IsToggled", automationId: automationId) != on.ToString())
            await Driver.SetProperty("IsToggled", on.ToString(), automationId: automationId);
    }

    /// <summary>
    /// Labels on the Kanban page with exactly this text. Scoped to the page: Shell keeps other pages
    /// alive, and the DataGrid pager's "‹" would otherwise count as a column chevron.
    /// </summary>
    async Task<int> CountLabels(string exactText)
        => (await Driver.QuerySelector($"KanbanPage Label[Text={MauiDevFlowDriver.Quote(exactText)}]")).Count(e => e.IsVisible);


    [Test]
    public async Task Board_Renders_Columns_With_Wip_Counts()
    {
        await OpenPage();
        await Driver.WaitUntilTextExists("Audit the theme tokens");

        // Five columns, all open. In progress has a WIP limit of two and two cards.
        await Driver.WaitUntil(async () => await CountLabels(Expanded) == 5, "five expanded column chevrons");
        await Assert.That(await CountLabels("2 / 2") > 0).IsTrue();
    }


    [Test]
    public async Task Collapse_All_And_Expand_All()
    {
        await OpenPage();
        await Driver.WaitUntil(async () => await CountLabels(Expanded) == 5, "five expanded column chevrons");

        await Driver.Tap("KanbanCollapseAll");
        await Driver.WaitUntil(async () => await CountLabels(Collapsed) == 5 && await CountLabels(Expanded) == 0,
            "every column collapsed");
        // Collapsed spines squeeze the WIP count together.
        await Driver.WaitUntil(async () => await CountLabels("2/2") > 0, "the compact WIP count");

        await Driver.Tap("KanbanExpandAll");
        await Driver.WaitUntil(async () => await CountLabels(Expanded) == 5 && await CountLabels(Collapsed) == 0,
            "every column expanded again");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Chevron_Collapses_One_Column()
    {
        await OpenPage();
        await Driver.WaitUntil(async () => await CountLabels(Expanded) == 5, "five expanded column chevrons");

        // The chevron Label itself carries the TapGestureRecognizer (with a Command).
        var chevron = await Driver.FindBySelector($"KanbanPage Label[Text={MauiDevFlowDriver.Quote(Expanded)}]");
        await Driver.Tap(elementId: chevron.Id);
        try
        {
            await Driver.WaitUntil(async () => await CountLabels(Collapsed) == 1 && await CountLabels(Expanded) == 4,
                "exactly one column collapsed");
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.Tap("KanbanExpandAll");
        }
    }


    [Test]
    public async Task Swimlane_Switch_Groups_The_Board()
    {
        await OpenPage();
        await Driver.WaitForProperty("KanbanBoard", "SwimlaneMode", "None");

        await Driver.Tap("KanbanSwimlaneSwitch");

        await Driver.WaitForProperty("KanbanBoard", "SwimlaneMode", "Grouped");
        await Driver.WaitUntil(async () => await CountLabels("Platform") == 1 && await CountLabels("Mobile") == 1,
            "the Platform and Mobile swimlane headers");

        await Driver.Tap("KanbanSwimlaneSwitch");
        await Driver.WaitForProperty("KanbanBoard", "SwimlaneMode", "None");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Wip_Switch_Turns_Limits_Into_Blocks()
    {
        await OpenPage();
        await Driver.WaitForProperty("KanbanBoard", "WipBehavior", "Warn");

        await Driver.Tap("KanbanWipSwitch");

        await Driver.WaitForProperty("KanbanBoard", "WipBehavior", "Block");
        await AssertNoAppErrors();

        await Driver.Tap("KanbanWipSwitch");
        await Driver.WaitForProperty("KanbanBoard", "WipBehavior", "Warn");
    }


    [Test]
    public async Task Undo_Starts_Disabled_And_Reset_Restores()
    {
        await OpenPage();
        await Driver.WaitForProperty("KanbanUndo", "IsEnabled", "False");

        await Driver.Tap("KanbanCollapseAll");
        await Driver.WaitUntil(async () => await CountLabels(Collapsed) == 5, "every column collapsed");

        await Driver.Tap("KanbanReset");

        await Driver.WaitForProperty("KanbanStatus", "Text", "Board reset");
        // Reset also re-opens every column.
        await Driver.WaitUntil(async () => await CountLabels(Expanded) == 5, "every column expanded by the reset");
        await Driver.WaitForProperty("KanbanUndo", "IsEnabled", "False");
        await AssertNoAppErrors();
    }
}
