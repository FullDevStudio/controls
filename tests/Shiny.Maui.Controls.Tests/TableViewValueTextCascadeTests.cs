using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shiny.Maui.Controls.Cells;
using Shouldly;
using Xunit;
using TvTableSection = Shiny.Maui.Controls.Sections.TableSection;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// The table's CellValueText* styles cascade to cells that leave their own unset. ApplyCascadedStyles
/// used to re-apply title/description/hint but never the value-text styles, so a LabelCell only picked
/// them up from its own ValueText* propertyChanged — which never fires while the property is unset.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class TableViewValueTextCascadeTests
{
    sealed class ProbeLabelCell : LabelCell
    {
        public Label Value => ValueLabel;
    }

    static (TableView Table, ProbeLabelCell Cell) Build(Action<TableView> configure)
    {
        new Application();

        var table = new TableView();
        configure(table);

        var cell = new ProbeLabelCell { Title = "Row", ValueText = "value" };
        var section = new TvTableSection("Section");
        section.Cells.Add(cell);
        table.Root.Sections.Add(section);
        return (table, cell);
    }

    [Fact]
    public void TableValueStyles_ReachACellThatSetsNone()
    {
        var (_, cell) = Build(t =>
        {
            t.CellValueTextColor = Colors.Blue;
            t.CellValueTextFontSize = 21;
            t.CellValueTextFontAttributes = FontAttributes.Bold;
        });

        cell.Value.TextColor.ShouldBe(Colors.Blue);
        cell.Value.FontSize.ShouldBe(21);
        cell.Value.FontAttributes.ShouldBe(FontAttributes.Bold);
    }

    [Fact]
    public void TableValueStyles_ChangedLater_StillCascade()
    {
        var (table, cell) = Build(_ => { });

        table.CellValueTextColor = Colors.Red;
        table.CellValueTextFontSize = 19;

        cell.Value.TextColor.ShouldBe(Colors.Red);
        cell.Value.FontSize.ShouldBe(19);
    }

    [Fact]
    public void CellOwnValueStyle_StillBeatsTheTable()
    {
        new Application();

        var table = new TableView { CellValueTextFontSize = 16, CellValueTextColor = Colors.Blue };
        var cell = new ProbeLabelCell { Title = "Row", ValueText = "v", ValueTextFontSize = 11, ValueTextColor = Colors.Green };
        var section = new TvTableSection("Section");
        section.Cells.Add(cell);
        table.Root.Sections.Add(section);

        cell.Value.FontSize.ShouldBe(11);
        cell.Value.TextColor.ShouldBe(Colors.Green);
    }
}
