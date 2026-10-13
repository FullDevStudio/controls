using Microsoft.Maui.Controls;
using Shiny.Maui.Controls.Cells;
using Shouldly;
using Xunit;
using TvTableSection = Shiny.Maui.Controls.Sections.TableSection;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// A section's HeaderView and FooterView are the consumer's own instances, and every render adds them
/// to a brand-new section layout. Only the cells used to be detached first, so the views were still
/// children of the outgoing layout when the next render added them again — Android ended the app with
/// "The specified child already has a parent", Windows with a COMException from UIElementCollection.Add.
/// Any re-render did it: a styling property, a cell collection change, a bound section property.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class TableViewSectionHeaderFooterViewTests
{
    static (TableView Table, TvTableSection Section) Build(View? header, View? footer)
    {
        new Application();

        var table = new TableView();
        var section = new TvTableSection("Section") { HeaderView = header, FooterView = footer };
        section.Cells.Add(new LabelCell { Title = "Row" });
        table.Root.Sections.Add(section);
        return (table, section);
    }

    static Layout SectionLayout(TableView table) =>
        ((VerticalStackLayout)table.ScrollContent).Children.OfType<Layout>().Single();

    [Fact]
    public void ReRender_DetachesFooterViewFromTheOutgoingSectionLayout()
    {
        var footer = new Label { Text = "Footer" };
        var (table, _) = Build(null, footer);
        var before = SectionLayout(table);
        before.Children.ShouldContain(footer);

        table.ShowSectionSeparator = !table.ShowSectionSeparator;

        var after = SectionLayout(table);
        after.ShouldNotBeSameAs(before);
        before.Children.ShouldNotContain(footer);
        after.Children.ShouldContain(footer);
        footer.Parent.ShouldBeSameAs(after);
    }

    [Fact]
    public void ReRender_DetachesHeaderViewFromTheOutgoingSectionLayout()
    {
        var header = new Label { Text = "Header" };
        var (table, _) = Build(header, null);
        var before = SectionLayout(table);

        table.ShowSectionSeparator = !table.ShowSectionSeparator;

        var after = SectionLayout(table);
        before.Children.ShouldNotContain(header);
        after.Children.ShouldContain(header);
        header.Parent.ShouldBeSameAs(after);
    }

    [Fact]
    public void CellCollectionChange_KeepsTheFooterViewInExactlyOneLayout()
    {
        var footer = new Label { Text = "Footer" };
        var (table, section) = Build(null, footer);
        var before = SectionLayout(table);

        section.Cells.Add(new LabelCell { Title = "Another row" });

        before.Children.ShouldNotContain(footer);
        SectionLayout(table).Children.ShouldContain(footer);
    }

    /// <summary>
    /// A hidden footer is left out of the new layout, but it was in the old one - it still has to come
    /// out, or showing it again re-adds a view that already has a parent.
    /// </summary>
    [Fact]
    public void HidingTheFooter_DetachesItFromTheOutgoingLayout()
    {
        var footer = new Label { Text = "Footer" };
        var (table, section) = Build(null, footer);
        var before = SectionLayout(table);

        section.FooterVisible = false;

        before.Children.ShouldNotContain(footer);
        SectionLayout(table).Children.ShouldNotContain(footer);

        section.FooterVisible = true;

        SectionLayout(table).Children.ShouldContain(footer);
    }
}
