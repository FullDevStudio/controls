using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

[Collection(ApplicationResourcesCollection.Name)]
public class AccordionTests
{
    public AccordionTests() => _ = new Application();

    static Accordion Build(Accordion accordion, int count = 3)
    {
        for (var i = 0; i < count; i++)
            accordion.Children.Add(new Expander { HeaderText = "Item " + i });

        return accordion;
    }


    [Fact]
    public void SingleModeClosesWhateverElseWasOpen()
    {
        var accordion = Build(new Accordion());

        accordion.Items[0].IsExpanded = true;
        accordion.Items[2].IsExpanded = true;

        accordion.Items[0].IsExpanded.ShouldBeFalse();
        accordion.Items[1].IsExpanded.ShouldBeFalse();
        accordion.Items[2].IsExpanded.ShouldBeTrue();
    }


    [Fact]
    public void MultipleModeLeavesTheOthersAlone()
    {
        var accordion = Build(new Accordion { SelectionMode = AccordionSelectionMode.Multiple });

        accordion.Items[0].IsExpanded = true;
        accordion.Items[2].IsExpanded = true;

        accordion.ExpandedIndexes.ShouldBe([0, 2]);
    }


    [Fact]
    public void ExpandedIndexFollowsWhicheverIsOpen()
    {
        var accordion = Build(new Accordion());

        accordion.ExpandedIndex.ShouldBe(-1);

        accordion.Items[1].IsExpanded = true;
        accordion.ExpandedIndex.ShouldBe(1);

        accordion.Items[1].IsExpanded = false;
        accordion.ExpandedIndex.ShouldBe(-1);
    }


    [Fact]
    public void SettingExpandedIndexOpensThatItem()
    {
        var accordion = Build(new Accordion());

        accordion.ExpandedIndex = 2;

        accordion.Items[2].IsExpanded.ShouldBeTrue();
    }


    [Fact]
    public void RefusingToCollapseAllOpensTheFirstItem()
    {
        var accordion = Build(new Accordion { AllowCollapseAll = false });

        accordion.Items[0].IsExpanded.ShouldBeTrue();

        // The only open item may not be closed by a tap, so it loses the affordance.
        accordion.Items[0].CanCollapse.ShouldBeFalse();
        accordion.Items[1].CanCollapse.ShouldBeTrue();
    }


    [Fact]
    public void CollapseAllStillLeavesOneOpenWhenItMust()
    {
        var accordion = Build(new Accordion { AllowCollapseAll = false });
        accordion.Items[2].IsExpanded = true;

        accordion.CollapseAll();

        accordion.ExpandedIndex.ShouldBe(0);
    }


    [Fact]
    public void CollapseAllEmptiesItWhenItIsAllowedTo()
    {
        var accordion = Build(new Accordion());
        accordion.Items[1].IsExpanded = true;

        accordion.CollapseAll();

        accordion.ExpandedIndexes.ShouldBeEmpty();
        accordion.ExpandedIndex.ShouldBe(-1);
    }


    [Fact]
    public void ExpandAllIsIgnoredInSingleMode()
    {
        var accordion = Build(new Accordion());

        accordion.ExpandAll();

        accordion.ExpandedIndexes.ShouldBeEmpty();
    }


    [Fact]
    public void ItemsSourceGeneratesAnExpanderPerElement()
    {
        var accordion = new Accordion
        {
            ItemsSource = new[] { "Alpha", "Beta" },
            ContentTemplate = new DataTemplate(() => new Label())
        };

        accordion.Items.Count.ShouldBe(2);
        accordion.Items[0].HeaderText.ShouldBe("Alpha");
        accordion.Items[1].HeaderText.ShouldBe("Beta");
    }


    [Fact]
    public void GeneratedItemsSitAfterTheOnesInMarkup()
    {
        var accordion = new Accordion();
        accordion.Children.Add(new Expander { HeaderText = "Declared" });
        accordion.ItemsSource = new[] { "Generated" };

        accordion.Items.Select(x => x.HeaderText).ShouldBe(["Declared", "Generated"]);
    }


