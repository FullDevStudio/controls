using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The diagram demo (/diagram). The surface is SVG, so nodes are real elements: their labels are
/// <c>text.shiny-diagram-label</c> and their outlines <c>path.shiny-diagram-shape</c>, which gains
/// <c>is-selected</c> when picked. The page reports selection and edits in <c>p.status-message</c>.
/// </summary>
public class DiagramTests : BlazorTest
{
    ILocator Status => Page.Locator("p.status-message");
    ILocator Surface => Page.Locator(".shiny-diagram");
    ILocator Label(string text) => Page.Locator("text.shiny-diagram-label", new() { HasTextString = text });
    ILocator SelectedShapes => Page.Locator("path.shiny-diagram-shape.is-selected");
    ILocator Picker(string label) => Page.Locator("label", new() { HasTextString = label }).Locator("select");
    ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>Labels don't take pointer events (the shape under them does), so click at the label's centre.</summary>
    async Task ClickNode(string text)
    {
        var box = (await Label(text).BoundingBoxAsync())!;
        await Page.Mouse.ClickAsync(box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    [Test]
    public async Task Switching_Sample_Reloads_Graph_And_Suggests_Layout()
    {
        await Open("diagram");
        await Expect(Status).ToHaveTextAsync("10 nodes, 0 connections.");
        await Expect(Label("Dana Whitfield")).ToHaveCountAsync(1);

        await Picker("Sample").SelectOptionAsync("DecisionTree");

        await Expect(Status).ToHaveTextAsync("7 nodes, 8 connections.");
        await Expect(Picker("Layout")).ToHaveValueAsync("Layered");
        await Expect(Label("Ticket raised")).ToHaveCountAsync(1);
        await Expect(Label("Dana Whitfield")).ToHaveCountAsync(0);

        await Picker("Sample").SelectOptionAsync("MindMap");
        await Expect(Status).ToHaveTextAsync("13 nodes, 0 connections.");
        await Expect(Picker("Layout")).ToHaveValueAsync("MindMap");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clicking_A_Node_Selects_It()
    {
        await Open("diagram");
        await Expect(SelectedShapes).ToHaveCountAsync(0);

        await ClickNode("Platform");

        await Expect(Status).ToHaveTextAsync("1 node(s), 0 connection(s) selected.");
        await Expect(SelectedShapes).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Ctrl_A_Selects_Every_Node()
    {
        await Open("diagram");
        await ClickNode("Platform");
        await Expect(SelectedShapes).ToHaveCountAsync(1);

        await Surface.PressAsync("Control+a");

        await Expect(Status).ToHaveTextAsync(new Regex(@"^10 node\(s\)"));
        await Expect(SelectedShapes).ToHaveCountAsync(10);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Delete_Key_Removes_Node_And_Undo_Restores_It()
    {
        await Open("diagram");
        await Expect(Button("Undo")).ToBeDisabledAsync();

        await ClickNode("Platform");
        await Expect(SelectedShapes).ToHaveCountAsync(1);
        await Surface.PressAsync("Delete");

        await Expect(Label("Platform")).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync(new Regex(@"1 node\(s\)"));
        await Expect(Button("Undo")).ToBeEnabledAsync();

        await Button("Undo").ClickAsync();
        await Expect(Label("Platform")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Delete_Key_Does_Nothing_When_Disabled()
    {
        await Open("diagram");
        await Page.Locator("label.toggle-row", new() { HasTextString = "Delete key" }).Locator("input").UncheckAsync();

        await ClickNode("Platform");
        await Expect(SelectedShapes).ToHaveCountAsync(1);
        await Surface.PressAsync("Delete");
        await Page.WaitForTimeoutAsync(300);

        await Expect(Label("Platform")).ToHaveCountAsync(1);
        await Expect(Button("Undo")).ToBeDisabledAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Dragging_A_Node_Is_An_Undoable_Move()
    {
        await Open("diagram");
        var label = Label("Platform");
        var before = await label.GetAttributeAsync("x");
        var box = (await label.BoundingBoxAsync())!;
        var x = box.X + box.Width / 2;
        var y = box.Y + box.Height / 2;

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= 8; i++)
            await Page.Mouse.MoveAsync(x + i * 15, y + i * 10);
        await Page.Mouse.UpAsync();

        await Expect(Status).ToHaveTextAsync(new Regex("^Move"));
        await Expect(label).Not.ToHaveAttributeAsync("x", before!);
        await Expect(Button("Undo")).ToBeEnabledAsync();

        await Button("Undo").ClickAsync();
        await Expect(label).ToHaveAttributeAsync("x", before!);
        await Expect(Button("Redo")).ToBeEnabledAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Add_Node_And_Reset()
    {
        await Open("diagram");
        await Button("Add node").ClickAsync();

        await Expect(Label("New 1")).ToHaveCountAsync(1);
        await Expect(Status).ToHaveTextAsync(new Regex("^Added New 1"));

        await Button("Reset").ClickAsync();
        await Expect(Label("New 1")).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync("10 nodes, 0 connections.");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Grid_Toggle_Removes_The_Background_Grid()
    {
        await Open("diagram");
        var gridFill = Page.Locator(".shiny-diagram-canvas rect[fill^='url(#shiny-diagram-grid']");
        await Expect(gridFill).ToHaveCountAsync(1);

        await Page.Locator("label.toggle-row", new() { HasTextString = "Grid" }).Locator("input").UncheckAsync();
        await Expect(gridFill).ToHaveCountAsync(0);

        await Page.Locator("label.toggle-row", new() { HasTextString = "Grid" }).Locator("input").CheckAsync();
        await Expect(gridFill).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Json_Round_Trip_Keeps_The_Graph()
    {
        await Open("diagram");
        await Picker("Sample").SelectOptionAsync("Flowchart");
        await Expect(Label("Commit pushed")).ToHaveCountAsync(1);

        await Button("Save & reload JSON").ClickAsync();

        await Expect(Status).ToHaveTextAsync(new Regex(@"^Round-tripped [\d,]+ bytes of JSON\."));
        await Expect(Label("Commit pushed")).ToHaveCountAsync(1);
        await Expect(Label("Deploy")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }
}
