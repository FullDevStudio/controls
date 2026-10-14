using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class KanbanTests : BlazorTest
{
    ILocator Status => Page.Locator("p.status-message");
    ILocator Chevrons => Page.Locator(".shiny-kanban-head .shiny-kanban-chevron");
    ILocator Card(string id) => Page.Locator($"[data-kanban-card='{id}']");
    ILocator Cell(string columnId) => Page.Locator($"[data-kanban-cell][data-kanban-column='{columnId}']").First;
    ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });


    [Test]
    public async Task Column_Header_Shows_Count_Against_Wip_Limit()
    {
        await Open("kanban");

        var heads = Page.Locator(".shiny-kanban-head-count");
        await Expect(heads.Nth(0)).ToHaveTextAsync("2");       // Backlog: no limit
        await Expect(heads.Nth(2)).ToHaveTextAsync("2 / 2");   // In progress: at its limit of two
        await Expect(Page.Locator(".shiny-kanban-head-inner.is-over-limit")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Chevron_Collapses_And_Expands_One_Column()
    {
        await Open("kanban");
        await Expect(Card("k1")).ToBeVisibleAsync();

        await Chevrons.First.ClickAsync();

        await Expect(Chevrons.First).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(Cell("backlog")).ToHaveClassAsync(new Regex(@"\bis-collapsed\b"));
        await Expect(Card("k1")).ToBeHiddenAsync();
        // The other columns are untouched.
        await Expect(Chevrons.Nth(1)).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Card("k3")).ToBeVisibleAsync();

        await Chevrons.First.ClickAsync();
        await Expect(Chevrons.First).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Card("k1")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Collapse_And_Expand_All_Columns()
    {
        await Open("kanban");

        await Button("Collapse").ClickAsync();
        await Expect(Page.Locator(".shiny-kanban-head .shiny-kanban-chevron[aria-expanded='false']")).ToHaveCountAsync(5);
        await Expect(Page.Locator(".shiny-kanban-cell.is-collapsed")).ToHaveCountAsync(5);

        await Button("Expand").ClickAsync();
        await Expect(Page.Locator(".shiny-kanban-head .shiny-kanban-chevron[aria-expanded='true']")).ToHaveCountAsync(5);
        await Expect(Page.Locator(".shiny-kanban-cell.is-collapsed")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Swimlanes_Group_Cards_By_Team()
    {
        await Open("kanban");
        await Expect(Page.Locator(".shiny-kanban-swimlane")).ToHaveCountAsync(0);

        await Page.Locator("label.toggle-row", new() { HasText = "Swimlanes" }).Locator("input").CheckAsync();

        var titles = Page.Locator(".shiny-kanban-swimlane-title");
        await Expect(titles).ToHaveTextAsync(new[] { "Platform", "Mobile" });
        // Each lane gets its own row of cells: the platform card k1 and the mobile card k2 now live apart.
        await Expect(Page.Locator("[data-kanban-cell][data-kanban-column='backlog'][data-kanban-swimlane='platform'] [data-kanban-card='k1']")).ToHaveCountAsync(1);
        await Expect(Page.Locator("[data-kanban-cell][data-kanban-column='backlog'][data-kanban-swimlane='mobile'] [data-kanban-card='k2']")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Clicking_A_Card_Selects_It()
    {
        await Open("kanban");

        await Card("k3").Locator(".shiny-kanban-card-title").ClickAsync();
        await Expect(Card("k3")).ToHaveClassAsync(new Regex(@"\bis-selected\b"));

        // Keyboard selection is the same thing for screen reader users.
        await Card("k4").FocusAsync();
        await Card("k4").PressAsync("Enter");
        await Expect(Card("k4")).ToHaveClassAsync(new Regex(@"\bis-selected\b"));
        await Expect(Card("k3")).Not.ToHaveClassAsync(new Regex(@"\bis-selected\b"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Inline_Add_Card_Creates_A_Card()
    {
        await Open("kanban");
        var backlogCards = Cell("backlog").Locator("[data-kanban-card]");
        await Expect(backlogCards).ToHaveCountAsync(2);

        await Cell("backlog").Locator(".shiny-kanban-add").ClickAsync();
        var input = Cell("backlog").Locator(".shiny-kanban-add-input");
        await Expect(input).ToBeVisibleAsync();
        await input.FillAsync("Write the UI tests");
        await input.PressAsync("Enter");

        await Expect(Status).ToHaveTextAsync("Added a card to Backlog");
        await Expect(backlogCards).ToHaveCountAsync(3);
        await Expect(Cell("backlog").GetByText("Write the UI tests")).ToBeVisibleAsync();
        await Expect(Page.Locator(".shiny-kanban-head-count").First).ToHaveTextAsync("3");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Escape_Cancels_Inline_Add()
    {
        await Open("kanban");

        await Cell("backlog").Locator(".shiny-kanban-add").ClickAsync();
        var input = Cell("backlog").Locator(".shiny-kanban-add-input");
        await input.FillAsync("Never mind");
        await input.PressAsync("Escape");

        await Expect(input).ToHaveCountAsync(0);
        await Expect(Cell("backlog").Locator("[data-kanban-card]")).ToHaveCountAsync(2);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Dragging_A_Card_Moves_It_And_Undo_Puts_It_Back()
    {
        await Open("kanban");
        await Expect(Button("Undo")).ToBeDisabledAsync();

        await DragCard("k1", "ready");

        await Expect(Status).ToHaveTextAsync("Moved \"Audit the theme tokens\" to Ready");
        await Expect(Cell("ready").Locator("[data-kanban-card='k1']")).ToHaveCountAsync(1);
        await Expect(Cell("backlog").Locator("[data-kanban-card]")).ToHaveCountAsync(1);
        await Expect(Button("Undo")).ToBeEnabledAsync();

        await Button("Undo").ClickAsync();

        await Expect(Status).ToHaveTextAsync("Put \"Audit the theme tokens\" back in Backlog");
        await Expect(Cell("backlog").Locator("[data-kanban-card='k1']")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Move_Into_Done_Is_Cancelled_Unless_From_Review()
    {
        await Open("kanban");

        await DragCard("k1", "done");

        await Expect(Status).ToHaveTextAsync("\"Audit the theme tokens\" has to go through review first");
        await Expect(Cell("backlog").Locator("[data-kanban-card='k1']")).ToHaveCountAsync(1);
        await Expect(Cell("done").Locator("[data-kanban-card='k1']")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Block_At_Wip_Keeps_A_Card_Out_Of_A_Full_Column()
    {
        await Open("kanban");
        await Page.Locator("label.toggle-row", new() { HasText = "Block at WIP" }).Locator("input").CheckAsync();

        await DragCard("k1", "doing");

        await Expect(Cell("doing").Locator("[data-kanban-card]")).ToHaveCountAsync(2);
        await Expect(Cell("backlog").Locator("[data-kanban-card='k1']")).ToHaveCountAsync(1);
        await Expect(Button("Undo")).ToBeDisabledAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Block_At_Wip_Reports_The_Refused_Drop()
    {
        await Open("kanban");
        await Page.Locator("label.toggle-row", new() { HasText = "Block at WIP" }).Locator("input").CheckAsync();

        await DragCard("k1", "doing");

        await Expect(Status).ToHaveTextAsync("In progress is at its WIP limit");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Warn_At_Wip_Lets_The_Column_Go_Over_Its_Limit()
    {
        await Open("kanban");

        await DragCard("k1", "doing");

        await Expect(Status).ToHaveTextAsync("Moved \"Audit the theme tokens\" to In progress");
        await Expect(Page.Locator(".shiny-kanban-head-count").Nth(2)).ToHaveTextAsync("3 / 2");
        await Expect(Page.Locator(".shiny-kanban-head-inner.is-over-limit")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Reset_Restores_The_Board()
    {
        await Open("kanban");
        await Cell("backlog").Locator(".shiny-kanban-add").ClickAsync();
        await Cell("backlog").Locator(".shiny-kanban-add-input").FillAsync("Temporary");
        await Cell("backlog").Locator(".shiny-kanban-add-input").PressAsync("Enter");
        await Expect(Cell("backlog").Locator("[data-kanban-card]")).ToHaveCountAsync(3);

        await Button("Reset").ClickAsync();

        await Expect(Status).ToHaveTextAsync("Board reset");
        await Expect(Cell("backlog").Locator("[data-kanban-card]")).ToHaveCountAsync(2);
        await AssertNoConsoleErrors();
    }


    /// <summary>
    /// kanban.js drives drags from pointer events with a movement threshold, and the drag only
    /// starts once .NET has answered which lanes the card may enter - so the move is stepped.
    /// </summary>
    async Task DragCard(string cardId, string toColumnId)
    {
        var card = Card(cardId);
        await card.ScrollIntoViewIfNeededAsync();
        var from = await card.BoundingBoxAsync() ?? throw new InvalidOperationException("card has no box");
        var startX = from.X + from.Width / 2;
        var startY = from.Y + Math.Min(20, from.Height / 2);

        await Page.Mouse.MoveAsync(startX, startY);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(startX + 12, startY + 12, new() { Steps = 3 });
        await Page.WaitForTimeoutAsync(250);

        var to = await Cell(toColumnId).BoundingBoxAsync() ?? throw new InvalidOperationException("cell has no box");
        var endX = to.X + to.Width / 2;
        var endY = to.Y + Math.Min(to.Height - 10, 60);
        await Page.Mouse.MoveAsync(endX, endY, new() { Steps = 12 });
        await Page.WaitForTimeoutAsync(150);
        await Page.Mouse.MoveAsync(endX + 2, endY + 2);
        await Page.Mouse.UpAsync();
    }
}
