using System.Collections.ObjectModel;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shiny.Maui.Controls.Collections;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>Where the covers go, how the position wraps and clamps, and what drags and taps do.</summary>
[Collection(ApplicationResourcesCollection.Name)]
public class CoverFlowViewTests
{
    public CoverFlowViewTests()
    {
        TestDispatcherProvider.Install();
        _ = new Application();
    }


    // ---------------------------------------------------------------------------------------------
    // Geometry
    // ---------------------------------------------------------------------------------------------

    static readonly CoverFlowLayout Defaults = new();

    [Fact]
    public void TheCentreCoverFacesForwardAtFullSize()
    {
        var t = CoverFlowGeometry.For(0, Defaults);

        t.TranslationX.ShouldBe(0);
        t.RotationY.ShouldBe(0);
        t.Scale.ShouldBe(1);
        t.Opacity.ShouldBe(1);
    }


    [Fact]
    public void SideCoversTurnTowardTheCentre()
    {
        var right = CoverFlowGeometry.For(1, Defaults);
        var left = CoverFlowGeometry.For(-1, Defaults);

        right.TranslationX.ShouldBe(130);
        right.RotationY.ShouldBe(-60);
        right.Scale.ShouldBe(0.85, 1e-9);
        left.TranslationX.ShouldBe(-130);
        left.RotationY.ShouldBe(60);
    }


    [Fact]
    public void TheStackSpacesCoversByItemSpacingAndKeepsTheirAngle()
    {
        var t = CoverFlowGeometry.For(3, Defaults);

        t.TranslationX.ShouldBe(130 + (2 * 50));
        t.RotationY.ShouldBe(-60);
        t.Scale.ShouldBe(0.85, 1e-9);
    }


    [Fact]
    public void AHalfwayCoverIsHalfTurnedAndHalfScaled()
    {
        var t = CoverFlowGeometry.For(0.5, Defaults);

        t.TranslationX.ShouldBe(65);
        t.RotationY.ShouldBe(-30);
        t.Scale.ShouldBe(0.925, 1e-9);
    }


    [Fact]
    public void OpacityFadesTowardSideOpacityThenOutPastTheLastVisibleCover()
    {
        var layout = Defaults with { SideOpacity = 0.4, VisibleItems = 4 };

        CoverFlowGeometry.For(1, layout).Opacity.ShouldBe(1);
        CoverFlowGeometry.For(4, layout).Opacity.ShouldBe(0.4, 1e-9);
        CoverFlowGeometry.For(2.5, layout).Opacity.ShouldBe(0.7, 1e-9);
        CoverFlowGeometry.For(4.5, layout).Opacity.ShouldBe(0.2, 1e-9);
        CoverFlowGeometry.For(5, layout).Opacity.ShouldBe(0);
    }


    [Theory]
    [InlineData(-1, 5, 4)]
    [InlineData(5, 5, 0)]
    [InlineData(-6, 5, 4)]
    [InlineData(3, 5, 3)]
    public void ModIsNeverNegative(int value, int count, int expected)
        => CoverFlowGeometry.Mod(value, count).ShouldBe(expected);


    [Fact]
    public void IndexAtClampsOrWraps()
    {
        CoverFlowGeometry.IndexAt(7.4, 5, loop: false).ShouldBe(4);
        CoverFlowGeometry.IndexAt(-2, 5, loop: false).ShouldBe(0);
        CoverFlowGeometry.IndexAt(7.4, 5, loop: true).ShouldBe(2);
        CoverFlowGeometry.IndexAt(-1, 5, loop: true).ShouldBe(4);
        CoverFlowGeometry.IndexAt(1.5, 5, loop: false).ShouldBe(2);
    }


    [Fact]
    public void NearestVirtualTakesTheShortWayRound()
    {
        // At 0 of 10, item 9 is one step back rather than nine forward.
        CoverFlowGeometry.NearestVirtual(0, 9, 10, loop: true).ShouldBe(-1);
        CoverFlowGeometry.NearestVirtual(19, 1, 10, loop: true).ShouldBe(21);
        CoverFlowGeometry.NearestVirtual(0, 9, 10, loop: false).ShouldBe(9);
    }


