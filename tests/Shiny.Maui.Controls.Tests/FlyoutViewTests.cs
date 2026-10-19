using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shiny.Maui.Controls.Flyout;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// The parts of <see cref="FlyoutView"/> that are arithmetic rather than pixels: what the content is
/// inset by, where the panel ends up, and which of the width-driven rules wins.
/// </summary>
/// <remarks>
/// Every test drives a real measure/arrange pass rather than poking at the runtime state, because the
/// distinction the control exists for — pushing insets the content, floating does not — is only
/// visible in the arranged bounds.
/// </remarks>
[Collection(ApplicationResourcesCollection.Name)]
public class FlyoutViewTests
{
    public FlyoutViewTests()
    {
        TestDispatcherProvider.Install();

        // A fresh Application per test, not `Application.Current ?? new` - Application.Current is
        // process-wide, so anything one test merges would leak into the rest of the collection.
        _ = new Application();
    }


    static (FlyoutView View, BoxView Content, FlyoutPanel Panel) Build(
        FlyoutPresentation presentation = FlyoutPresentation.Push,
        FlyoutPanelState state = FlyoutPanelState.Expanded,
        FlyoutSide side = FlyoutSide.Start,
        Action<FlyoutPanel>? configure = null,
        FlyoutPushMode pushMode = FlyoutPushMode.Resize)
    {
        var content = new BoxView();
        var panel = new FlyoutPanel
        {
            Presentation = presentation,
            State = state,
            PanelContent = new BoxView()
        };
        configure?.Invoke(panel);

        // Resize by default here, not because it is the control's default - it is not - but because
        // it is the mode whose arithmetic these tests were written against. The shift arithmetic has
        // its own tests below.
        var view = new FlyoutView { Content = content, IsAnimationEnabled = false, PushMode = pushMode };
        if (side == FlyoutSide.Start)
            view.Start = panel;
        else
            view.End = panel;

        return (view, content, panel);
    }


    /// <summary>
    /// The cross-platform half of a layout pass. <c>IView.Arrange</c> is not enough on its own: on a
    /// <see cref="Microsoft.Maui.Controls.Layout"/> it only sets the layout's own frame and then hands
    /// off to the platform handler, so with no handler the children are never arranged at all.
    /// </summary>
    static void Layout(FlyoutView view, double width = 1000, double height = 800)
    {
        var layout = (Microsoft.Maui.ILayout)view;
        layout.CrossPlatformMeasure(width, height);
        layout.CrossPlatformArrange(new Rect(0, 0, width, height));
    }


