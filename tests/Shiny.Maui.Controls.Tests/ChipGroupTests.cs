using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// Cover for a control whose whole behaviour is what a tap does to a selection, and what a bound
/// collection sees of it afterwards.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class ChipGroupTests
{
    public ChipGroupTests()
    {
        TestDispatcherProvider.Install();
        TestDispatcherProvider.Instance.Timers.Clear();

        _ = new Application();
        Application.Current!.Resources.MergedDictionaries.Add(new Themes.BasicLightTheme());
    }


    record Category(string Name, int Id);


    static ChipGroup Group(params string[] items)
        => new() { ItemsSource = new ObservableCollection<string>(items) };


    /// <summary>
    /// Taps a chip the way a user does — through the gesture the chip actually carries, rather than a
    /// seam that exists only here. The handler hangs off the recognizer's Command for exactly this
    /// reason: a test cannot raise Tapped.
    /// </summary>
    static void Tap(ChipGroup group, int index)
    {
        var chip = group.Chips[index];
        var tap = chip.GestureRecognizers.OfType<TapGestureRecognizer>().Single();
        tap.Command!.Execute(null);
    }


    static void TapRemove(ChipGroup group, int index)
    {
        var chip = group.Chips[index];
        var tap = chip.RemoveTarget.GestureRecognizers.OfType<TapGestureRecognizer>().Single();
        tap.Command!.Execute(null);
    }


    // ---------------------------------------------------------------------------------------------
    // Items
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void EachItemGetsAChip()
    {
        var group = Group("design", "engineering", "sales");

        group.Chips.Count.ShouldBe(3);
        group.Items.ShouldBe(["design", "engineering", "sales"]);
    }


    [Fact]
    public void AnObservableSourceIsWatchedLive()
    {
        var source = new ObservableCollection<string> { "design" };
        var group = new ChipGroup { ItemsSource = source };

        source.Add("engineering");

        group.Chips.Count.ShouldBe(2);
    }


    [Fact]
    public void ANullItemGetsNoChip()
    {
        var group = new ChipGroup { ItemsSource = new List<string?> { "design", null, "sales" } };

        group.Items.ShouldBe(["design", "sales"]);
    }


    [Fact]
    public void ADisplayMemberPathNamesWhatTheChipShows()
    {
        var group = new ChipGroup
        {
            ItemsSource = new List<Category> { new("Design", 1), new("Engineering", 2) },
            DisplayMemberPath = nameof(Category.Name)
        };

        group.Chips.Select(x => x.Label.Text).ShouldBe(["Design", "Engineering"]);
    }


    [Fact]
    public void AnItemDisplayBindingWinsOverThePath()
    {
        var group = new ChipGroup
        {
            ItemsSource = new List<Category> { new("Design", 1) },
            DisplayMemberPath = nameof(Category.Name),
            ItemDisplayBinding = new Binding(nameof(Category.Id))
        };

        group.Chips[0].Label.Text.ShouldBe("1");
    }


    // ---------------------------------------------------------------------------------------------
    // Single selection
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ATapSelectsTheChip()
    {
        var group = Group("design", "engineering");

        Tap(group, 1);

        group.SelectedItem.ShouldBe("engineering");
        group.IsSelected("engineering").ShouldBeTrue();
        group.IsSelected("design").ShouldBeFalse();
    }


    [Fact]
    public void ASecondChipReplacesTheFirst()
    {
        var group = Group("design", "engineering");

        Tap(group, 0);
        Tap(group, 1);

        group.SelectedItem.ShouldBe("engineering");
        group.SelectedItems!.Count.ShouldBe(1);
    }


    [Fact]
    public void ReTappingTheAnswerKeepsItUnlessDeselectIsAllowed()
    {
        var group = Group("design", "engineering");

        Tap(group, 0);
        Tap(group, 0);
        group.SelectedItem.ShouldBe("design");

        group.AllowDeselect = true;
        Tap(group, 0);
        group.SelectedItem.ShouldBeNull();
    }


    [Fact]
    public void SelectionModeNoneSelectsNothingButStillReportsTheTap()
    {
        var group = Group("design", "engineering");
        group.SelectionMode = ChipSelectionMode.None;

        object? tapped = null;
        group.ChipTapped += (_, e) => tapped = e.Item;

        Tap(group, 1);

        tapped.ShouldBe("engineering");
        group.SelectedItem.ShouldBeNull();
    }


    // ---------------------------------------------------------------------------------------------
    // Multiple selection
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MultipleModeTogglesEachChip()
    {
        var group = Group("design", "engineering", "sales");
        group.SelectionMode = ChipSelectionMode.Multiple;

        Tap(group, 0);
        Tap(group, 2);
        group.SelectedItems!.Count.ShouldBe(2);

        Tap(group, 0);
        group.SelectedItems!.Count.ShouldBe(1);
        group.IsSelected("sales").ShouldBeTrue();
    }


    [Fact]
    public void TheSelectionIsReportedInSourceOrder()
    {
        var group = Group("design", "engineering", "sales");
        group.SelectionMode = ChipSelectionMode.Multiple;

        Tap(group, 2);
        Tap(group, 0);

        group.SelectedItems!.Cast<object>().ShouldBe(["design", "sales"]);
        group.SelectedItem.ShouldBe("design");
    }


    [Fact]
    public void TheCapRefusesTheNewChipRatherThanDroppingAnOldOne()
    {
        var group = Group("design", "engineering", "sales");
        group.SelectionMode = ChipSelectionMode.Multiple;
        group.MaxSelectionCount = 2;

        Tap(group, 0);
        Tap(group, 1);
        Tap(group, 2);

        group.SelectedItems!.Cast<object>().ShouldBe(["design", "engineering"]);
    }


    // ---------------------------------------------------------------------------------------------
    // Binding
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SettingSelectedItemSelectsThatChip()
    {
        var group = Group("design", "engineering");

        group.SelectedItem = "engineering";

        group.IsSelected("engineering").ShouldBeTrue();
        group.SelectedItems!.Cast<object>().ShouldBe(["engineering"]);
    }


    [Fact]
    public void SettingSelectedItemToSomethingNotInTheSourceSelectsNothing()
    {
        var group = Group("design", "engineering");

        group.SelectedItem = "marketing";

        group.SelectedItems!.Count.ShouldBe(0);
    }


    [Fact]
    public void TheBoundSelectedItemsListIsWrittenIntoRatherThanReplaced()
    {
        var bound = new ObservableCollection<object>();
        var group = Group("design", "engineering");
        group.SelectionMode = ChipSelectionMode.Multiple;
        group.SelectedItems = bound;

        Tap(group, 0);
        Tap(group, 1);

        group.SelectedItems.ShouldBeSameAs(bound);
        bound.ShouldBe(["design", "engineering"]);
    }


    [Fact]
    public void AChangeToTheBoundListMovesTheSelection()
    {
        var bound = new ObservableCollection<object>();
        var group = Group("design", "engineering");
        group.SelectionMode = ChipSelectionMode.Multiple;
        group.SelectedItems = bound;

        bound.Add("engineering");

        group.IsSelected("engineering").ShouldBeTrue();
        group.SelectedItem.ShouldBe("engineering");
    }


    [Fact]
    public void SelectionChangedFiresOnceForATap()
    {
        var group = Group("design", "engineering");
        var count = 0;
        group.SelectionChanged += (_, _) => count++;

        Tap(group, 0);

        count.ShouldBe(1);
    }


    [Fact]
    public void ASelectionTheSourceNoLongerOffersIsDropped()
    {
        var source = new ObservableCollection<string> { "design", "engineering" };
        var group = new ChipGroup { ItemsSource = source };

        Tap(group, 1);
        group.SelectedItem.ShouldBe("engineering");

        source.Remove("engineering");

        group.SelectedItem.ShouldBeNull();
        group.SelectedItems!.Count.ShouldBe(0);
    }


    // ---------------------------------------------------------------------------------------------
    // Removal
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RemovingAChipTakesTheItemOutOfTheSource()
    {
        var source = new ObservableCollection<string> { "design", "engineering" };
        var group = new ChipGroup { ItemsSource = source, AllowRemove = true };

        object? removed = null;
        group.ChipRemoved += (_, e) => removed = e.Item;

        TapRemove(group, 0);

        source.ShouldBe(["engineering"]);
        group.Chips.Count.ShouldBe(1);
        removed.ShouldBe("design");
    }


    [Fact]
    public void CancellingTheRemovalKeepsTheChip()
    {
        var source = new ObservableCollection<string> { "design" };
        var group = new ChipGroup { ItemsSource = source, AllowRemove = true };
        group.ChipRemoving += (_, e) => e.Cancel = true;

        TapRemove(group, 0);

        source.ShouldBe(["design"]);
        group.Chips.Count.ShouldBe(1);
    }


    [Fact]
    public void RemovingTheSelectedChipClearsTheSelection()
    {
        var source = new ObservableCollection<string> { "design", "engineering" };
        var group = new ChipGroup { ItemsSource = source, AllowRemove = true };

        Tap(group, 0);
        TapRemove(group, 0);

        group.SelectedItem.ShouldBeNull();
    }


    // ---------------------------------------------------------------------------------------------
    // Read-only
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ReadOnlyBlocksSelectionAndRemoval()
    {
        var source = new ObservableCollection<string> { "design", "engineering" };
        var group = new ChipGroup { ItemsSource = source, AllowRemove = true, IsReadOnly = true };

        Tap(group, 0);
        TapRemove(group, 0);

        group.SelectedItem.ShouldBeNull();
        source.Count.ShouldBe(2);
    }


    [Fact]
    public void ReadOnlyTakesTheRemoveAffordanceAway()
    {
        var group = new ChipGroup { ItemsSource = new List<string> { "design" }, AllowRemove = true };
        group.Chips[0].CanRemove.ShouldBeTrue();

        group.IsReadOnly = true;
        group.Chips[0].CanRemove.ShouldBeFalse();
    }


    // ---------------------------------------------------------------------------------------------
    // The API
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void SelectAndDeselectDriveTheSameSelection()
    {
        var group = Group("design", "engineering");

        group.Select("engineering").ShouldBeTrue();
        group.SelectedItem.ShouldBe("engineering");

        group.Deselect("engineering").ShouldBeTrue();
        group.SelectedItem.ShouldBeNull();

        group.Select("marketing").ShouldBeFalse();
    }


    [Fact]
    public void ClearSelectionEmptiesEverything()
    {
        var group = Group("design", "engineering");
        group.SelectionMode = ChipSelectionMode.Multiple;

        Tap(group, 0);
        Tap(group, 1);
        group.ClearSelection();

        group.SelectedItems!.Count.ShouldBe(0);
        group.SelectedItem.ShouldBeNull();
    }


    // ---------------------------------------------------------------------------------------------
    // Arrival order (#28)
    // ---------------------------------------------------------------------------------------------

    class Model
    {
        public ObservableCollection<string> Items { get; } = new(["First", "Second", "Third"]);
        public object? Selected { get; set; } = "Second";
    }


    /// <summary>
    /// SelectedItem was refused for not being in a source that did not exist yet, and nothing looked
    /// at it again when the items arrived: the group showed no selection while SelectedItem - and the
    /// view model bound to it - still held one.
    /// </summary>
    [Fact]
    public void ASelectedItemThatArrivesBeforeTheItemsIsShownOnceTheyDo()
    {
        var group = new ChipGroup { SelectionMode = ChipSelectionMode.Single };
        group.SetBinding(ChipGroup.SelectedItemProperty, new Binding(nameof(Model.Selected), BindingMode.TwoWay));
        group.SetBinding(ChipGroup.ItemsSourceProperty, nameof(Model.Items));

        var model = new Model();
        group.BindingContext = model;

        group.SelectedItem.ShouldBe("Second");
        group.IsSelected("Second").ShouldBeTrue();
        group.Chips[1].IsSelected.ShouldBeTrue();
        group.SelectedItems!.Cast<object>().ShouldBe(["Second"]);
        model.Selected.ShouldBe("Second");
    }


    [Fact]
    public void ASelectedItemSetFirstInCodeIsShownWhenTheItemsArrive()
    {
        var group = new ChipGroup { SelectionMode = ChipSelectionMode.Multiple, SelectedItem = "b" };

        group.ItemsSource = new[] { "a", "b", "c" };

        group.IsSelected("b").ShouldBeTrue();
    }


    [Fact]
    public void AnEarlySelectedItemTheItemsDoNotContainSelectsNothing()
    {
        var group = new ChipGroup { SelectionMode = ChipSelectionMode.Single, SelectedItem = "z" };

        group.ItemsSource = new[] { "a", "b", "c" };

        group.SelectedItems!.Count.ShouldBe(0);
        group.Chips.Any(x => x.IsSelected).ShouldBeFalse();
    }


    // ---------------------------------------------------------------------------------------------
    // ItemTemplate ink (#30)
    // ---------------------------------------------------------------------------------------------

    static ChipGroup Templated(Func<object> content)
        => new()
        {
            SelectionMode = ChipSelectionMode.Single,
            ChipTextColor = Colors.Blue,
            SelectedChipTextColor = Colors.Red,
            ItemTemplate = new DataTemplate(content),
            ItemsSource = new[] { "one", "two" }
        };


    /// <summary>
    /// The selected ink was painted on the chip's own label only - which a template hides - so the
    /// selected chip's template text kept the unselected colour on the selected fill.
    /// </summary>
    [Fact]
    public void ATemplatesLabelTakesTheSelectedInk()
    {
        var group = Templated(() =>
        {
            var label = new Label();
            label.SetBinding(Label.TextProperty, ".");
            return label;
        });

        group.SelectedItem = "one";

        ((Label)TemplateRoot(group, 0)).TextColor.ShouldBe(Colors.Red);
        ((Label)TemplateRoot(group, 1)).TextColor.ShouldBe(Colors.Blue);

        group.SelectedItem = "two";

        ((Label)TemplateRoot(group, 0)).TextColor.ShouldBe(Colors.Blue);
        ((Label)TemplateRoot(group, 1)).TextColor.ShouldBe(Colors.Red);
    }


    [Fact]
    public void ALabelNestedInTheTemplateTakesTheInkToo()
    {
        var group = Templated(() => new HorizontalStackLayout { Children = { new Label { Text = "x" } } });

        group.SelectedItem = "two";

        NestedLabel(group, 1).TextColor.ShouldBe(Colors.Red);
        NestedLabel(group, 0).TextColor.ShouldBe(Colors.Blue);
    }


    [Fact]
    public void ALabelThatColoursItselfKeepsItsColour()
    {
        var group = Templated(() => new HorizontalStackLayout
        {
            Children = { new Label { Text = "x", TextColor = Colors.Green } }
        });

        group.SelectedItem = "one";

        NestedLabel(group, 0).TextColor.ShouldBe(Colors.Green);
        NestedLabel(group, 1).TextColor.ShouldBe(Colors.Green);
    }


    [Fact]
    public void ALabelWhoseOwnStyleColoursItKeepsThatColour()
    {
        var style = new Style(typeof(Label));
        style.Setters.Add(new Setter { Property = Label.TextColorProperty, Value = Colors.Green });

        var group = Templated(() => new Label { Text = "x", Style = style });
        group.SelectedItem = "one";

        ((Label)TemplateRoot(group, 0)).TextColor.ShouldBe(Colors.Green);
    }


    /// <summary>An app-wide implicit Label style (the MAUI template ships one) is not the template's choice.</summary>
    [Fact]
    public void AnAppWideLabelStyleDoesNotStopTheInk()
    {
        var style = new Style(typeof(Label));
        style.Setters.Add(new Setter { Property = Label.TextColorProperty, Value = Colors.Black });
        Application.Current!.Resources.Add(style);

        var group = Templated(() => new Label { Text = "x" });
        _ = new ContentPage { Content = group };
        group.SelectedItem = "one";

        ((Label)TemplateRoot(group, 0)).TextColor.ShouldBe(Colors.Red);
        ((Label)TemplateRoot(group, 1)).TextColor.ShouldBe(Colors.Blue);
    }


    static View TemplateRoot(ChipGroup group, int index)
        => FindLabels(group.Chips[index]).First(x => !ReferenceEquals(x, group.Chips[index].Label));

    static Label NestedLabel(ChipGroup group, int index) => (Label)TemplateRoot(group, index);

    static IEnumerable<Label> FindLabels(IVisualTreeElement element)
    {
        if (element is Label label)
            yield return label;

        foreach (var child in element.GetVisualChildren())
        {
            foreach (var found in FindLabels(child))
                yield return found;
        }
    }
}
