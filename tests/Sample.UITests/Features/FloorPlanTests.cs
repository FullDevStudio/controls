namespace Sample.UITests.Features;

/// <summary>
/// The floor plan editor (route <c>floorplan</c>) and the seating chart (route <c>seatingchart</c>).
/// The plan itself is a Skia canvas DevFlow can't tap, so the editor is driven through its toolbar
/// Buttons (commands that set the view model's ActiveTool) and switches, asserting on the status label
/// (which shows the active tool's name when nothing is selected) and the FloorPlanView's bindables.
/// </summary>
[SkipOn(UIPlatform.Linux, "page not in the Linux head")]
[SkipOn(UIPlatform.MacOS, KnownGaps.SkiaNoAppKitHandler)]
public class FloorPlanTests : UITest
{
    const string Status = "FloorPlanStatus";
    const string Surface = "FloorPlanSurface";

    async Task OpenEditor()
    {
        await Open("floorplan", "FloorPlanPage");
        // Each visit builds a fresh page and view model, but make the starting tool explicit anyway.
        await Driver.Tap("FloorPlanSelectTool");
        await Driver.WaitForProperty(Status, "Text", "Select");
    }

    [Test]
    [Arguments("FloorPlanPanTool", "Pan")]
    [Arguments("FloorPlanDrawRoom", "Draw Room")]
    [Arguments("FloorPlanDrawWall", "Draw Wall")]
    [Arguments("FloorPlanPlaceDesk", "Place Desk")]
    [Arguments("FloorPlanPlaceChair", "Place Chair")]
    [Arguments("FloorPlanPlaceCubicle", "Place Cubicle")]
    [Arguments("FloorPlanPlaceDoor", "Place Door")]
    [Arguments("FloorPlanPlaceOutlet", "Place Standard Outlet")]
    [Arguments("FloorPlanPlaceData", "Place Data Outlet")]
    public async Task Toolbar_Button_Activates_Its_Tool(string button, string toolName)
    {
        await OpenEditor();

        await Driver.Tap(button);
        await Driver.WaitForProperty(Status, "Text", toolName);

        await Driver.Tap("FloorPlanSelectTool");
        await Driver.WaitForProperty(Status, "Text", "Select");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Grid_And_Snap_Switches_Reach_The_Control()
    {
        await OpenEditor();
        await Driver.WaitForProperty(Surface, "ShowGrid", "True");
        await Driver.WaitForProperty(Surface, "SnapToGrid", "True");

        await Driver.SetProperty("IsToggled", "False", automationId: "FloorPlanGridSwitch");
        await Driver.WaitForProperty(Surface, "ShowGrid", "False");

        await Driver.SetProperty("IsToggled", "False", automationId: "FloorPlanSnapSwitch");
        await Driver.WaitForProperty(Surface, "SnapToGrid", "False");

        await Driver.SetProperty("IsToggled", "True", automationId: "FloorPlanGridSwitch");
        await Driver.WaitForProperty(Surface, "ShowGrid", "True");
    }

    [Test]
    public async Task Editor_Starts_In_Edit_Mode_With_Nothing_Selected()
    {
        await OpenEditor();

        await Driver.WaitForProperty(Surface, "Mode", "Edit");
        await Assert.That(await Driver.GetProperty("SelectedElement", automationId: Surface)).IsNull();
    }

    [Test]
    public async Task Viewport_And_Selection_Commands_With_No_Selection_Are_Harmless()
    {
        await OpenEditor();

        foreach (var button in new[] { "FloorPlanZoomIn", "FloorPlanZoomOut", "FloorPlanFit", "FloorPlanDelete", "FloorPlanFront", "FloorPlanBack" })
            await Driver.Tap(button);

        // Nothing was selected, so nothing was deleted or reordered and the status still names the tool.
        await Driver.WaitForProperty(Status, "Text", "Select");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Seating_Chart_Is_Read_Only_View_Mode()
    {
        await Open("seatingchart", "SeatingChartPage");

        await Driver.WaitForProperty("SeatingChartSurface", "Mode", "View");
        await Driver.WaitForProperty("SeatingChartCaption", "Text", "Tap a desk or a room");
        await Assert.That(await Driver.GetProperty("SelectedElement", automationId: "SeatingChartSurface")).IsNull();
        await AssertNoAppErrors();
    }
}
