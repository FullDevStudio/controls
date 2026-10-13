using Microsoft.Maui.Controls;
using Shiny.Maui.Controls.Cells;
using Shouldly;
using Xunit;
using TvTableSection = Shiny.Maui.Controls.Sections.TableSection;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// A fixed cell declared inside a TableView ItemTemplate section bound to the page's view model, not the
/// section's item: the section layout inherited the TableView's BindingContext and handed it to the cell.
/// https://github.com/shinyorg/controls/issues/23
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class TableViewTemplatedSectionContextTests
{
    public TableViewTemplatedSectionContextTests() => TestDispatcherProvider.Install();

    [Fact]
    public void FixedCellInTemplatedSectionBindsToItem()
    {
        new Application();
        var items = new[] { new NamedModel("ITEM 1"), new NamedModel("ITEM 2") };
        var table = BuildTable(items);

        var sections = table.GetAllSections();
        sections.Count.ShouldBe(2);
        for (var i = 0; i < items.Length; i++)
        {
            var cell = (LabelCell)sections[i].Cells[0];
            cell.BindingContext.ShouldBe(items[i]);
            cell.Title.ShouldBe(items[i].Name);
        }
    }

    [Fact]
    public void FixedCellKeepsItemWhenTableContextChanges()
    {
        new Application();
        var items = new[] { new NamedModel("ITEM 1") };
        var table = BuildTable(items);

        table.BindingContext = new NamedModel("OTHER page view model");

        var cell = (LabelCell)table.GetAllSections()[0].Cells[0];
        cell.Title.ShouldBe("ITEM 1");
    }

    [Fact]
    public void StaticSectionCellStillFollowsTableContext()
    {
        new Application();
        var cell = new LabelCell();
        cell.SetBinding(CellBase.TitleProperty, "Name");
        var section = new TvTableSection();
        section.Cells.Add(cell);
        var table = new TableView { BindingContext = new NamedModel("PAGE") };
        table.Root.Sections.Add(section);

        cell.Title.ShouldBe("PAGE");
        table.BindingContext = new NamedModel("PAGE 2");
        cell.Title.ShouldBe("PAGE 2");
    }

    static TableView BuildTable(NamedModel[] items) => new()
    {
        BindingContext = new NamedModel("PAGE view model"),
        ItemsSource = items,
        ItemTemplate = new DataTemplate(() =>
        {
            var section = new TvTableSection();
            section.SetBinding(TvTableSection.TitleProperty, "Name");
            var cell = new LabelCell();
            cell.SetBinding(CellBase.TitleProperty, "Name");
            section.Cells.Add(cell);
            return section;
        })
    };

    public record NamedModel(string Name);
}
