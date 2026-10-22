using Microsoft.Maui.Controls;
using Shouldly;
using Xunit;
using SwitchCell = Shiny.Maui.Controls.Cells.SwitchCell;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// A cell's tap recognizer is only there while the cell can be selected. On the AppKit head it would
/// otherwise take the click from the control inside a cell that ignores taps - a SwitchCell's switch.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class CellTapGestureTests
{
    [Fact]
    public void ASelectableCellHasATapRecognizer()
    {
        new Application();

        new SwitchCell().GestureRecognizers.OfType<TapGestureRecognizer>().Count().ShouldBe(1);
    }

    [Fact]
    public void ACellThatCantBeSelectedHasNoTapRecognizer()
    {
        new Application();

        new SwitchCell { IsSelectable = false }.GestureRecognizers.ShouldBeEmpty();
    }

    [Fact]
    public void TheRecognizerFollowsIsSelectable()
    {
        new Application();

        var cell = new SwitchCell { IsSelectable = false };
        cell.IsSelectable = true;
        cell.IsSelectable = true;
        cell.GestureRecognizers.OfType<TapGestureRecognizer>().Count().ShouldBe(1);

        cell.IsSelectable = false;
        cell.GestureRecognizers.ShouldBeEmpty();
    }
}
