using Microsoft.Maui.Controls;
using Shiny.Maui.Controls.Infrastructure;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// The suggestion list floats in the page overlay instead of being a row of the control: laid out
/// inline it was a ScrollView inside the form's ScrollView (two scrollers on one axis) and it pushed
/// the rest of the form down each time it opened.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class AutoCompleteDropDownTests
{
    public AutoCompleteDropDownTests()
    {
        TestDispatcherProvider.Install();
        _ = new Application();
    }

    static AutoCompleteEntry Build() => new()
    {
        ItemsSource = new List<string> { "Apple", "Apricot", "Banana" },
        AutomationId = "Fruit"
    };


    [Fact]
    public void OnAPageTheSuggestionsFloatInTheOverlay()
    {
        var control = Build();
        var page = new ContentPage
        {
            Content = new ScrollView { Content = new VerticalStackLayout { Children = { control } } }
        };

        control.SearchNow("ap");

        control.IsDropDownOpen.ShouldBeTrue();
        var layer = PageOverlay.GetOrCreateRoot(page).Children.OfType<PageOverlay.AutoCompleteLayer>().Single();
        control.DropDown.Parent.ShouldBeSameAs(layer);

        // Nothing scrollable is left inside the control, so it cannot nest in the page's scroller.
        control.GetVisualTreeDescendants().OfType<ScrollView>().ShouldBeEmpty();
        control.DropDown.AutomationId.ShouldBe("Fruit_DropDown");
        control.DropDown.GetVisualTreeDescendants().OfType<Label>().Select(l => l.Text).ShouldBe(["Apple", "Apricot"]);
    }


    [Fact]
    public void ClosingTakesTheListOutOfTheOverlay()
    {
        var control = Build();
        var page = new ContentPage { Content = new VerticalStackLayout { Children = { control } } };

        control.SearchNow("ap");
        control.SearchNow("zzz"); // no matches closes it

        control.IsDropDownOpen.ShouldBeFalse();
        PageOverlay.GetOrCreateRoot(page).Children.OfType<PageOverlay.AutoCompleteLayer>().Single().Children.ShouldBeEmpty();
    }


    [Fact]
    public void OffAPageItFallsBackToTheInlineRow()
    {
        var control = Build();
        _ = new VerticalStackLayout { Children = { control } };

        control.SearchNow("ban");

        control.IsDropDownOpen.ShouldBeTrue();
        control.GetVisualTreeDescendants().ShouldContain(control.DropDown);
        control.DropDown.IsVisible.ShouldBeTrue();
    }
}
