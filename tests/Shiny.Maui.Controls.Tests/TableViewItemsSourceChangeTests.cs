using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Shiny.Maui.Controls.Cells;
using Shiny.Maui.Controls.Infrastructure;
using Shouldly;
using Xunit;
using TvTableSection = Shiny.Maui.Controls.Sections.TableSection;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// A section's <c>ItemsSource</c> - and the table's own, which generates whole sections - used to be built again from
/// scratch on every change: every item got a new cell from the template, and the table drew every section again. Filling
/// a collection one item at a time therefore cost n²/2 cells, which on a real page meant seconds to open it on Android.
/// An Add, Remove, Replace or Move now touches only the rows it names; a Reset still builds them all again.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class TableViewItemsSourceChangeTests
{
    public TableViewItemsSourceChangeTests() => TestDispatcherProvider.Install();

    [Fact]
    public void AddingItemsOneByOneCreatesOneCellEach()
    {
        var created = 0;
        var items = new ObservableCollection<string>();
        var (table, _) = Build(items, () => created++);

        for (var i = 0; i < 100; i++)
            items.Add($"item {i}");

        created.ShouldBe(100, "one cell per item - building every cell again on each Add made it 5,050");
        Items(DrawnRows(table)).ShouldBe(items);
    }

    [Fact]
    public void AnItemInsertedInTheMiddleLeavesTheOtherRowsInPlace()
    {
        var created = 0;
        var items = new ObservableCollection<string> { "a", "b", "c" };
        var (table, _) = Build(items, () => created++);
        var layout = SectionLayout(table);
        var before = DrawnRows(table);

        items.Insert(1, "x");

        created.ShouldBe(4);
        SectionLayout(table).ShouldBeSameAs(layout, "the section is not drawn again");
        var after = DrawnRows(table);
        Items(after).ShouldBe(new[] { "a", "x", "b", "c" });
        after[0].ShouldBeSameAs(before[0]);
        after[2].ShouldBeSameAs(before[1]);
        after[3].ShouldBeSameAs(before[2]);
    }

    [Fact]
    public void RemoveReplaceAndMoveTouchOnlyTheRowsTheyName()
    {
        var created = 0;
        var items = new ObservableCollection<string> { "a", "b", "c", "d" };
        var (table, _) = Build(items, () => created++);
        var layout = SectionLayout(table);
        var cells = DrawnRows(table).ToDictionary(x => (string)x.BindingContext);

        items.Remove("b");
        Items(DrawnRows(table)).ShouldBe(new[] { "a", "c", "d" });

        items[1] = "z";
        Items(DrawnRows(table)).ShouldBe(new[] { "a", "z", "d" });

        items.Move(0, 2);
        var rows = DrawnRows(table);
        Items(rows).ShouldBe(new[] { "z", "d", "a" });

        created.ShouldBe(5, "only the replacement needs a new cell");
        rows[1].ShouldBeSameAs(cells["d"]);
        rows[2].ShouldBeSameAs(cells["a"], "a moved item keeps its cell");
        SectionLayout(table).ShouldBeSameAs(layout, "the section is not drawn again");
        cells["b"].Parent.ShouldBeNull("a row that left is taken out of the section");
        cells["c"].Parent.ShouldBeNull("a replaced row is taken out of the section");
    }

    /// <summary>
    /// A cell left inside its old <c>DragSortRow</c> is still a child of it, and adding it again ends the app on
    /// Android ("The specified child already has a parent") and on WinUI.
    /// </summary>
    [Fact]
    public void AMovedDragSortRowLeavesItsOldRowBehind()
    {
        var items = new ObservableCollection<string> { "a", "b", "c" };
        var (table, _) = Build(items, () => { }, useDragSort: true);
        var moved = DrawnRows(table)[0];
        var oldRow = moved.Parent.ShouldBeOfType<DragSortRow>();

        items.Move(0, 2);

        var rows = DrawnRows(table);
        Items(rows).ShouldBe(new[] { "b", "c", "a" });
        rows[2].ShouldBeSameAs(moved, "a moved item keeps its cell");
        var newRow = moved.Parent.ShouldBeOfType<DragSortRow>();
        newRow.ShouldNotBeSameAs(oldRow);
        newRow.Parent.ShouldBeSameAs(SectionLayout(table));
        oldRow.Children.ShouldNotContain(moved);
        SectionLayout(table).Children.OfType<DragSortRow>().Count().ShouldBe(3);
    }

    [Fact]
    public void AHiddenRowStaysOutWithoutLeavingASeparatorBehind()
    {
        new Application();
        var items = new ObservableCollection<Item> { new("a", true), new("b", true) };
        var section = new TvTableSection("Items")
        {
            ItemTemplate = new DataTemplate(() =>
            {
                var cell = new LabelCell();
                cell.SetBinding(VisualElement.IsVisibleProperty, nameof(Item.Visible));
                return cell;
            }),
            ItemsSource = items,
        };
        var table = new TableView();
        table.Root.Sections.Add(section);

        items.Insert(1, new Item("hidden", false));
        items.Add(new Item("c", true));

        DrawnRows(table).Select(x => ((Item)x.BindingContext).Name).ShouldBe(new[] { "a", "b", "c" });
        section.GetAllCells()[1].Parent.ShouldBeNull("a hidden row is never put in");
    }

    [Fact]
    public void CellIdsAreRenumberedAfterAnInsert()
    {
        var items = new ObservableCollection<string> { "a", "b", "c" };
        var (table, _) = Build(items, () => { });
        table.AutomationId = "Settings";

        items.Insert(0, "x");

        var rows = DrawnRows(table);
        Items(rows).ShouldBe(new[] { "x", "a", "b", "c" });
        rows.Select(x => x.AutomationId).ShouldBe(new[]
        {
            "Settings_Cell_0_0", "Settings_Cell_0_1", "Settings_Cell_0_2", "Settings_Cell_0_3"
        });
    }

    [Fact]
    public void AResetStillBuildsEveryRowAgain()
    {
        var created = 0;
        var items = new ObservableCollection<string> { "a", "b" };
        var (table, _) = Build(items, () => created++);

        items.Clear();
        DrawnRows(table).ShouldBeEmpty();

        items.Add("c");
        Items(DrawnRows(table)).ShouldBe(new[] { "c" });
        created.ShouldBe(3);
    }

    [Fact]
    public void GeneratedSectionsAreCreatedOncePerItem()
    {
        var created = 0;
        var items = new ObservableCollection<string>();
        var table = BuildGenerated(items, () => created++);

        items.Add("s0");
        var first = DrawnSections(table).Single();
        for (var i = 1; i < 20; i++)
            items.Add($"s{i}");

        created.ShouldBe(20, "one section per item - building every section again on each Add made it 210");
        var drawn = DrawnSections(table);
        drawn.Select(x => (string)x.BindingContext).ShouldBe(items);
        drawn[0].ShouldBeSameAs(first, "a section already on screen is not drawn again");
    }

    [Fact]
    public void GeneratedSectionsFollowARemoveAndAMove()
    {
        var created = 0;
        var items = new ObservableCollection<string> { "a", "b", "c", "d" };
        var table = BuildGenerated(items, () => created++);
        var views = DrawnSections(table).ToDictionary(x => (string)x.BindingContext);

        items.Remove("b");
        items.Move(0, 2);

        var drawn = DrawnSections(table);
        drawn.Select(x => (string)x.BindingContext).ShouldBe(new[] { "c", "d", "a" });
        drawn[0].ShouldBeSameAs(views["c"]);
        drawn[2].ShouldBeSameAs(views["a"], "a moved section keeps its view");
        created.ShouldBe(4);
    }


    static (TableView Table, TvTableSection Section) Build(ObservableCollection<string> items, Action created, bool useDragSort = false)
    {
        new Application();

        var section = new TvTableSection("Items")
        {
            UseDragSort = useDragSort,
            ItemTemplate = new DataTemplate(() =>
            {
                created();
                return new LabelCell();
            }),
            ItemsSource = items,
        };
        var table = new TableView();
        table.Root.Sections.Add(section);
        return (table, section);
    }

    static TableView BuildGenerated(ObservableCollection<string> items, Action created)
    {
        new Application();

        return new TableView
        {
            ItemTemplate = new DataTemplate(() =>
            {
                created();
                var section = new TvTableSection();
                section.Cells.Add(new LabelCell { Title = "Row" });
                return section;
            }),
            ItemsSource = items,
        };
    }

    static Layout SectionLayout(TableView table) =>
        ((VerticalStackLayout)table.ScrollContent).Children.OfType<Layout>().Single();

    static string[] Items(IEnumerable<CellBase> rows) => rows.Select(x => (string)x.BindingContext).ToArray();

    /// <summary>The section's rows in drawn order, after checking there is exactly one separator between every two of them.</summary>
    static List<CellBase> DrawnRows(TableView table)
    {
        var rows = new List<CellBase>();
        var expectRow = true;
        foreach (var child in SectionLayout(table).Children)
        {
            if (child is BoxView)
            {
                expectRow.ShouldBeFalse("a separator with no row above it, or two together");
                expectRow = true;
            }
            else if ((child is DragSortRow drag ? drag.Cell : child) is CellBase cell)
            {
                expectRow.ShouldBeTrue("two rows with no separator between them");
                rows.Add(cell);
                expectRow = false;
            }
        }

        if (rows.Count > 0)
            expectRow.ShouldBeFalse("a separator under the last row");
        return rows;
    }

    /// <summary>The table's section views in drawn order, after checking there is exactly one separator between every two of them.</summary>
    static List<View> DrawnSections(TableView table)
    {
        var sections = new List<View>();
        var expectSection = true;
        foreach (var child in ((VerticalStackLayout)table.ScrollContent).Children)
        {
            if (child is BoxView)
            {
                expectSection.ShouldBeFalse("a section separator with no section above it, or two together");
                expectSection = true;
            }
            else
            {
                expectSection.ShouldBeTrue("two sections with no separator between them");
                sections.Add((View)child);
                expectSection = false;
            }
        }

        if (sections.Count > 0)
            expectSection.ShouldBeFalse("a section separator under the last section");
        return sections;
    }

    public record Item(string Name, bool Visible);
}