    [Fact]
    public void DefaultsReachItemsThatDidNotSetThemselves()
    {
        var accordion = new Accordion { AnimationDuration = 400, Animation = ExpanderAnimation.Slide };

        var inherits = new Expander();
        var insists = new Expander { AnimationDuration = 100 };

        accordion.Children.Add(inherits);
        accordion.Children.Add(insists);

        inherits.AnimationDuration.ShouldBe(400u);
        inherits.Animation.ShouldBe(ExpanderAnimation.Slide);

        // Its own value survives, but it still picks up what it did not set.
        insists.AnimationDuration.ShouldBe(100u);
        insists.Animation.ShouldBe(ExpanderAnimation.Slide);
    }


    [Fact]
    public void ChangingADefaultLaterReachesTheItemsItAlreadySeeded()
    {
        var accordion = new Accordion { AnimationDuration = 400 };
        var item = new Expander();
        accordion.Children.Add(item);

        accordion.AnimationDuration = 600;

        item.AnimationDuration.ShouldBe(600u);
    }


    [Fact]
    public void AnUntouchedDefaultIsNotPushedAtAll()
    {
        var accordion = new Accordion();
        var item = new Expander { AnimationDuration = 100 };
        accordion.Children.Add(item);

        item.AnimationDuration.ShouldBe(100u);
    }


    [Fact]
    public void ItemEventsCarryTheIndexAndTheData()
    {
        AccordionItemEventArgs? seen = null;
        var accordion = new Accordion { ItemsSource = new[] { "Alpha", "Beta" } };
        accordion.ItemExpanded += (_, e) => seen = e;

        accordion.Items[1].IsExpanded = true;

        seen.ShouldNotBeNull();
        seen.Index.ShouldBe(1);
        seen.Data.ShouldBe("Beta");
        seen.IsExpanded.ShouldBeTrue();
    }


    [Fact]
    public void ClosingTheOthersDoesNotEchoAsItemEvents()
    {
        var accordion = Build(new Accordion());
        accordion.Items[0].IsExpanded = true;

        var collapses = 0;
        accordion.ItemCollapsed += (_, _) => collapses++;

        accordion.Items[1].IsExpanded = true;

        // Item 0 closing is a consequence of item 1 opening, not an event of its own.
        collapses.ShouldBe(0);
    }


    [Fact]
    public void RemovingAnItemDetachesItFromTheAccordion()
    {
        var accordion = Build(new Accordion());
        var item = accordion.Items[0];

        accordion.Children.Remove(item);

        item.Owner.ShouldBeNull();
        accordion.Items.Count.ShouldBe(2);
    }


    /// <summary>
    /// Switching AllowCollapseAll off at runtime opens the first item silently; ExpandedIndex has to
    /// follow it rather than stay at -1 while item 0 is plainly open.
    /// </summary>
    [Fact]
    public void DisallowingAllClosedAtRuntimePublishesTheIndex()
    {
        var accordion = Build(new Accordion());
        accordion.ExpandedIndex.ShouldBe(-1);

        accordion.AllowCollapseAll = false;

        accordion.Items[0].IsExpanded.ShouldBeTrue();
        accordion.ExpandedIndex.ShouldBe(0);
    }


    [Fact]
    public void SwitchingToSingleModePublishesTheSurvivor()
    {
        var accordion = Build(new Accordion { SelectionMode = AccordionSelectionMode.Multiple });
        accordion.Items[1].IsExpanded = true;
        accordion.Items[2].IsExpanded = true;
        accordion.ExpandedIndex.ShouldBe(1);

        accordion.SelectionMode = AccordionSelectionMode.Single;

        accordion.ExpandedIndexes.Count.ShouldBe(1);
        accordion.ExpandedIndex.ShouldBe(accordion.ExpandedIndexes[0]);
    }


    /// <summary>
    /// Each add builds the one expander it adds. Rebuilding every generated item on every change
    /// would build 1 + 2 + … + 50 = 1,275 of them for these fifty.
    /// </summary>
    [Fact]
    public void AddingToTheSourceBuildsOnlyTheNewExpanders()
    {
        var built = 0;
        var source = new ObservableCollection<string>();
        var accordion = new Accordion
        {
            ItemsSource = source,
            ItemTemplate = new DataTemplate(() =>
            {
                built++;
                return new Expander();
            })
        };

        for (var i = 0; i < 50; i++)
            source.Add("Item " + i);

        built.ShouldBe(50);
        accordion.Items.Count.ShouldBe(50);
        accordion.Items[49].BindingContext.ShouldBe("Item 49");
    }


