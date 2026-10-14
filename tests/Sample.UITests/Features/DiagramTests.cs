namespace Sample.UITests.Features;

/// <summary>
/// The diagram demo (route <c>diagram</c>). The surface is a Skia canvas DevFlow can't tap, so tests
/// drive the page's pickers, switches and Buttons and read back the <c>DiagramView</c>'s bindables and
/// the status label the view model writes ("N nodes, M connections" on every load).
/// </summary>
[SkipOn(UIPlatform.MacOS, "page not in the macOS head")]
[SkipOn(UIPlatform.Linux, "page not in the Linux head")]
public class DiagramTests : UITest
{
    const string Status = "DiagramStatus";
    const string Surface = "DiagramSurface";

    /// <summary>Opens the page on the org chart regardless of what an earlier visit left in the view model.</summary>
    async Task OpenOrgChart()
    {
        await Open("diagram", "DiagramPage");
        await Driver.SetProperty("SelectedIndex", "0", automationId: "DiagramSamplePicker");
        await Driver.Tap("DiagramReset");
        await Driver.WaitForProperty(Status, "Text", "10 nodes, 0 connections");
    }

    [Test]
    public async Task Switching_Sample_Reloads_Graph_And_Suggests_Layout()
    {
        await OpenOrgChart();
        await Driver.WaitForProperty(Surface, "LayoutKind", "Tree");

        await Driver.SetProperty("SelectedIndex", "1", automationId: "DiagramSamplePicker"); // DecisionTree
        await Driver.WaitForProperty(Status, "Text", "7 nodes, 8 connections");
        await Driver.WaitForProperty(Surface, "LayoutKind", "Layered");
        await Driver.WaitForProperty("DiagramLayoutPicker", "SelectedItem", "Layered");

        await Driver.SetProperty("SelectedIndex", "3", automationId: "DiagramSamplePicker"); // MindMap
        await Driver.WaitForProperty(Status, "Text", "13 nodes, 0 connections");
        await Driver.WaitForProperty(Surface, "LayoutKind", "MindMap");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Layout_Direction_And_Router_Pickers_Reach_The_Control()
    {
        await OpenOrgChart();

        await Driver.SetProperty("SelectedIndex", "4", automationId: "DiagramLayoutPicker"); // Radial
        await Driver.WaitForProperty(Surface, "LayoutKind", "Radial");

        await Driver.SetProperty("SelectedIndex", "2", automationId: "DiagramDirectionPicker"); // LeftToRight
        await Driver.WaitForProperty(Surface, "Direction", "LeftToRight");

        await Driver.SetProperty("SelectedIndex", "2", automationId: "DiagramRouterPicker"); // Bezier
        await Driver.WaitForProperty(Surface, "Router", "Bezier");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Switches_Reach_The_Control()
    {
        await OpenOrgChart();

        await Driver.SetProperty("IsToggled", "True", automationId: "DiagramTipOverSwitch");
        await Driver.WaitForProperty(Surface, "TreeStyle", "TipOver");
        await Driver.SetProperty("IsToggled", "False", automationId: "DiagramTipOverSwitch");
        await Driver.WaitForProperty(Surface, "TreeStyle", "Normal");

        await Driver.SetProperty("IsToggled", "False", automationId: "DiagramGridSwitch");
        await Driver.WaitForProperty(Surface, "ShowGrid", "False");
        await Driver.SetProperty("IsToggled", "True", automationId: "DiagramGridSwitch");
        await Driver.WaitForProperty(Surface, "ShowGrid", "True");

        await Driver.SetProperty("IsToggled", "True", automationId: "DiagramSnapSwitch");
        await Driver.WaitForProperty(Surface, "SnapToGrid", "True");
        await Driver.SetProperty("IsToggled", "False", automationId: "DiagramSnapSwitch");
        await Driver.WaitForProperty(Surface, "SnapToGrid", "False");

        await Driver.SetProperty("IsToggled", "False", automationId: "DiagramDragSwitch");
        await Driver.WaitForProperty(Surface, "AllowNodeDrag", "False");
        await Driver.SetProperty("IsToggled", "True", automationId: "DiagramDragSwitch");
        await Driver.WaitForProperty(Surface, "AllowNodeDrag", "True");

        await Driver.SetProperty("IsToggled", "False", automationId: "DiagramLinkSwitch");
        await Driver.WaitForProperty(Surface, "AllowConnectionEdit", "False");
        await Driver.SetProperty("IsToggled", "True", automationId: "DiagramLinkSwitch");
        await Driver.WaitForProperty(Surface, "AllowConnectionEdit", "True");
    }

    [Test]
    public async Task Add_Node_Then_Reset_Restores_The_Sample()
    {
        await OpenOrgChart();

        await Driver.Tap("DiagramAdd");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: Status))?.StartsWith("Added a node") == true,
            "status reports the added node"
        );

        await Driver.Tap("DiagramReset");
        await Driver.WaitForProperty(Status, "Text", "10 nodes, 0 connections");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Zoom_Is_Clamped_And_Fit_Changes_It()
    {
        await OpenOrgChart();

        // MaxZoom defaults to 4; the coerce callback clamps anything past it.
        await Driver.SetProperty("Zoom", "10", automationId: Surface);
        await Driver.WaitForProperty(Surface, "Zoom", "4");

        await Driver.Tap("DiagramFit");
        await Driver.WaitUntil(
            async () => await Driver.GetDouble("Zoom", Surface) is { } z && z < 4 && z >= 0.2,
            "Fit brings the whole org chart into view, below the clamped maximum"
        );
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Undo_Delete_And_Redo_With_Nothing_To_Do_Are_Harmless()
    {
        await OpenOrgChart();

        await Driver.Tap("DiagramUndo");
        await Driver.Tap("DiagramRedo");
        await Driver.Tap("DiagramDelete");

        await Assert.That(await Driver.GetProperty("CanUndo", automationId: Surface)).IsEqualTo("False");
        await Driver.WaitForProperty(Status, "Text", "10 nodes, 0 connections");
        await AssertNoAppErrors();
    }
}
