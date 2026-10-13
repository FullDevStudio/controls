using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shiny.Maui.Controls.Themes;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// A SecurityPin with no colours set drew cells with no outline and no fill: every colour defaulted to
/// null, and a null was simply not written, so nothing but an app-wide implicit Border style could make
/// the cells visible. (#31)
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class SecurityPinTests
{
    readonly ResourceDictionary theme = new BasicLightTheme();

    public SecurityPinTests()
    {
        TestDispatcherProvider.Install();
        _ = new Application();
    }


    SecurityPin Pin()
    {
        var pin = new SecurityPin { Length = 4 };
        pin.Resources.MergedDictionaries.Add(this.theme);
        return pin;
    }

    Color Token(string key) => this.theme[key] switch
    {
        Color color => color,
        SolidColorBrush brush => brush.Color,
        var other => throw new InvalidOperationException($"{key} is a {other?.GetType().Name}")
    };

    static List<Border> Cells(SecurityPin pin) => Find<Border>(pin).ToList();

    static IEnumerable<T> Find<T>(IVisualTreeElement element)
    {
        if (element is T match)
            yield return match;

        foreach (var child in element.GetVisualChildren())
        {
            foreach (var found in Find<T>(child))
                yield return found;
        }
    }

    static Color? StrokeOf(Border border) => (border.Stroke as SolidColorBrush)?.Color;


    [Fact]
    public void WithNoColoursSet_TheCellsFollowTheTheme()
    {
        var pin = Pin();
        var cells = Cells(pin);

        cells.Count.ShouldBe(4);
        foreach (var cell in cells)
        {
            StrokeOf(cell).ShouldBe(this.Token(ShinyThemeKeys.Brush.Outline));
            cell.BackgroundColor.ShouldBe(this.Token(ShinyThemeKeys.Color.SurfaceContainerLow));
        }

        foreach (var label in Find<Label>(pin).Where(x => x.Parent is Border))
            label.TextColor.ShouldBe(this.Token(ShinyThemeKeys.Color.OnSurface));
    }


    [Fact]
    public void AnExplicitColour_WinsOverTheTheme()
    {
        var pin = Pin();
        pin.CellBorderColor = Colors.Red;
        pin.CellBackgroundColor = Colors.Yellow;
        pin.CellTextColor = Colors.Blue;

        foreach (var cell in Cells(pin))
        {
            StrokeOf(cell).ShouldBe(Colors.Red);
            cell.BackgroundColor.ShouldBe(Colors.Yellow);
        }

        foreach (var label in Find<Label>(pin).Where(x => x.Parent is Border))
            label.TextColor.ShouldBe(Colors.Blue);
    }


    [Fact]
    public void ClearingAColour_HandsTheCellBackToTheTheme()
    {
        var pin = Pin();
        pin.CellBorderColor = Colors.Red;
        pin.CellBackgroundColor = Colors.Yellow;

        pin.CellBorderColor = null;
        pin.CellBackgroundColor = null;

        foreach (var cell in Cells(pin))
        {
            StrokeOf(cell).ShouldBe(this.Token(ShinyThemeKeys.Brush.Outline));
            cell.BackgroundColor.ShouldBe(this.Token(ShinyThemeKeys.Color.SurfaceContainerLow));
        }
    }


    [Fact]
    public void ChangingTheLength_BuildsThemedCells()
    {
        var pin = Pin();
        pin.Length = 6;

        var cells = Cells(pin);
        cells.Count.ShouldBe(6);
        StrokeOf(cells[5]).ShouldBe(this.Token(ShinyThemeKeys.Brush.Outline));
    }
}
