using Microsoft.Maui.DevFlow.Driver;

namespace Sample.UITests.Features;

/// <summary>
/// DataGrid (datagrid), TreeDataGrid (treedatagrid), column formatting (datagridformatting) and
/// grouping/summary rows (datagridgrouping).
/// </summary>
/// <remarks>
/// <para>The grid wires its header, row, caret and group-header taps as TapGestureRecognizers on container
/// Grids, not on the Labels a test can find by text - <see cref="UiTree"/> finds a label and walks up
/// to the nearest ancestor that carries a tap gesture. Currency and dates assume the device runs en-US.</para>
/// <para>Every text lookup is scoped to its own grid or page with a selector: Shell keeps every visited
/// page alive, so "Ada" or "Engineering" also match the other DataGrid demo pages. The rows are a
/// virtualised CollectionView, so only the rows on screen exist - assertions stick to those, or scroll.</para>
/// <para>Shell also keeps each page (and its view-model) for the session, so a test that changes the
/// grid's state puts it back in a <c>finally</c>: the next test expects the page as it first loads.</para>
/// </remarks>
public class DataGridTests : UITest
{
    const string People = "#PeopleGrid";

    /// <summary>
    /// On the AppKit head the grid swaps in its row CollectionView after the page is realized, and a child
    /// added to a realized layout never gets a platform view there: the body paints empty and no row
    /// exists in the tree (devflow: every grid label under #PeopleGrid but the pager has nativeType null).
    /// </summary>
    const string AppKitRowsNeverRealize = "BUG: AppKit never realizes the DataGrid's rebuilt row CollectionView - the grid body is blank, no rows exist";

    async Task<List<ElementInfo>> Visible(string scope, string text, string type = "Label")
        => (await Driver.QuerySelector($"{scope} {type}[Text={MauiDevFlowDriver.Quote(text)}]")).Where(e => e.IsVisible).ToList();

    async Task<bool> HasText(string exact, string scope = People)
        => (await Visible(scope, exact)).Count > 0;

    async Task<int> CountText(string exact, string scope)
        => (await Visible(scope, exact)).Count;

    async Task TapButton(string exactText, string scope = People)
    {
        string? id = null;
        await Driver.WaitUntil(async () =>
        {
            id = (await Visible(scope, exactText, "Button")).FirstOrDefault()?.Id;
            return id != null;
        }, $"a '{exactText}' button in {scope}");
        await Driver.Tap(elementId: id);
    }

    // ---------------------------------------------------------------- datagrid

    async Task OpenGrid()
    {
        // sibling ShellContent switches don't present on AppKit (KnownGaps.AppKitSiblingShellContentSwitch)
        if (Platform == UIPlatform.MacOS)
            await Open("home");
        await Open("datagrid", "DataGridPage");
        // a run that died mid-test may have left the grid off its first page
        await Driver.WaitUntil(() => HasText("1-6 of 8"), "the pager's first page");
    }


