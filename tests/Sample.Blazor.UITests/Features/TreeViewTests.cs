using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class TreeViewTests : BlazorTest
{
    ILocator Row(string name)
        => Page.Locator(".shiny-treeview-row").Filter(new()
        {
            Has = Page.Locator(".shiny-treeview-content span", new() { HasTextRegex = new Regex("^" + Regex.Escape(name) + "$") })
        });

    /// <summary>The treeitem &lt;li&gt; that owns the row (carries aria-expanded / aria-selected).</summary>
    ILocator Item(string name) => Row(name).Locator("xpath=..");

    ILocator Chevron(string name) => Row(name).Locator(".shiny-treeview-chevron");
    ILocator Status => Page.Locator("p.status");
    ILocator Toolbar(string text) => Page.Locator(".toolbar").GetByRole(AriaRole.Button, new() { Name = text, Exact = true });


    [Test]
    public async Task Chevron_Expands_And_Collapses_A_Folder()
    {
        await Open("treeview");
        await Expect(Row("Projects")).ToHaveCountAsync(0);

        await Chevron("Documents").ClickAsync();
        await Expect(Item("Documents")).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Row("Projects")).ToBeVisibleAsync();
        await Expect(Status).ToHaveTextAsync("Expanded: Documents");

        await Chevron("Documents").ClickAsync();
        await Expect(Item("Documents")).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(Row("Projects")).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync("Collapsed: Documents");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Clicking_A_Row_Selects_Only_That_Row()
    {
        await Open("treeview");

        await Row("todo.txt").ClickAsync();
        await Expect(Item("todo.txt")).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Status).ToHaveTextAsync("Selected: todo.txt");

        await Row("Downloads").ClickAsync();
        await Expect(Item("Downloads")).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Item("todo.txt")).ToHaveAttributeAsync("aria-selected", "false");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Locked_Rows_Cannot_Be_Selected()
    {
        await Open("treeview");
        await Chevron("Documents").ClickAsync();

        await Row("secret.key").ClickAsync();
        await Expect(Item("secret.key")).ToHaveAttributeAsync("aria-selected", "false");
        await Expect(Status).Not.ToHaveTextAsync("Selected: secret.key");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Lazy_Branch_Loads_Its_Children_On_Expand()
    {
        await Open("treeview");
        await Expect(Row("remote-backup.zip")).ToHaveCountAsync(0);

        await Chevron("Cloud (lazy)").ClickAsync();
        await Expect(Row("remote-backup.zip")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(Row("shared")).ToBeVisibleAsync();
        await Expect(Item("Cloud (lazy)")).ToHaveAttributeAsync("aria-expanded", "true");

        // and the nested lazy level
        await Chevron("shared").ClickAsync();
        await Expect(Row("team-notes.md")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Expand_All_Then_Collapse_All()
    {
        await Open("treeview");

        await Toolbar("Expand all").ClickAsync();
        await Expect(Row("Program.cs")).ToBeVisibleAsync();
        await Expect(Row("photo.png")).ToBeVisibleAsync();
        await Expect(Row("budget.xlsx")).ToBeVisibleAsync(new() { Timeout = 10_000 });   // both lazy levels

        await Toolbar("Collapse all").ClickAsync();
        await Expect(Row("Program.cs")).ToHaveCountAsync(0);
        await Expect(Row("Documents")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Multi_Select_All_Checks_Every_Selectable_Node()
    {
        await Open("treeview");
        await Expect(Toolbar("Select all")).ToHaveCountAsync(0);

        await Toolbar("Expand all").ClickAsync();
        await Expect(Row("budget.xlsx")).ToBeVisibleAsync(new() { Timeout = 10_000 });

        await Page.Locator(".toolbar label", new() { HasTextString = "Multi-select" }).Locator("input").CheckAsync();
        await Toolbar("Select all").ClickAsync();

        // 19 nodes once both lazy levels are loaded, minus the locked secret.key.
        await Expect(Status).ToHaveTextAsync("18 checked");
        await Expect(Item("todo.txt")).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Item("secret.key")).ToHaveAttributeAsync("aria-selected", "false");

        await Toolbar("Deselect all").ClickAsync();
        await Expect(Status).ToHaveTextAsync("Selection cleared");
        await Expect(Page.Locator("li.shiny-treeview-item[aria-selected=true]")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Keyboard_Navigates_Expands_And_Selects()
    {
        await Open("treeview");
        var tree = Page.Locator(".shiny-treeview");
        await tree.FocusAsync();

        await tree.PressAsync("ArrowDown");          // focuses the first node
        await Expect(Row("Documents")).ToHaveClassAsync(new Regex(@"\bfocused\b"));

        await tree.PressAsync("ArrowRight");         // expands it
        await Expect(Item("Documents")).ToHaveAttributeAsync("aria-expanded", "true");

        await tree.PressAsync("ArrowRight");         // into the first child
        await Expect(Row("Projects")).ToHaveClassAsync(new Regex(@"\bfocused\b"));

        await tree.PressAsync("Enter");
        await Expect(Item("Projects")).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Status).ToHaveTextAsync("Selected: Projects");

        await tree.PressAsync("ArrowLeft");          // to the parent
        await tree.PressAsync("ArrowLeft");          // collapses it
        await Expect(Item("Documents")).ToHaveAttributeAsync("aria-expanded", "false");
        await AssertNoConsoleErrors();
    }
}