    [Fact]
    public void ResistanceOnlyAppliesPastTheEnds()
    {
        CoverFlowGeometry.Resist(2, 5, loop: false).ShouldBe(2);
        CoverFlowGeometry.Resist(-1, 5, loop: false).ShouldBe(-CoverFlowGeometry.RubberBand, 1e-9);
        CoverFlowGeometry.Resist(5, 5, loop: false).ShouldBe(4 + CoverFlowGeometry.RubberBand, 1e-9);
        CoverFlowGeometry.Resist(-1, 5, loop: true).ShouldBe(-1);
    }


    [Fact]
    public void AFlickCoastsPastTheNearestCover()
    {
        CoverFlowGeometry.SettleTarget(2.2, 0, 10, loop: false).ShouldBe(2);
        CoverFlowGeometry.SettleTarget(2.2, 8, 10, loop: false).ShouldBe(4);
        CoverFlowGeometry.SettleTarget(8.5, 40, 10, loop: false).ShouldBe(9);
        CoverFlowGeometry.SettleTarget(-0.3, -10, 10, loop: true).ShouldBe(-3);
    }


    [Fact]
    public void LoopingReachIsCappedSoNoItemShowsTwice()
    {
        CoverFlowGeometry.Reach(4, 3, loop: true).ShouldBe(1);
        CoverFlowGeometry.Reach(4, 20, loop: true).ShouldBe(4);
        CoverFlowGeometry.Reach(4, 3, loop: false).ShouldBe(4);
    }


    // ---------------------------------------------------------------------------------------------
    // Realization
    // ---------------------------------------------------------------------------------------------

    static CoverFlowView Create(int count = 10, bool loop = false, int position = 0)
    {
        var view = new CoverFlowView
        {
            Loop = loop,
            CurrentPosition = position,
            ItemsSource = Enumerable.Range(0, count).Select(i => $"Item {i}").ToList()
        };
        view.MeasureStage(800, 400);
        view.ArrangeStage(new Rect(0, 0, 800, 400));
        return view;
    }


    [Fact]
    public void AnEmptySourceRealizesNothing()
    {
        var view = new CoverFlowView { ItemsSource = Array.Empty<string>() };

        view.RealizedSlots.ShouldBeEmpty();
        view.Next();
        view.GoTo(3);
        view.HandleTap(new Point(10, 10));
        view.CurrentPosition.ShouldBe(0);
    }


    [Fact]
    public void OnlyCoversInReachAreRealized()
    {
        var view = Create(count: 20, position: 10);

        // VisibleItems (4) either side, plus one fading in on each side.
        view.RealizedSlots.Keys.Order().ShouldBe(Enumerable.Range(5, 11));
    }


    [Fact]
    public void AStartingPositionSetBeforeTheItemsIsKept()
    {
        var view = Create(count: 10, position: 6);

        view.Position.ShouldBe(6);
        view.CurrentPosition.ShouldBe(6);
    }


    [Fact]
    public void AnOutOfRangePositionIsClampedWithoutLooping()
    {
        var view = Create(count: 5, position: 9);

        view.Position.ShouldBe(4);
        view.CurrentPosition.ShouldBe(4);
    }


    [Fact]
    public void ATemplateViewIsBoundToItsItem()
    {
        var view = Create(count: 5);

        view.RealizedSlots[2].Item.BindingContext.ShouldBe("Item 2");
    }


    [Fact]
    public void LoopingRealizesBothSidesOfTheFirstItem()
    {
        var view = Create(count: 20, loop: true);

        view.RealizedSlots.Keys.Min().ShouldBe(-5);
        view.RealizedSlots[-1].Item.BindingContext.ShouldBe("Item 19");
    }


    [Fact]
    public void TheCentreCoverIsDrawnOnTop()
    {
        var view = Create(count: 10, position: 5);
        var slots = view.RealizedSlots;

        slots[5].ZIndex.ShouldBeGreaterThan(slots[4].ZIndex);
        slots[5].ZIndex.ShouldBeGreaterThan(slots[6].ZIndex);
        slots[4].ZIndex.ShouldBeGreaterThan(slots[3].ZIndex);
        slots[6].ZIndex.ShouldBeGreaterThan(slots[7].ZIndex);
    }