    [Test, SkipOn(UIPlatform.MacOS, AppKitRowsNeverRealize)]
    public async Task Pager_Pages_Through_The_Rows()
    {
        await OpenGrid();
        await Assert.That(await HasText("Knuth")).IsFalse();

        await TapButton("›");
        try
        {
            await Driver.WaitUntil(() => HasText("7-8 of 8"), "the pager's last page");
            await Driver.WaitUntil(() => HasText("Knuth"), "row seven on the second page");
        }
        finally
        {
            await TapButton("«");
        }
        await Driver.WaitUntil(() => HasText("1-6 of 8"), "back to the first page");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task PageSize_Change_Repages()
    {
        await OpenGrid();
        try
        {
            await Driver.SetProperty("PageSize", "4", automationId: "PeopleGrid");
            await Driver.WaitUntil(() => HasText("1-4 of 8"), "four rows a page");
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.SetProperty("PageSize", "6", automationId: "PeopleGrid");
        }
        await Driver.WaitUntil(() => HasText("1-6 of 8"), "six rows a page again");
    }


    [Test]
    public async Task Freeze_Switch_Unpins_The_First_Column()
    {
        await OpenGrid();
        await Driver.WaitForProperty("PeopleGrid", "FrozenColumns", "1");

        await Driver.Tap("FreezeColumnSwitch");
        try
        {
            await Driver.WaitForProperty("PeopleGrid", "FrozenColumns", "0");
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.Tap("FreezeColumnSwitch");
        }
        await Driver.WaitForProperty("PeopleGrid", "FrozenColumns", "1");
    }


    [Test, SkipOn(UIPlatform.MacOS, AppKitRowsNeverRealize)]
    public async Task Header_Tap_Sorts_Ascending_Then_Descending()
    {
        await OpenGrid();
        // Page one in source order holds Hopper (52) but not Liskov (49).
        await Driver.WaitUntil(() => HasText("Hopper"), "the unsorted first page");
        await Assert.That(await HasText("Liskov")).IsFalse();

        async Task TapAge() => await (await UiTree.Capture(Driver, "DataGridPage")).TapNearestTappable(Driver, "Age");

        await TapAge();
        try
        {
            // Youngest six: Liskov (49) is in, Hopper (52) and Knuth (58) are out.
            await Driver.WaitUntil(async () => await HasText("Liskov") && !await HasText("Hopper"), "ascending by age");

            await TapAge();
            // Oldest six: Knuth (58) is in, Lovelace (36) is out.
            await Driver.WaitUntil(async () => await HasText("Knuth") && !await HasText("Lovelace"), "descending by age");
        }
        finally
        {
            // the third tap clears the sort (Ascending → Descending → None)
            await TapAge();
        }
        await Driver.WaitUntil(async () => await HasText("Hopper") && !await HasText("Liskov"), "source order again");
        await AssertNoAppErrors();
    }


    [Test, SkipOn(UIPlatform.MacOS, AppKitRowsNeverRealize)]
    public async Task Row_Tap_Opens_The_Editor_And_Cancel_Closes_It()
    {
        await OpenGrid();
        await Driver.WaitUntil(() => HasText("Lovelace"), "the first page");

        // EditMode="Form" with the default EditTrigger (OnRowClick): a tap edits rather than selects.
        await StartEditing("Lovelace"); // the row's editors and the Save/Cancel bar

        await TapButton("Cancel");
        // iOS's CollectionView keeps the swapped-out edit cell (and its Entries) in the tree, so
        // "closed" is the edit actions bar hiding again rather than the editors leaving the tree.
        await Driver.WaitUntil(async () => !await EditBarShown(), "the editors to close");
        await Driver.WaitUntil(() => HasText("Lovelace"), "the row unchanged");
        await AssertNoAppErrors();
    }


    /// <summary>The Save/Cancel bar is up: the button and every ancestor (the bar is a hidden Border) visible.</summary>
    async Task<bool> EditBarShown()
    {
        var tree = await UiTree.Capture(Driver, "DataGridPage");
        return tree.FindAll("Save").Any(tree.IsShown);
    }

    /// <summary>
    /// Taps the row showing <paramref name="text"/> until its editors are up. iOS's CollectionView
    /// keeps recycled row views (same text, same bounds) in the tree after a reload, and a tap on one
    /// of those starts nothing - so each candidate row is tried in turn.
    /// </summary>
    async Task<string> StartEditing(string text)
    {
        var tree = await UiTree.Capture(Driver, "DataGridPage");
        var rows = tree.FindAll(text).Where(e => e.Type == "Label" && tree.IsShown(e)).Select(tree.NearestTappable).DistinctBy(r => r.Id).ToList();
        foreach (var row in rows)
        {
            await Driver.Tap(elementId: row.Id);
            string? entry = null;
            try
            {
                await Driver.WaitUntil(async () =>
                {
                    entry = (await Driver.QuerySelector($"{People} Entry")).FirstOrDefault(e => e.Text == text && e.IsVisible)?.Id;
                    return entry != null && await EditBarShown();
                }, $"the editor for '{text}'", 4);
                return entry!;
            }
            catch (TimeoutException)
            {
            }
        }
        throw new TimeoutException($"No row showing '{text}' (of {rows.Count} candidates) opened its editors");
    }

    async Task EditLastName(string from, string to)
    {
        var entry = await StartEditing(from);
        await Driver.Clear(elementId: entry);
        await Driver.Fill(to, elementId: entry);
        await TapButton("Save");
        await Driver.WaitUntil(async () => await HasText(to) && !await EditBarShown(), $"'{to}' saved into the row");
    }


    [Test, SkipOn(UIPlatform.MacOS, AppKitRowsNeverRealize)]
    public async Task Edit_And_Save_Commits_To_The_Item()
    {
        await OpenGrid();
        await Driver.WaitUntil(() => HasText("Lovelace"), "the first page");

        await EditLastName("Lovelace", "Byron");
        try
        {
            await AssertNoAppErrors();
        }
        finally
        {
            await EditLastName("Byron", "Lovelace");
        }
    }


    [Test, SkipOn(UIPlatform.MacOS, AppKitRowsNeverRealize)]
    public async Task Detail_Caret_Loads_And_Shows_The_Breakdown()
    {
        await OpenGrid();
        await Driver.WaitUntil(() => HasText("Lovelace"), "the first page");

        // The caret sits in the row's leading expander cell, which has its own tap gesture.
        await TapCaret("Lovelace", "▸", () => HasText("Breakdown for Ada"));
        try
        {
            // RowDetailLoader takes ~900ms, then the template binds the value it filled in.
            await Driver.WaitUntil(() => HasText("Recent activity: 11 approvals this quarter"), "the loaded activity", timeoutSeconds: 10);
            await AssertNoAppErrors();
        }
        finally
        {
            await TapCaret("Lovelace", "▾", async () => !await HasText("Breakdown for Ada"));
        }
        await Driver.WaitUntil(async () => !await HasText("Breakdown for Ada"), "the detail row to collapse");
    }


    /// <summary>Taps the caret in the row showing <paramref name="rowText"/> - trying each candidate row, see <see cref="StartEditing"/>.</summary>
    async Task TapCaret(string rowText, string caret, Func<Task<bool>> done)
    {
        var tree = await UiTree.Capture(Driver, "DataGridPage");
        var rows = tree.FindAll(rowText).Where(e => e.Type == "Label" && tree.IsShown(e)).Select(tree.NearestTappable).DistinctBy(r => r.Id).ToList();
        foreach (var row in rows)
        {
            var glyph = tree.FindAll(caret).FirstOrDefault(c => tree.IsShown(c) && tree.IsWithinPublic(c, row));
            if (glyph == null)
                continue;
            await tree.TapNearestTappable(Driver, glyph);
            try
            {
                await Driver.WaitUntil(done, $"the {caret} caret on '{rowText}' to take effect", 10);
                return;
            }
            catch (TimeoutException)
            {
            }
        }
        throw new TimeoutException($"No '{caret}' caret on a row showing '{rowText}' took effect (of {rows.Count} candidates)");
    }


    // ---------------------------------------------------------------- tree data grid

    const string Accounts = "#AccountsGrid";

    /// <summary>
    /// DataGrid and TreeDataGrid are sibling ShellContents in one FlyoutItem; on AppKit Shell never presents
    /// one reached straight from the other (<see cref="KnownGaps.AppKitSiblingShellContentSwitch"/>), so leave
    /// the section first - these tests are about the grid, not Shell.
    /// </summary>
    async Task OpenTree()
    {
        if (Platform == UIPlatform.MacOS)
            await Open("home");
        await Open("treedatagrid", "TreeDataGridPage");
    }

    [Test, SkipOn(UIPlatform.Linux, KnownGaps.LinuxSiblingShellContentUnreachable)]
    public async Task Tree_Caret_Expands_And_Collapses_A_Branch()
    {
        await OpenTree();
        await Driver.WaitUntil(() => HasText("Engineering", Accounts), "the root accounts");
        await Assert.That(await HasText("Platform", Accounts)).IsFalse();

        var tree = await UiTree.Capture(Driver, "TreeDataGridPage");
        await tree.TapNearestTappable(Driver, tree.First("▸", within: tree.NearestTappable(tree.First("Engineering"))));
        try
        {
            await Driver.WaitUntil(async () => await HasText("Platform", Accounts) && await HasText("Mobile", Accounts), "Engineering's teams");
        }
        finally
        {
            tree = await UiTree.Capture(Driver, "TreeDataGridPage");
            await tree.TapNearestTappable(Driver, tree.First("▾", within: tree.NearestTappable(tree.First("Engineering"))));
        }
        await Driver.WaitUntil(async () => !await HasText("Platform", Accounts), "the branch to collapse");
        await AssertNoAppErrors();
    }


    [Test, SkipOn(UIPlatform.Linux, KnownGaps.LinuxSiblingShellContentUnreachable)]
    public async Task Tree_Lazy_Branch_Loads_Its_Children()
    {
        await OpenTree();
        await Driver.WaitUntil(() => HasText("Field operations", Accounts), "the lazy account");

        var tree = await UiTree.Capture(Driver, "TreeDataGridPage");
        await tree.TapNearestTappable(Driver, tree.First("▸", within: tree.NearestTappable(tree.First("Field operations"))));
        try
        {
            await Driver.WaitForProperty("TreeStatus", "Text", "Loaded Field operations");
            await Driver.WaitUntil(async () => await HasText("Northern region", Accounts) && await HasText("Southern region", Accounts), "the loaded regions");
            await AssertNoAppErrors();
        }
        finally
        {
            tree = await UiTree.Capture(Driver, "TreeDataGridPage");
            await tree.TapNearestTappable(Driver, tree.First("▾", within: tree.NearestTappable(tree.First("Field operations"))));
        }
    }


    // ---------------------------------------------------------------- formatting

    const string Formatting = "DataGridFormattingPage";

    /// <summary>Scrolls the n-th grid on the formatting page so row <paramref name="itemIndex"/> is realised.</summary>
    async Task ScrollFormattingGrid(int grid, int itemIndex)
    {
        var list = await Driver.FindBySelector($"{Formatting} CollectionView", index: grid, timeoutSeconds: 10);
        await Driver.Scroll(elementId: list.Id, itemIndex: itemIndex);
    }

    [Test, SkipOn(UIPlatform.MacOS, "datagridformatting page not in the macOS head"), SkipOn(UIPlatform.Linux, "datagridformatting page not in the Linux head")]
    public async Task Formatting_Presets_Render_Without_Templates()
    {
        await Open("datagridformatting", "DataGridFormattingPage");

        await Driver.WaitUntil(() => HasText("$142,000", Formatting), "Currency with Decimals=0");

        // Grace (row 2) is inactive: no review date. Each grid shows ~2 rows, so bring her into view.
        await ScrollFormattingGrid(0, 2);
        await Driver.WaitUntil(() => HasText("Signed off", Formatting), "Enum using its [Description]");
        await Driver.WaitUntil(() => HasText("—", Formatting), "NullText for a missing review date");

        await ScrollFormattingGrid(1, 2);
        await Driver.WaitUntil(() => HasText("Overdue", Formatting), "the second grid's NullText");
    }


    [Test, SkipOn(UIPlatform.MacOS, "datagridformatting page not in the macOS head"), SkipOn(UIPlatform.Linux, "datagridformatting page not in the Linux head")]
    public async Task Formatting_TextFormatter_Maps_Values()
    {
        await Open("datagridformatting", "DataGridFormattingPage");

        // AgeBand: under 40 Junior, under 50 Senior, otherwise Principal. Ada 36, Alan 41, Grace 52.
        await ScrollFormattingGrid(0, 2);
        await Driver.WaitUntil(async () => await HasText("Junior", Formatting) && await HasText("Senior", Formatting) && await HasText("Principal", Formatting),
            "every age band");
    }


    [Test, SkipOn(UIPlatform.MacOS, "datagridformatting page not in the macOS head"), SkipOn(UIPlatform.Linux, "datagridformatting page not in the Linux head")]
    public async Task Formatting_CellStyle_Bolds_Low_Salaries()
    {
        await Open("datagridformatting", "DataGridFormattingPage");
        await Driver.WaitUntil(() => HasText("$98,000", Formatting), "Alan's salary");

        // SalaryStyle bolds anyone under 100,000 - in the second grid only, so exactly one $98,000 is bold.
        async Task<int> Bold(string text)
        {
            var count = 0;
            foreach (var label in await Visible(Formatting, text))
            {
                if ((await Driver.GetProperty("FontAttributes", elementId: label.Id))?.Contains("Bold") == true)
                    count++;
            }
            return count;
        }

        await Driver.WaitUntil(async () => await Bold("$98,000") == 1, "the low salary styled bold");
        await Assert.That(await Bold("$142,000")).IsEqualTo(0);
    }


    // ---------------------------------------------------------------- grouping

    // Groups sort by key: Research (Donald, West) comes first, then Sales and Support. The upper grid
    // is only a couple of rows tall, so Research and its nested region are what is on screen.
    const string Sales = "#SalesGrid";
    const string Regions = "#RegionGrid";

    Task OpenGrouping() => Open("datagridgrouping", "DataGridGroupingPage");


    [Test, SkipOn(UIPlatform.MacOS, "datagridgrouping page not in the macOS head"), SkipOn(UIPlatform.Linux, "datagridgrouping page not in the Linux head")]
    public async Task Grouping_Starts_Two_Levels_Deep()
    {
        await OpenGrouping();

        await Driver.WaitUntil(() => HasText("Department: Research (1)", Sales), "the outer department group");
        await Driver.WaitUntil(() => HasText("Region: West (1)", Sales), "the region group nested inside it");
        // The lower grid groups by region only: four each.
        await Driver.WaitUntil(async () => await HasText("Region: East (4)", Regions) && await HasText("Region: West (4)", Regions),
            "the second grid's region groups");
    }


    [Test, SkipOn(UIPlatform.MacOS, "datagridgrouping page not in the macOS head"), SkipOn(UIPlatform.Linux, "datagridgrouping page not in the Linux head")]
    public async Task Grouping_Buttons_Regroup_The_Bound_Grid()
    {
        await OpenGrouping();
        await Driver.WaitUntil(() => HasText("Region: West (1)", Sales), "the nested region groups");

        try
        {
            await Driver.Tap("GroupByDepartmentButton");
            await Driver.WaitUntil(async () => await HasText("Department: Research (1)", Sales) && !await HasText("Region: West (1)", Sales),
                "a single grouping level");

            await Driver.Tap("UngroupButton");
            await Driver.WaitUntil(async () => !await HasText("Department: Research (1)", Sales), "no grouping");
        }
        finally
        {
            await Driver.Tap("GroupByDepartmentRegionButton");
        }
        await Driver.WaitUntil(async () => await HasText("Department: Research (1)", Sales) && await HasText("Region: West (1)", Sales),
            "both levels again");
        await AssertNoAppErrors();
    }


    [Test, SkipOn(UIPlatform.MacOS, "datagridgrouping page not in the macOS head"), SkipOn(UIPlatform.Linux, "datagridgrouping page not in the Linux head")]
    public async Task Grouping_Summary_Placement_Cycles()
    {
        await OpenGrouping();
        await Driver.WaitForProperty("SalesGrid", "GroupSummaryPlacement", "Footer");

        try
        {
            await Driver.Tap("SummaryPlacementButton");
            await Driver.WaitForProperty("SalesGrid", "GroupSummaryPlacement", "Header");
            await Driver.WaitForProperty("SummaryPlacementButton", "Text", "Summaries: Header");

            await Driver.Tap("SummaryPlacementButton");
            await Driver.WaitForProperty("SalesGrid", "GroupSummaryPlacement", "Both");
            await AssertNoAppErrors();
        }
        finally
        {
            // and round again to the page's starting placement
            for (var i = 0; i < 3 && await Driver.GetProperty("GroupSummaryPlacement", automationId: "SalesGrid") != "Footer"; i++)
            {
                await Driver.Tap("SummaryPlacementButton");
                await Task.Delay(500);
            }
        }
        await Driver.WaitForProperty("SalesGrid", "GroupSummaryPlacement", "Footer");
    }


    [Test, SkipOn(UIPlatform.MacOS, "datagridgrouping page not in the macOS head"), SkipOn(UIPlatform.Linux, "datagridgrouping page not in the Linux head")]
    public async Task Grouping_Header_Tap_Collapses_And_Expands()
    {
        await OpenGrouping();
        await Driver.WaitUntil(() => HasText("Region: West (1)", Sales), "Research's region group");

        // Upper grid: collapsing Department: Research hides the region group nested in it.
        await (await UiTree.Capture(Driver, "DataGridGroupingPage")).TapNearestTappable(Driver, "Department: Research (1)");
        try
        {
            await Driver.WaitUntil(async () => !await HasText("Region: West (1)", Sales), "Research to collapse");
        }
        finally
        {
            await (await UiTree.Capture(Driver, "DataGridGroupingPage")).TapNearestTappable(Driver, "Department: Research (1)");
        }
        await Driver.WaitUntil(() => HasText("Region: West (1)", Sales), "Research to expand again");

        // Lower grid starts collapsed (GroupsInitiallyExpanded=False): East's reps appear only once it opens.
        await Assert.That(await HasText("Alan", Regions)).IsFalse();
        await (await UiTree.Capture(Driver, "DataGridGroupingPage")).TapNearestTappable(Driver, "Region: East (4)");
        try
        {
            await Driver.WaitUntil(() => HasText("Alan", Regions), "East to expand in the lower grid");
        }
        finally
        {
            await (await UiTree.Capture(Driver, "DataGridGroupingPage")).TapNearestTappable(Driver, "Region: East (4)");
        }
        await Driver.WaitUntil(async () => !await HasText("Alan", Regions), "East to collapse again");
        await AssertNoAppErrors();
    }


    /// <summary>
    /// One capture of the visual tree, indexed so a label can be walked up to its tappable container.
    /// Scoped to one page's subtree, since Shell keeps the other DataGrid pages (same names) alive.
    /// </summary>
    sealed class UiTree
    {
        readonly Dictionary<string, ElementInfo> byId = new();
        readonly List<ElementInfo> order = new();
        ElementInfo? root;

        public static async Task<UiTree> Capture(MauiDevFlowDriver driver, string pageType)
        {
            var tree = new UiTree();
            tree.Add(await driver.Tree(80));
            tree.root = tree.order.FirstOrDefault(e => e.Type == pageType)
                ?? throw new InvalidOperationException($"'{pageType}' is not in the visual tree");
            return tree;
        }

        void Add(IEnumerable<ElementInfo>? items)
        {
            foreach (var e in items ?? [])
            {
                this.byId[e.Id] = e;
                this.order.Add(e);
                this.Add(e.Children);
            }
        }

        ElementInfo? Parent(ElementInfo e)
            => e.ParentId != null && this.byId.TryGetValue(e.ParentId, out var p) ? p : null;

        bool IsWithin(ElementInfo e, ElementInfo ancestor)
        {
            for (var p = this.Parent(e); p != null; p = this.Parent(p))
            {
                if (p.Id == ancestor.Id)
                    return true;
            }
            return false;
        }

        /// <summary>The first visible element (tree order) on the page whose text is exactly <paramref name="text"/>.</summary>
        public ElementInfo First(string text, ElementInfo? within = null)
            => this.order.FirstOrDefault(e => e.Text == text && e.IsVisible && this.IsWithin(e, within ?? this.root!))
                ?? throw new InvalidOperationException($"No visible element with text '{text}'" + (within == null ? " on the page" : " inside the given container"));

        public bool IsWithinPublic(ElementInfo e, ElementInfo ancestor) => this.IsWithin(e, ancestor);

        public IEnumerable<ElementInfo> FindAll(string text)
            => this.order.Where(e => e.Text == text && this.IsWithin(e, this.root!));

        /// <summary>The element and every ancestor are visible - a hidden container hides its children.</summary>
        public bool IsShown(ElementInfo e)
        {
            // up to the page: Shell's own items (FlyoutItem, ShellContent) report invisible
            for (ElementInfo? current = e; current != null; current = this.Parent(current))
            {
                if (!current.IsVisible)
                    return false;
                if (current.Id == this.root!.Id)
                    return true;
            }
            return false;
        }

        public ElementInfo NearestTappable(ElementInfo e)
        {
            for (ElementInfo? current = e; current != null; current = this.Parent(current))
            {
                if (current.Gestures?.Contains("tap") == true)
                    return current;
            }
            throw new InvalidOperationException($"No ancestor of '{e.Text}' ({e.Type}) has a tap gesture");
        }

        public Task TapNearestTappable(MauiDevFlowDriver driver, string text)
            => this.TapNearestTappable(driver, this.First(text));

        public Task TapNearestTappable(MauiDevFlowDriver driver, ElementInfo e)
            => driver.Tap(elementId: this.NearestTappable(e).Id);
    }
}