    [Fact]
    public void AnOpenItemStaysOpenWhenTheSourceGrows()
    {
        var source = new ObservableCollection<string> { "Alpha", "Beta", "Gamma" };
        var accordion = new Accordion { ItemsSource = source };
        var beta = accordion.Items[1];
        beta.IsExpanded = true;

        source.Insert(0, "First");
        source.Add("Last");

        accordion.Items.Select(x => x.HeaderText).ShouldBe(["First", "Alpha", "Beta", "Gamma", "Last"]);
        accordion.Items[2].IsExpanded.ShouldBeTrue();
        accordion.ExpandedIndex.ShouldBe(2);
        accordion.Items[2].ShouldBeSameAs(beta);
    }


    [Fact]
    public void RemovingFromTheSourceLeavesTheOtherExpandersAlone()
    {
        var source = new ObservableCollection<string> { "Alpha", "Beta", "Gamma" };
        var accordion = new Accordion { ItemsSource = source };
        var gamma = accordion.Items[2];
        gamma.IsExpanded = true;

        source.RemoveAt(0);

        accordion.Items.Select(x => x.HeaderText).ShouldBe(["Beta", "Gamma"]);
        accordion.Items[1].IsExpanded.ShouldBeTrue();
        accordion.ExpandedIndex.ShouldBe(1);
        accordion.Items[1].ShouldBeSameAs(gamma);
    }


    [Fact]
    public void ReplacingAnItemRebuildsOnlyItsExpander()
    {
        var source = new ObservableCollection<string> { "Alpha", "Beta", "Gamma" };
        var accordion = new Accordion { ItemsSource = source };
        var alpha = accordion.Items[0];
        var gamma = accordion.Items[2];
        gamma.IsExpanded = true;

        source[1] = "Delta";

        accordion.Items.Select(x => x.HeaderText).ShouldBe(["Alpha", "Delta", "Gamma"]);
        accordion.Items[2].IsExpanded.ShouldBeTrue();
        accordion.Items[0].ShouldBeSameAs(alpha);
        accordion.Items[2].ShouldBeSameAs(gamma);
    }


    /// <summary>
    /// A moved item is taken out and put back rather than rebuilt. With AllowCollapseAll off that is
    /// also the moment it could go wrong: half way through, the open item is not in the list at all.
    /// </summary>
    [Fact]
    public void MovingAnItemTakesItsOpenExpanderAlong()
    {
        var source = new ObservableCollection<string> { "Alpha", "Beta", "Gamma" };
        var accordion = new Accordion { ItemsSource = source, AllowCollapseAll = false };
        var beta = accordion.Items[1];
        beta.IsExpanded = true;

        source.Move(1, 2);

        accordion.Items.Select(x => x.HeaderText).ShouldBe(["Alpha", "Gamma", "Beta"]);
        accordion.ExpandedIndexes.ShouldBe([2]);
        accordion.ExpandedIndex.ShouldBe(2);
        accordion.Items[2].ShouldBeSameAs(beta);
    }


    [Fact]
    public void AMovedExpanderKeepsItsDataAndItsDefaults()
    {
        AccordionItemEventArgs? seen = null;
        var source = new ObservableCollection<string> { "Alpha", "Beta", "Gamma" };
        var accordion = new Accordion { ItemsSource = source, AnimationDuration = 400 };
        accordion.ItemExpanded += (_, e) => seen = e;

        source.Move(0, 2);
        accordion.AnimationDuration = 600;
        accordion.Items[2].IsExpanded = true;

        // Still seeded by the accordion, so a later default reaches it ...
        accordion.Items[2].AnimationDuration.ShouldBe(600u);

        // ... and still known as the expander for "Alpha".
        seen.ShouldNotBeNull();
        seen.Index.ShouldBe(2);
        seen.Data.ShouldBe("Alpha");
    }


    [Fact]
    public void ItemsAddedLaterStillSitAfterTheOnesInMarkup()
    {
        var source = new ObservableCollection<string>();
        var accordion = new Accordion();
        accordion.Children.Add(new Expander { HeaderText = "Declared" });
        accordion.ItemsSource = source;

        source.Add("Alpha");
        source.Insert(0, "First");
        source.Add("Last");

        accordion.Items.Select(x => x.HeaderText).ShouldBe(["Declared", "First", "Alpha", "Last"]);
    }
}