    [Fact]
    public void ReflectionsRealizeASecondFlippedCopy()
    {
        var view = new CoverFlowView { ShowReflection = true, ItemsSource = new[] { "a", "b" } };

        var slot = view.RealizedSlots[0];
        slot.HasReflection.ShouldBeTrue();
        slot.ReflectionView!.ScaleY.ShouldBe(-1);
        slot.ReflectionView.BindingContext.ShouldBe("a");
    }


    [Fact]
    public void ACollectionChangeIsFollowed()
    {
        var source = new ObservableCollection<string> { "a" };
        var view = new CoverFlowView { ItemsSource = source };

        source.Add("b");

        view.RealizedSlots.Count.ShouldBe(2);
    }


    [Fact]
    public void TheCaptionFollowsTheCentreItem()
    {
        var view = Create(count: 5);
        view.CaptionTemplate = new DataTemplate(() => new Label());

        view.CaptionView!.BindingContext.ShouldBe("Item 0");
        view.GoTo(3);
        view.CaptionView.BindingContext.ShouldBe("Item 3");
    }


    // ---------------------------------------------------------------------------------------------
    // Navigation and events
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NextAndPreviousStopAtTheEnds()
    {
        var view = Create(count: 3);

        view.Previous();
        view.CurrentPosition.ShouldBe(0);

        view.Next();
        view.Next();
        view.Next();
        view.CurrentPosition.ShouldBe(2);
    }


    [Fact]
    public void NextWrapsWhenLooping()
    {
        var view = Create(count: 3, loop: true, position: 2);

        view.Next();

        view.CurrentPosition.ShouldBe(0);
        view.Position.ShouldBe(3);
    }


    [Fact]
    public void GoToClampsWithoutLooping()
    {
        var view = Create(count: 5);

        view.GoTo(99);

        view.CurrentPosition.ShouldBe(4);
    }


    [Fact]
    public void SettingCurrentPositionMovesTheCovers()
    {
        var view = Create(count: 10);

        view.CurrentPosition = 7;

        view.Position.ShouldBe(7);
    }


    [Fact]
    public void PositionChangedFiresOncePerNewIndex()
    {
        var view = Create(count: 10);
        var events = new List<int>();
        var commands = new List<object?>();
        view.PositionChanged += (_, i) => events.Add(i);
        view.PositionChangedCommand = new Command(p => commands.Add(p));

        view.GoTo(4);
        view.GoTo(4);
        view.Next();

        events.ShouldBe([4, 5]);
        commands.ShouldBe([4, 5]);
    }


    [Fact]
    public void ReplacingTheItemsDoesNotRaisePositionChanged()
    {
        var view = Create(count: 10, position: 3);
        var raised = false;
        view.PositionChanged += (_, _) => raised = true;

        view.ItemsSource = new[] { "x" };

        raised.ShouldBeFalse();
        view.CurrentPosition.ShouldBe(0);
    }


    // ---------------------------------------------------------------------------------------------
    // Gestures
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TappingASideCoverBringsItToTheCentre()
    {
        var view = Create(count: 10, position: 2);
        object? selected = null;
        view.ItemSelected += (_, e) => selected = e.Item;

        view.HandleTap(view.CentreOf(3));

        view.CurrentPosition.ShouldBe(3);
        selected.ShouldBeNull();
    }


    [Fact]
    public void TappingASideCoverWhileSettlingStillMovesToIt()
    {
        var view = Create(count: 10, position: 0);
        view.AnimateWithoutHandler = true;
        object? selected = null;
        view.ItemSelected += (_, e) => selected = e.Item;

        view.CurrentPosition = 3;
        view.AdvanceAnimation(80);

        // cover 0, left of the centre slot while the move to 3 is under way
        var point = view.CentreOf(0);
        (view.StageCentre.X - point.X).ShouldBeGreaterThan(100);
        view.HandleTap(point);
        view.AdvanceAnimation(5000);

        selected.ShouldBeNull();
        view.CurrentPosition.ShouldBe(0);
    }


