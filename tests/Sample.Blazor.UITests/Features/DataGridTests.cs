using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class DataGridTests : BlazorTest
{
    ILocator Status => Page.Locator("p.status-message").First;

    ILocator Section(string heading) => Page.Locator("section.demo-section", new()
    {
        Has = Page.Locator("h2", new() { HasTextRegex = new Regex($"^{Regex.Escape(heading)}$") })
    });

    ILocator Main => Section("Full-featured data grid");
    ILocator MainRows => Main.Locator("tbody tr.shiny-dg-row");
    ILocator Header(ILocator scope, string text) => scope.Locator("th.shiny-dg-header", new()
    {
        Has = Page.Locator(".shiny-dg-header-text", new() { HasTextRegex = new Regex($"^{Regex.Escape(text)}$") })
    });

    /// <summary>The text of one column (1-based <paramref name="cell"/> of each data row, check column included).</summary>
    static async Task<string[]> Column(ILocator rows, int cell)
        => (await rows.Locator($"td:nth-child({cell})").AllInnerTextsAsync()).Select(t => t.Trim()).ToArray();


    // ---------------------------------------------------------------- full-featured grid

    [Test]
    public async Task Header_Click_Sorts_Ascending_Then_Descending()
    {
        await Open("datagrid");
        var age = Header(Main, "Age");

        await age.Locator(".shiny-dg-header-text").ClickAsync();
        await Expect(age.Locator(".shiny-dg-sort-icon")).ToHaveTextAsync("▲");
        var ascending = (await Column(MainRows, 4)).Select(int.Parse).ToArray();
        await Assert.That(ascending).IsEquivalentTo(ascending.OrderBy(a => a).ToArray());
        await Expect(MainRows.First.Locator("td").Nth(2)).ToHaveTextAsync("Lovelace");

        await age.Locator(".shiny-dg-header-text").ClickAsync();
        await Expect(age.Locator(".shiny-dg-sort-icon")).ToHaveTextAsync("▼");
        await Expect(MainRows.First.Locator("td").Nth(2)).ToHaveTextAsync("Knuth");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Pager_Rows_Per_Page_And_Navigation()
    {
        await Open("datagrid");
        var pager = Main.Locator(".shiny-dg-pager");
        await Expect(pager.Locator(".shiny-dg-pager-range")).ToHaveTextAsync("1-8 of 8");

        await pager.Locator("select").SelectOptionAsync("5");
        await Expect(pager.Locator(".shiny-dg-pager-range")).ToHaveTextAsync("1-5 of 8");
        await Expect(MainRows).ToHaveCountAsync(5);

        await pager.GetByRole(AriaRole.Button, new() { Name = "Next" }).ClickAsync();
        await Expect(pager.Locator(".shiny-dg-pager-range")).ToHaveTextAsync("6-8 of 8");
        await Expect(MainRows).ToHaveCountAsync(3);
        await Expect(pager.GetByRole(AriaRole.Button, new() { Name = "Next" })).ToBeDisabledAsync();

        await pager.GetByRole(AriaRole.Button, new() { Name = "First" }).ClickAsync();
        await Expect(pager.Locator(".shiny-dg-pager-range")).ToHaveTextAsync("1-5 of 8");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Row_Checkboxes_Select_Multiple_Rows()
    {
        await Open("datagrid");

        await MainRows.Nth(0).Locator("td.shiny-dg-checkcol input").CheckAsync();
        await MainRows.Nth(2).Locator("td.shiny-dg-checkcol input").CheckAsync();

        await Expect(Status).ToHaveTextAsync("2 selected");
        await Expect(Main.Locator("tr.shiny-dg-selected")).ToHaveCountAsync(2);

        // The header checkbox selects everything.
        await Main.Locator("thead th.shiny-dg-checkcol input").CheckAsync();
        await Expect(Status).ToHaveTextAsync("8 selected");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Row_Click_Edits_In_A_Form_And_Save_Commits()
    {
        await Open("datagrid");

        await MainRows.First.Locator("td.shiny-dg-cell").Nth(1).ClickAsync();
        var editors = Main.Locator("input.shiny-dg-editor");
        await Expect(editors.First).ToBeVisibleAsync();
        await Expect(Main.Locator(".shiny-dg-edit-save")).ToBeVisibleAsync();

        var last = editors.Nth(1);
        await Expect(last).ToHaveValueAsync("Lovelace");
        await last.FillAsync("Byron");
        await Main.Locator(".shiny-dg-edit-save").ClickAsync();

        await Expect(Status).ToHaveTextAsync("Saved Ada Byron");
        await Expect(Main.Locator("input.shiny-dg-editor")).ToHaveCountAsync(0);
        await Expect(MainRows.First.Locator("td").Nth(2)).ToHaveTextAsync("Byron");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Edit_Cancel_Discards_Changes()
    {
        await Open("datagrid");

        await MainRows.First.Locator("td.shiny-dg-cell").Nth(1).ClickAsync();
        await Main.Locator("input.shiny-dg-editor").Nth(1).FillAsync("Byron");
        await Main.Locator(".shiny-dg-edit-cancel").ClickAsync();

        await Expect(Main.Locator("input.shiny-dg-editor")).ToHaveCountAsync(0);
        await Expect(MainRows.First.Locator("td").Nth(2)).ToHaveTextAsync("Lovelace");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Filter_Menu_Narrows_The_Rows()
    {
        await Open("datagrid");
        var department = Header(Main, "Department");

        await department.Locator(".shiny-dg-filter-icon").ClickAsync();
        var menu = department.Locator(".shiny-dg-filter-menu");
        await Expect(menu).ToBeVisibleAsync();
        await menu.Locator("input[type=text]").FillAsync("Research");
        await menu.GetByRole(AriaRole.Button, new() { Name = "Apply" }).ClickAsync();

        await Expect(MainRows).ToHaveCountAsync(2);
        await Expect(department.Locator(".shiny-dg-filter-icon")).ToHaveClassAsync(new Regex(@"\bshiny-dg-filter-active\b"));
        await Assert.That((await Column(MainRows, 5)).Distinct()).IsEquivalentTo(new[] { "Research" });

        await department.Locator(".shiny-dg-filter-icon").ClickAsync();
        await department.Locator(".shiny-dg-filter-menu").GetByRole(AriaRole.Button, new() { Name = "Clear" }).ClickAsync();
        await Expect(MainRows).ToHaveCountAsync(8);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Group_Icon_Groups_By_A_Column()
    {
        await Open("datagrid");
        var department = Header(Main, "Department");

        await department.Locator(".shiny-dg-group-icon").ClickAsync();

        await Expect(department.Locator(".shiny-dg-group-icon")).ToHaveClassAsync(new Regex(@"\bshiny-dg-group-active\b"));
        var groups = Main.Locator("tr.shiny-dg-group-header");
        await Expect(groups).ToHaveCountAsync(4);   // Engineering, Research, Mathematics, Software
        await Expect(groups.First.Locator(".shiny-dg-group-count")).ToHaveTextAsync("(2)");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Option_Toggles_Restyle_The_Grid()
    {
        await Open("datagrid");
        var grid = Main.Locator(".shiny-dg").First;

        await Page.Locator("label.toggle-row", new() { HasText = "Dense" }).Locator("input").CheckAsync();
        await Expect(grid).ToHaveClassAsync(new Regex(@"\bshiny-dg-dense\b"));

        await Page.Locator("label.toggle-row", new() { HasText = "Striped" }).Locator("input").UncheckAsync();
        await Expect(grid).Not.ToHaveClassAsync(new Regex(@"\bshiny-dg-striped\b"));

        await Page.Locator("label.toggle-row", new() { HasText = "Loading" }).Locator("input").CheckAsync();
        await Expect(grid.Locator(".shiny-dg-loading")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Selection_Mode_None_Removes_Checkboxes()
    {
        await Open("datagrid");
        await Expect(Main.Locator("td.shiny-dg-checkcol").First).ToBeVisibleAsync();

        await Page.Locator("label", new() { HasText = "Selection" }).Locator("select").SelectOptionAsync("None");

        await Expect(Main.Locator(".shiny-dg-checkcol")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    // ---------------------------------------------------------------- frozen columns

    [Test]
    public async Task Frozen_Column_Count_Follows_The_Selects()
    {
        await Open("datagrid");
        var section = Section("Frozen columns + sticky header");
        var frozenStartHeaders = section.Locator("th.shiny-dg-frozen-start");
        await Expect(frozenStartHeaders).ToHaveCountAsync(1);
        await Expect(section.Locator("th.shiny-dg-frozen-end")).ToHaveCountAsync(1);

        await section.Locator("label", new() { HasText = "Frozen at start" }).Locator("select").SelectOptionAsync("2");
        await Expect(frozenStartHeaders).ToHaveCountAsync(2);

        await section.Locator("label", new() { HasText = "Frozen at end" }).Locator("select").SelectOptionAsync("0");
        await Expect(section.Locator("th.shiny-dg-frozen-end")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Frozen_Column_Stays_Put_While_The_Grid_Scrolls()
    {
        await Open("datagrid");
        var section = Section("Frozen columns + sticky header");
        var scroller = section.Locator(".shiny-dg-scroll");
        var frozen = section.Locator("th.shiny-dg-frozen-start").First;
        var plain = Header(section, "Department");
        double frozenBefore = (await frozen.BoundingBoxAsync())!.X;
        double plainBefore = (await plain.BoundingBoxAsync())!.X;

        await scroller.EvaluateAsync("e => e.scrollLeft = 400");

        await Page.WaitForFunctionAsync("e => e.scrollLeft > 0", await scroller.ElementHandleAsync());
        double frozenAfter = (await frozen.BoundingBoxAsync())!.X;
        double plainAfter = (await plain.BoundingBoxAsync())!.X;
        await Assert.That(Math.Abs(frozenAfter - frozenBefore)).IsLessThan(1.0);
        await Assert.That(plainAfter).IsLessThan(plainBefore - 100);
        await AssertNoConsoleErrors();
    }


    // ---------------------------------------------------------------- resize / reorder

    [Test]
    public async Task Dragging_A_Header_Edge_Resizes_Within_Its_Bounds()
    {
        await Open("datagrid");
        var section = Section("Column resizing (min / max width)");
        var first = Header(section, "First");
        var handle = first.Locator(".shiny-dg-resize-handle");
        await handle.ScrollIntoViewIfNeededAsync();
        var box = (await handle.BoundingBoxAsync())!;
        var x = box.X + box.Width / 2;
        var y = box.Y + box.Height / 2;

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x + 40, y, new() { Steps = 5 });
        await Page.Mouse.UpAsync();

        await Expect(section.Locator(".status-message")).ToHaveTextAsync(new Regex(@"^FirstName → \d+px$"));

        // MaxWidth 260px: a big drag clamps.
        box = (await handle.BoundingBoxAsync())!;
        x = box.X + box.Width / 2;
        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        // as far right as the screen goes (on a phone that is well short of +400, but still past the cap)
        await Page.Mouse.MoveAsync(Math.Min(x + 400, Page.ViewportSize!.Width - 4), y, new() { Steps = 8 });
        await Page.Mouse.UpAsync();
        await Expect(section.Locator(".status-message")).ToHaveTextAsync("FirstName → 260px");
        await AssertNoConsoleErrors();
    }


    // ---------------------------------------------------------------- formatting

    [Test]
    public async Task Formatting_Presets_Render_Without_Templates()
    {
        await Open("datagrid");
        var section = Section("Column formatting");
        var ada = section.Locator("tbody tr.shiny-dg-row").First;

        await Expect(ada.Locator("td").Nth(1)).ToHaveTextAsync("$142,000");   // Currency, Decimals=0
        await Expect(ada.Locator("td").Nth(2)).ToHaveTextAsync("61%");        // Percent of 0.61
        await Expect(ada.Locator("td").Nth(5)).ToHaveTextAsync("Signed off"); // Enum [Description]
        await Expect(ada.Locator("td").Nth(8)).ToHaveTextAsync("Junior");     // TextFormatter
        await Expect(ada.Locator("td").Nth(7)).ToHaveTextAsync(new Regex(@"^\d+ yrs$")); // Suffix
        // NullText stands in for an inactive person's missing review date.
        await Expect(section.Locator("tbody td", new() { HasTextRegex = new Regex("^—$") }).First).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task CellStyle_Flags_Rows_From_Their_Data()
    {
        await Open("datagrid");
        var section = Section("Conditional cell styling & wrapping");
        var rows = section.Locator("tbody tr.shiny-dg-row");

        // Ada earns 142,000 (< 150,000): bold. Alan earns 158,000: not.
        static async Task<int> Weight(ILocator cell)
            => int.Parse(await cell.EvaluateAsync<string>("e => getComputedStyle(e).fontWeight"));

        var adaSalary = rows.Nth(0).Locator("td").Nth(1);
        var alanSalary = rows.Nth(1).Locator("td").Nth(1);
        await Expect(adaSalary).ToHaveTextAsync("$142,000");
        await Assert.That(await Weight(adaSalary)).IsGreaterThan(await Weight(alanSalary));

        // Grace is inactive with no review: "Overdue", in bold too.
        var graceReview = rows.Nth(2).Locator("td").Nth(2);
        await Expect(graceReview).ToHaveTextAsync("Overdue");
        await Assert.That(await Weight(graceReview)).IsGreaterThan(await Weight(rows.Nth(0).Locator("td").Nth(2)));
        await AssertNoConsoleErrors();
    }


    // ---------------------------------------------------------------- grouping & summary

    [Test]
    public async Task Grouping_Buttons_Regroup_And_Summaries_Follow()
    {
        await Open("datagrid");
        var section = Section("Grouping & summary rows");
        var groups = section.Locator("tr.shiny-dg-group-header");
        await Expect(groups).ToHaveCountAsync(4);

        await section.GetByRole(AriaRole.Button, new() { Name = "Dept + Region", Exact = true }).ClickAsync();
        await Expect(groups).Not.ToHaveCountAsync(4);
        var nested = await groups.CountAsync();
        await Assert.That(nested).IsGreaterThan(4);

        await section.GetByRole(AriaRole.Button, new() { Name = "Ungroup", Exact = true }).ClickAsync();
        await Expect(groups).ToHaveCountAsync(0);
        // The grid-level summary rows remain: Total and Average.
        await Expect(section.Locator("tfoot td", new() { HasTextRegex = new Regex("^Total$") })).ToHaveCountAsync(1);
        await Expect(section.Locator("tfoot td", new() { HasTextRegex = new Regex("^Average$") })).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Group_Header_Click_Collapses_Its_Rows()
    {
        await Open("datagrid");
        var section = Section("Grouping & summary rows");
        var first = section.Locator("tr.shiny-dg-group-header").First;
        var rows = section.Locator("tbody tr.shiny-dg-row");
        var before = await rows.CountAsync();

        await first.ClickAsync();

        await Expect(first.Locator(".shiny-dg-group-caret")).ToHaveTextAsync("▸");
        await Expect(rows).ToHaveCountAsync(before - 2);   // Engineering's two rows
        await first.ClickAsync();
        await Expect(rows).ToHaveCountAsync(before);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Group_Summary_Placement_Moves_The_Totals()
    {
        await Open("datagrid");
        var section = Section("Grouping & summary rows");
        await Expect(section.Locator("tr.shiny-dg-group-footer").First).ToBeAttachedAsync();

        await section.Locator("label", new() { HasText = "Group summaries" }).Locator("select").SelectOptionAsync("None");

        await Expect(section.Locator("tr.shiny-dg-group-footer")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    // ---------------------------------------------------------------- highlighting

    [Test]
    public async Task Highlight_Toggles_Add_And_Remove_Washes()
    {
        await Open("datagrid");
        var section = Section("Highlighting rows, columns & cells");
        // A wash is a background-image laid behind the text, so stripes and selection still show.
        var washed = section.Locator("tbody td[style*='background-image']");
        var rows = section.Locator("tbody tr.shiny-dg-row");
        await Expect(rows.Nth(0).Locator("td").Nth(2)).ToHaveAttributeAsync("style", new Regex("background-image"));   // Salary column
        await Expect(rows.Nth(0).Locator("td").Nth(0)).Not.ToHaveAttributeAsync("style", new Regex("background-image")); // active row

        await section.GetByLabel("Highlight the Salary column").UncheckAsync();
        await Expect(rows.Nth(0).Locator("td").Nth(2)).Not.ToHaveAttributeAsync("style", new Regex("background-image"));
        // Grace (inactive) is still washed as a row.
        await Expect(rows.Nth(2).Locator("td").Nth(0)).ToHaveAttributeAsync("style", new Regex("background-image"));

        await section.GetByLabel("Highlight inactive rows").UncheckAsync();
        await Expect(washed).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Highlight_Ring_Boxes_One_Cell()
    {
        await Open("datagrid");
        var section = Section("Highlighting rows, columns & cells");
        var adaDepartment = section.Locator("tbody tr.shiny-dg-row").First.Locator("td").Nth(1);
        var before = await adaDepartment.GetAttributeAsync("style") ?? "";

        await section.GetByLabel("Ring one cell").CheckAsync();

        await Expect(adaDepartment).Not.ToHaveAttributeAsync("style", before);
        await Expect(adaDepartment).ToHaveAttributeAsync("style", new Regex("3px"));
        await AssertNoConsoleErrors();
    }


    // ---------------------------------------------------------------- detail rows

    [Test]
    public async Task Detail_Caret_Loads_Then_Shows_The_Breakdown()
    {
        await Open("datagrid");
        var section = Section("Detail (breakdown) rows");

        await section.Locator(".shiny-dg-expander").First.ClickAsync();

        await Expect(section.GetByText("Breakdown for Ada Lovelace")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(section.GetByText("Recent activity: 11 approvals this quarter")).ToBeVisibleAsync();
        await Expect(Status).ToHaveTextAsync("Opened Ada");
        await Expect(section.Locator(".shiny-dg-expander").First).ToHaveTextAsync("▾");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Detail_Expand_Mode_Single_Keeps_One_Open()
    {
        await Open("datagrid");
        var section = Section("Detail (breakdown) rows");
        await section.Locator("label", new() { HasText = "Expand mode" }).Locator("select").SelectOptionAsync("Single");

        await section.Locator(".shiny-dg-expander").Nth(0).ClickAsync();
        await Expect(section.GetByText("Breakdown for Ada Lovelace")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await section.Locator(".shiny-dg-expander").Nth(1).ClickAsync();
        await Expect(section.GetByText("Breakdown for Alan Turing")).ToBeVisibleAsync(new() { Timeout = 10_000 });

        await Expect(section.GetByText("Breakdown for Ada Lovelace")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    // ---------------------------------------------------------------- tree grid

    ILocator TreeSection => Section("Tree grid");
    ILocator TreeRow(string name) => TreeSection.Locator("tbody tr.shiny-dg-row", new()
    {
        Has = Page.Locator("td", new() { HasTextRegex = new Regex($"^\\s*[▸▾]?\\s*{Regex.Escape(name)}\\s*$") })
    });


    [Test]
    public async Task Tree_Caret_Expands_And_Collapses()
    {
        await Open("datagrid");
        await Expect(TreeSection.Locator("tbody tr.shiny-dg-row")).ToHaveCountAsync(3);

        await TreeRow("Engineering").Locator(".shiny-dg-tree-caret").ClickAsync();
        await Expect(TreeSection.Locator("tbody tr.shiny-dg-row")).ToHaveCountAsync(5);
        await Expect(TreeRow("Platform")).ToHaveCountAsync(1);
        await Expect(TreeRow("Engineering").Locator(".shiny-dg-tree-caret")).ToHaveTextAsync("▾");

        await TreeRow("Engineering").Locator(".shiny-dg-tree-caret").ClickAsync();
        await Expect(TreeSection.Locator("tbody tr.shiny-dg-row")).ToHaveCountAsync(3);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Tree_Lazy_Branch_Loads_Its_Children()
    {
        await Open("datagrid");

        await TreeRow("Field operations").Locator(".shiny-dg-tree-caret").ClickAsync();

        await Expect(TreeRow("Northern region")).ToHaveCountAsync(1, new() { Timeout = 10_000 });
        await Expect(TreeRow("Southern region")).ToHaveCountAsync(1);
        await Expect(TreeRow("Field operations").Locator(".shiny-dg-tree-caret")).ToHaveTextAsync("▾");
        await Expect(Page.Locator("p.status-message")).ToHaveTextAsync("Loaded Field operations");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Tree_Quick_Filter_Keeps_Matching_Ancestors()
    {
        await Open("datagrid");

        await TreeSection.Locator("input.shiny-dg-quickfilter").FillAsync("Tooling");

        // Tooling is two levels down, so its ancestor Engineering stays; Research has no match and goes.
        // The lazy branch has unknown children, so it stays too.
        await Expect(TreeRow("Research")).ToHaveCountAsync(0);
        await Expect(TreeRow("Engineering")).ToHaveCountAsync(1);
        await Expect(TreeRow("Field operations")).ToHaveCountAsync(1);

        await TreeRow("Engineering").Locator(".shiny-dg-tree-caret").ClickAsync();
        await Expect(TreeRow("Platform")).ToHaveCountAsync(1);
        await Expect(TreeRow("Mobile")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }
}
