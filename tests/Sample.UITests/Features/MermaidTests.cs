namespace Sample.UITests.Features;

/// <summary>
/// The Mermaid diagram pages (routes <c>flowchart</c>, <c>directions</c>, <c>themes</c>, <c>subgraphs</c>,
/// <c>editor</c>). The diagram is drawn on a GraphicsView, so tests read the control's bindables:
/// <c>ParseError</c> (null when the text parsed), <c>Theme</c>, <c>AllowPanning</c>/<c>AllowZooming</c>.
/// </summary>
public class MermaidTests : UITest
{
    const string ValidGraph = "graph TD\n    X[One] --> Y[Two]";

    async Task AssertParsed(string automationId)
        => await Driver.WaitUntil(
            async () => String.IsNullOrEmpty(await Driver.GetProperty("ParseError", automationId: automationId)),
            $"{automationId} parsed its diagram without a ParseError"
        );

    async Task WaitForParseError(string automationId, string contains)
        => await Driver.WaitUntil(
            async () => (await Driver.GetProperty("ParseError", automationId: automationId))?.Contains(contains) == true,
            $"{automationId}.ParseError mentions '{contains}'"
        );

    [Test]
    public async Task Flowchart_Parses_And_Reports_Invalid_Text()
    {
        await Open("flowchart", "BasicFlowchartPage");
        await AssertParsed("FlowchartDiagram");

        await Driver.SetProperty("DiagramText", "hello world", automationId: "FlowchartDiagram");
        await WaitForParseError("FlowchartDiagram", "Expected 'graph' or 'flowchart'");

        await Driver.SetProperty("DiagramText", ValidGraph, automationId: "FlowchartDiagram");
        await AssertParsed("FlowchartDiagram");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Directions_All_Parse_With_Gestures_Disabled()
    {
        await Open("directions", "DirectionsPage");

        foreach (var direction in new[] { "TD", "LR", "BT", "RL" })
        {
            var id = $"Directions{direction}Diagram";
            await AssertParsed(id);
            await Driver.WaitForProperty(id, "AllowPanning", "False");
            await Driver.WaitForProperty(id, "AllowZooming", "False");
        }
        await AssertNoAppErrors();
    }

    [Test]
    [Arguments("1", "DarkTheme")]
    [Arguments("2", "ForestTheme")]
    [Arguments("3", "NeutralTheme")]
    public async Task Theme_Picker_Applies_Theme(string index, string themeType)
    {
        await Open("themes", "ThemesPage");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Theme", automationId: "ThemesDiagram"))?.Contains("DefaultTheme") == true,
            "the page starts on the default theme"
        );

        await Driver.SetProperty("SelectedIndex", index, automationId: "ThemesPicker");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Theme", automationId: "ThemesDiagram"))?.Contains(themeType) == true,
            $"Theme becomes {themeType}"
        );

        await Driver.SetProperty("SelectedIndex", "0", automationId: "ThemesPicker");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Theme", automationId: "ThemesDiagram"))?.Contains("DefaultTheme") == true,
            "Theme returns to DefaultTheme"
        );
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Subgraphs_Sample_Parses_Without_Error()
    {
        await Open("subgraphs", "SubgraphsPage");
        await AssertParsed("SubgraphsDiagram");

        await AssertNoAppErrors();
    }

    [Test]
    public async Task Editor_Text_Drives_The_Diagram()
    {
        await Open("editor", "InteractiveEditorPage");
        // Shell keeps the page: start from a blank editor rather than whatever an earlier test typed
        await Driver.SetProperty("Text", "", automationId: "EditorText");
        await AssertParsed("EditorDiagram");
        await Assert.That(await Driver.IsElementVisible("EditorParseError")).IsFalse();

        await Driver.SetProperty("Text", ValidGraph, automationId: "EditorText");

        await Driver.WaitForProperty("EditorDiagram", "DiagramText", ValidGraph);
        await AssertParsed("EditorDiagram");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Editor_Shows_And_Clears_The_Parse_Error_Label()
    {
        await Open("editor", "InteractiveEditorPage");

        await Driver.SetProperty("Text", "not a diagram", automationId: "EditorText");

        await Driver.WaitForProperty("EditorParseError", "IsVisible", "True");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: "EditorParseError")) is { } t
                && t.StartsWith("Parse error: ") && t.Contains("Expected 'graph' or 'flowchart'"), // "Parse error: Line 1, Col 1: Expected …"
            "the label shows the parser's message"
        );

        await Driver.SetProperty("Text", ValidGraph, automationId: "EditorText");
        await Driver.WaitForProperty("EditorParseError", "IsVisible", "False");
        await Driver.WaitUntil(
            async () => String.IsNullOrEmpty(await Driver.GetProperty("Text", automationId: "EditorParseError")),
            "the label's text is cleared"
        );
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Editor_Empty_Text_Is_Not_An_Error()
    {
        await Open("editor", "InteractiveEditorPage");

        await Driver.SetProperty("Text", "", automationId: "EditorText");

        await AssertParsed("EditorDiagram");
        await Driver.WaitForProperty("EditorParseError", "IsVisible", "False");
    }

    [Test]
    public async Task Editor_Pan_And_Zoom_Switches_Toggle_Gestures()
    {
        await Open("editor", "InteractiveEditorPage");
        await Driver.WaitForProperty("EditorDiagram", "AllowPanning", "True");
        await Driver.WaitForProperty("EditorDiagram", "AllowZooming", "True");

        await Driver.SetProperty("IsToggled", "False", automationId: "EditorPanSwitch");
        await Driver.WaitForProperty("EditorDiagram", "AllowPanning", "False");

        await Driver.SetProperty("IsToggled", "False", automationId: "EditorZoomSwitch");
        await Driver.WaitForProperty("EditorDiagram", "AllowZooming", "False");

        await Driver.SetProperty("IsToggled", "True", automationId: "EditorPanSwitch");
        await Driver.WaitForProperty("EditorDiagram", "AllowPanning", "True");
    }
}