    [Fact]
    public void TappingTheCentreCoverSelectsIt()
    {
        var view = Create(count: 10, position: 2);
        CollectionItemEventArgs? args = null;
        object? commanded = null;
        view.ItemSelected += (_, e) => args = e;
        view.ItemSelectedCommand = new Command(p => commanded = p);

        view.HandleTap(view.CentreOf(2));

        args.ShouldNotBeNull();
        args.Index.ShouldBe(2);
        args.Item.ShouldBe("Item 2");
        commanded.ShouldBe("Item 2");
        view.CurrentPosition.ShouldBe(2);
    }


    [Fact]
    public void TappingTheCentreWhileSettlingFinishesTheMoveAndSelectsTheArrivingCover()
    {
        // The iOS UI run caught this: a tap straight after setting CurrentPosition landed mid-animation,
        // and instead of selecting it re-targeted to the cover passing the middle (3 -> 1).
        var view = Create(count: 10, position: 0);
        view.AnimateWithoutHandler = true;
        object? selected = null;
        view.ItemSelected += (_, e) => selected = e.Item;

        view.CurrentPosition = 3;
        view.AdvanceAnimation(80);
        view.IsAnimating.ShouldBeTrue();
        view.Position.ShouldBeInRange(0.5, 2.5);

        view.HandleTap(view.StageCentre);

        view.IsAnimating.ShouldBeFalse();
        view.Position.ShouldBe(3);
        view.CurrentPosition.ShouldBe(3);
        selected.ShouldBe("Item 3");
    }


    [Fact]
    public void SelectCentreSelectsWhereItIsHeading()
    {
        var view = Create(count: 10, position: 0);
        view.AnimateWithoutHandler = true;
        object? selected = null;
        view.ItemSelected += (_, e) => selected = e.Item;

        view.Next();
        view.Next();
        view.AdvanceAnimation(40);

        view.SelectCentre();

        selected.ShouldBe("Item 2");
        view.CurrentPosition.ShouldBe(2);
    }


    [Fact]
    public void TurningLoopOnMidAnimationKeepsTheTarget()
    {
        // The other half of the iOS failure: Loop toggled while a move ran parked it between covers
        // (CurrentPosition still 11, the covers near 6), so Next then went to 7 instead of wrapping.
        var view = Create(count: 12, position: 0);
        view.AnimateWithoutHandler = true;

        view.CurrentPosition = 11;
        view.AdvanceAnimation(100);
        view.IsAnimating.ShouldBeTrue();

        view.Loop = true;

        view.Position.ShouldBe(11);
        view.CurrentPosition.ShouldBe(11);

        view.Next();
        view.AdvanceAnimation(5000);
        view.CurrentPosition.ShouldBe(0);
    }


    [Fact]
    public void TappingEmptyStageDoesNothing()
    {
        var view = Create(count: 10, position: 2);

        view.HandleTap(new Point(400, 395));

        view.CurrentPosition.ShouldBe(2);
    }


    [Fact]
    public void DraggingLeftMovesForwardAndSettlesOnAWholeItem()
    {
        var view = Create(count: 10, position: 2);

        view.Drag(GestureStatus.Started, 0);
        view.Drag(GestureStatus.Running, -130 * 1.4);
        view.Position.ShouldBe(3.4, 1e-9);

        view.Drag(GestureStatus.Completed, 0);

        // No animator headless, so the settle lands immediately; a 1.4-item drag rests at least one on.
        view.Position.ShouldBe(Math.Round(view.Position));
        view.CurrentPosition.ShouldBeGreaterThanOrEqualTo(3);
    }


    [Fact]
    public void DraggingPastTheStartIsResistedAndSpringsBack()
    {
        var view = Create(count: 10);

        view.Drag(GestureStatus.Started, 0);
        view.Drag(GestureStatus.Running, 130);
        view.Position.ShouldBe(-CoverFlowGeometry.RubberBand, 1e-9);

        view.Drag(GestureStatus.Completed, 0);
        view.CurrentPosition.ShouldBe(0);
        view.Position.ShouldBe(0);
    }


    [Fact]
    public void SwipeCanBeDisabled()
    {
        var view = Create(count: 10);
        view.IsSwipeEnabled = false;

        view.Drag(GestureStatus.Started, 0);
        view.Drag(GestureStatus.Running, -400);

        view.Position.ShouldBe(0);
    }
}