    [Fact]
    public void PushInResizeModeNarrowsTheContent()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Push);
        panel.ExpandedWidth = 280;

        Layout(view);

        view.ContentBounds.X.ShouldBe(280);
        view.ContentBounds.Width.ShouldBe(720);
        panel.Frame.Width.ShouldBe(280);
        panel.TranslationX.ShouldBe(0);
    }


    [Fact]
    public void OverlayLeavesTheContentWhereItIs()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Overlay);
        panel.ExpandedWidth = 280;

        Layout(view);

        view.ContentBounds.X.ShouldBe(0);
        view.ContentBounds.Width.ShouldBe(1000);
        panel.Frame.Width.ShouldBe(280);
        panel.TranslationX.ShouldBe(0);
        view.ScrimProgress.ShouldBe(1);
    }


    [Fact]
    public void PushingPanelHasNoScrim()
    {
        var (view, _, _) = Build(FlyoutPresentation.Push);

        Layout(view);

        view.ScrimProgress.ShouldBe(0);
    }


    [Theory]
    [InlineData(FlyoutPresentation.Push)]
    [InlineData(FlyoutPresentation.Overlay)]
    public void ARailAlwaysInsetsTheContent(FlyoutPresentation presentation)
    {
        var (view, _, panel) = Build(presentation, FlyoutPanelState.Collapsed);
        panel.CollapsedWidth = 64;

        Layout(view);

        view.ContentBounds.X.ShouldBe(64);
        view.ContentBounds.Width.ShouldBe(936);
        view.ScrimProgress.ShouldBe(0);
    }


    [Fact]
    public void HiddenPanelIsParkedOffScreen()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Overlay, FlyoutPanelState.Hidden);
        panel.ExpandedWidth = 280;

        Layout(view);

        view.ContentBounds.X.ShouldBe(0);
        view.ContentBounds.Width.ShouldBe(1000);
        panel.TranslationX.ShouldBe(-280);
        panel.IsVisible.ShouldBeFalse();
    }


    /// <summary>
    /// The reason an expanded floating panel keeps the inset it had rather than dropping to zero: a
    /// rail that is expanded over the content must not shove the content sideways on the way.
    /// </summary>
    [Fact]
    public void ExpandingARailInOverlayKeepsTheContentStill()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Overlay, FlyoutPanelState.Collapsed);
        panel.CollapsedWidth = 64;
        panel.ExpandedWidth = 280;

        Layout(view);
        view.ContentBounds.X.ShouldBe(64);

        panel.State = FlyoutPanelState.Expanded;
        Layout(view);

        view.ContentBounds.X.ShouldBe(64);
        view.ContentBounds.Width.ShouldBe(936);
        panel.Frame.Width.ShouldBe(280);
    }


    [Fact]
    public void AutoPresentationFollowsTheHostWidth()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Auto);
        panel.CompactWidth = 800;
        panel.ExpandedWidth = 280;

        Layout(view, width: 900);
        view.GetEffectivePresentation(FlyoutSide.Start).ShouldBe(FlyoutPresentation.Push);
        view.ContentBounds.X.ShouldBe(280);

        Layout(view, width: 700);
        view.GetEffectivePresentation(FlyoutSide.Start).ShouldBe(FlyoutPresentation.Overlay);
        view.ContentBounds.X.ShouldBe(0);
    }


    [Fact]
    public void CollapseBelowCompactsAndThenRestores()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Push);
        panel.CollapseBelow = 700;

        Layout(view, width: 900);
        panel.State.ShouldBe(FlyoutPanelState.Expanded);

        Layout(view, width: 600);
        panel.State.ShouldBe(FlyoutPanelState.Collapsed);

        Layout(view, width: 900);
        panel.State.ShouldBe(FlyoutPanelState.Expanded);
    }


    /// <summary>
    /// Issue #20: WinUI hands the measure and the arrange widths a fraction of a pixel apart. When
    /// both passes reported the width it flipped between the two on every pass, each flip counted as
    /// a resize that re-targeted the panels and invalidated the measure, and WinUI killed the app with
    /// a layout cycle. Only the first measure learns the width now; after that the arrange owns it.
    /// </summary>
    [Fact]
    public void MeasureAndArrangeWidthsAFractionApartSettle()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Push);
        panel.CollapsedState = FlyoutPanelState.Hidden;
        panel.CollapseBelow = 900;
        panel.CompactWidth = 900;

        var layout = (Microsoft.Maui.ILayout)view;
        for (var i = 0; i < 5; i++)
        {
            layout.CrossPlatformMeasure(894.7, 812.7);
            if (i > 0)
                view.HostWidth.ShouldBe(895.3, "the measure moved the host width again");

            layout.CrossPlatformArrange(new Rect(0, 0, 895.3, 812.7));
            view.HostWidth.ShouldBe(895.3);
        }

        panel.State.ShouldBe(FlyoutPanelState.Hidden);
    }


    /// <summary>
    /// Compaction is a response to the viewport; a deliberate choice outranks it. Widening the host
    /// after the user has closed the panel themselves must not re-open it.
    /// </summary>
    [Fact]
    public void ADeliberateStateChangeSurvivesTheHostGrowingBack()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Push);
        panel.CollapseBelow = 700;

        Layout(view, width: 900);
        Layout(view, width: 600);
        panel.State.ShouldBe(FlyoutPanelState.Collapsed);

        panel.State = FlyoutPanelState.Hidden;
        Layout(view, width: 900);

        panel.State.ShouldBe(FlyoutPanelState.Hidden);
    }


    [Fact]
    public void ToggleGoesToTheCollapsedStateAndBack()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Overlay);
        panel.CollapsedState = FlyoutPanelState.Hidden;
        Layout(view);

        _ = view.ToggleAsync();
        panel.State.ShouldBe(FlyoutPanelState.Hidden);

        _ = view.ToggleAsync();
        panel.State.ShouldBe(FlyoutPanelState.Expanded);
    }


    [Fact]
    public void EndPanelArrivesFromTheOtherEdge()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Push, side: FlyoutSide.End);
        panel.ExpandedWidth = 300;

        Layout(view);

        panel.Side.ShouldBe(FlyoutSide.End);
        view.ContentBounds.X.ShouldBe(0);
        view.ContentBounds.Width.ShouldBe(700);
        panel.Frame.X.ShouldBe(700);
    }


    [Fact]
    public void RightToLeftPutsTheStartPanelOnTheRight()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Push);
        panel.ExpandedWidth = 280;
        view.FlowDirection = FlowDirection.RightToLeft;

        Layout(view);

        view.ContentBounds.X.ShouldBe(0);
        view.ContentBounds.Width.ShouldBe(720);
        panel.Frame.X.ShouldBe(720);
    }


    [Fact]
    public void APanelIsNeverWiderThanTheFlyout()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Push);
        panel.ExpandedWidth = 400;

        Layout(view, width: 300);

        panel.Frame.Width.ShouldBe(300);
        view.ContentBounds.Width.ShouldBe(0);
    }


    [Fact]
    public void StateChangedReportsTheTransitionButNotTheInitialLayout()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Push);
        var seen = new List<FlyoutStateChangedEventArgs>();
        view.StateChanged += (_, e) => seen.Add(e);

        Layout(view);
        seen.ShouldBeEmpty();

        panel.State = FlyoutPanelState.Collapsed;

        seen.Count.ShouldBe(1);
        seen[0].Side.ShouldBe(FlyoutSide.Start);
        seen[0].OldState.ShouldBe(FlyoutPanelState.Expanded);
        seen[0].NewState.ShouldBe(FlyoutPanelState.Collapsed);
    }


    [Fact]
    public void BothPanelsNarrowTheirOwnSideInResizeMode()
    {
        var view = new FlyoutView
        {
            Content = new BoxView(),
            IsAnimationEnabled = false,
            PushMode = FlyoutPushMode.Resize,
            Start = new FlyoutPanel { Presentation = FlyoutPresentation.Push, ExpandedWidth = 200 },
            End = new FlyoutPanel { Presentation = FlyoutPresentation.Push, ExpandedWidth = 150 }
        };

        Layout(view);

        view.ContentBounds.X.ShouldBe(200);
        view.ContentBounds.Width.ShouldBe(650);
    }


    // --- shift, the default push mode ---------------------------------------------------------------

    [Fact]
    public void ShiftIsTheDefault()
    {
        new FlyoutView().PushMode.ShouldBe(FlyoutPushMode.Shift);
    }


    [Fact]
    public void ShiftMovesTheContentWithoutNarrowingIt()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Push, pushMode: FlyoutPushMode.Shift);
        panel.ExpandedWidth = 280;

        Layout(view);

        // Moved over, still its full width. The far edge travels out of the view, which is clipped -
        // nothing inside the content re-lays out, so text does not rewrap as the panel opens.
        view.ContentBounds.X.ShouldBe(280);
        view.ContentBounds.Width.ShouldBe(1000);
    }


    [Fact]
    public void ShiftFromTheEndMovesTheContentTheOtherWay()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Push, side: FlyoutSide.End, pushMode: FlyoutPushMode.Shift);
        panel.ExpandedWidth = 300;

        Layout(view);

        view.ContentBounds.X.ShouldBe(-300);
        view.ContentBounds.Width.ShouldBe(1000);
    }


    [Fact]
    public void TwoShiftingPanelsCancelRatherThanCrush()
    {
        var content = new BoxView();
        var view = new FlyoutView
        {
            Content = content,
            IsAnimationEnabled = false,
            PushMode = FlyoutPushMode.Shift,
            Start = new FlyoutPanel { Presentation = FlyoutPresentation.Push, State = FlyoutPanelState.Expanded, PanelContent = new BoxView(), ExpandedWidth = 280 },
            End = new FlyoutPanel { Presentation = FlyoutPresentation.Push, State = FlyoutPanelState.Expanded, PanelContent = new BoxView(), ExpandedWidth = 200 }
        };

        Layout(view);

        // 280 right and 200 left is a net 80 right. Shifting cannot satisfy both sides at once, and
        // the alternative - splitting the difference by narrowing - is the exact thing this mode
        // exists to avoid.
        view.ContentBounds.X.ShouldBe(80);
        view.ContentBounds.Width.ShouldBe(1000);
    }


    [Fact]
    public void OverlayStillLeavesTheContentAloneInShiftMode()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Overlay, pushMode: FlyoutPushMode.Shift);
        panel.ExpandedWidth = 280;

        Layout(view);

        view.ContentBounds.X.ShouldBe(0);
        view.ContentBounds.Width.ShouldBe(1000);
    }


    [Fact]
    public void ARailShiftsTooRatherThanNarrowing()
    {
        var (view, _, panel) = Build(FlyoutPresentation.Push, FlyoutPanelState.Collapsed, pushMode: FlyoutPushMode.Shift);
        panel.CollapsedWidth = 64;

        Layout(view);

        // Deliberate: the mode governs every displacement the view applies, so "Shift never resizes
        // your content" holds without exception. An app whose rail is permanent chrome wants
        // Resize - a rail that shifts pushes 64pt of content off the far edge for good.
        view.ContentBounds.X.ShouldBe(64);
        view.ContentBounds.Width.ShouldBe(1000);
    }

}
