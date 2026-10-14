using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

/// <summary>
/// The ribbon's selection and overflow bookkeeping, driven by setting parameters directly.
/// </summary>
/// <remarks>
/// The renderer is not in play here, so the tabs are registered by hand exactly as the cascade does it.
/// That is the point: which tab the bar lands on when the one it was showing disappears, and which
/// groups a measuring pass folds away, are decisions made in C# and are the ones worth pinning down.
/// </remarks>
public class RibbonTests
{
    static (Ribbon Ribbon, RibbonTab[] Tabs) Build(params string[] titles)
    {
        var ribbon = new Ribbon();
        var tabs = titles.Select(t => new RibbonTab { Title = t, Key = t.ToLowerInvariant(), Ribbon = ribbon }).ToArray();

        foreach (var tab in tabs)
            ribbon.AddTab(tab);

        return (ribbon, tabs);
    }


    [Fact]
    public void LandsOnTheFirstTab()
    {
        var (ribbon, tabs) = Build("Home", "Insert");

        ribbon.IsActive(tabs[0]).ShouldBeTrue();
        ribbon.IsActive(tabs[1]).ShouldBeFalse();
    }


    [Fact]
    public void FollowsTheSelectedKey()
    {
        var (ribbon, tabs) = Build("Home", "Insert");

        ribbon.SyncSelection("insert");

        ribbon.IsActive(tabs[1]).ShouldBeTrue();
    }


    /// <summary>
    /// The contextual tab case: a tab bound to "is a table selected" simply disappears, and the ribbon
    /// has to land somewhere real rather than show an empty body.
    /// </summary>
    [Fact]
    public void FallsBackWhenTheShowingTabIsHidden()
    {
        var (ribbon, tabs) = Build("Home", "Table");
        tabs[1].ContextTitle = "Table Tools";

        ribbon.SyncSelection("table");
        ribbon.IsActive(tabs[1]).ShouldBeTrue();
        tabs[1].IsContextual.ShouldBeTrue();

        tabs[1].Visible = false;
        ribbon.EnsureSelection();

        ribbon.IsActive(tabs[1]).ShouldBeFalse();
        ribbon.IsActive(tabs[0]).ShouldBeTrue();
    }


    /// <summary>
    /// "Insert picture" makes the Format tab Visible and selects it in the same render. The ribbon's
    /// SelectedKey lands before the tab's Visible does, so the request has to wait for the tab rather
    /// than fall back to Home.
    /// </summary>
    [Fact]
    public void ARequestedTabThatIsNotVisibleYetIsSelectedWhenItAppears()
    {
        var (ribbon, tabs) = Build("Home", "Insert", "Picture");
        tabs[2].Visible = false;
        ribbon.SyncSelection("insert");

        ribbon.SyncSelection("picture");
        ribbon.IsActive(tabs[1]).ShouldBeTrue();

        tabs[2].Visible = true;
        ribbon.EnsureSelection();

        ribbon.IsActive(tabs[2]).ShouldBeTrue();
        ribbon.ExpireRequestedKey().ShouldBeFalse();
    }


    [Fact]
    public void ARequestForATabThatNeverAppearsIsDropped()
    {
        var (ribbon, tabs) = Build("Home", "Picture");
        tabs[1].Visible = false;

        ribbon.SyncSelection("picture");

        ribbon.IsActive(tabs[0]).ShouldBeTrue();
        ribbon.ExpireRequestedKey().ShouldBeTrue();

        // expired: showing the tab later no longer jumps to it
        tabs[1].Visible = true;
        ribbon.EnsureSelection();
        ribbon.IsActive(tabs[0]).ShouldBeTrue();
    }


    [Fact]
    public void ADisabledTabIsNeverLandedOn()
    {
        var (ribbon, tabs) = Build("Home", "Insert");
        tabs[0].Enabled = false;
        ribbon.EnsureSelection();

        ribbon.IsActive(tabs[1]).ShouldBeTrue();
    }


    [Fact]
    public void UnregisteringTheShowingTabMovesOff()
    {
        var (ribbon, tabs) = Build("Home", "Insert");

        ribbon.RemoveTab(tabs[0]);

        ribbon.IsActive(tabs[1]).ShouldBeTrue();
    }


    /// <summary>A tab's key defaults to its title, so most ribbons never set one.</summary>
    [Fact]
    public void TheKeyFallsBackToTheTitle()
        => new RibbonTab { Title = "Review" }.EffectiveKey.ShouldBe("Review");


    // -----------------------------------------------------------------------------------------
    // Overflow
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void GroupsAreOpenUntilTheMeasuringPassSaysOtherwise()
    {
        var ribbon = new Ribbon();
        var group = new RibbonGroup { Ribbon = ribbon };
        ribbon.Register(group);

        ribbon.IsGroupCollapsed(group).ShouldBeFalse();
    }


    [Fact]
    public void OverflowCollapsesExactlyWhatItWasTold()
    {
        var ribbon = new Ribbon();
        var a = new RibbonGroup { Ribbon = ribbon };
        var b = new RibbonGroup { Ribbon = ribbon };
        ribbon.Register(a);
        ribbon.Register(b);

        ribbon.ApplyOverflow([b.Id]);

        ribbon.IsGroupCollapsed(a).ShouldBeFalse();
        ribbon.IsGroupCollapsed(b).ShouldBeTrue();

        ribbon.ApplyOverflow([]);
        ribbon.IsGroupCollapsed(b).ShouldBeFalse();
    }


    /// <summary>
    /// Groups are reported by id rather than position, so a group that unregisters mid-pass cannot
    /// leave a stale entry behind that folds away whichever group later takes its index.
    /// </summary>
    [Fact]
    public void UnregisteringAGroupClearsItsCollapsedState()
    {
        var ribbon = new Ribbon();
        var group = new RibbonGroup { Ribbon = ribbon };
        ribbon.Register(group);
        ribbon.ApplyOverflow([group.Id]);

        ribbon.Unregister(group);
        ribbon.Register(group);

        ribbon.IsGroupCollapsed(group).ShouldBeFalse();
    }


    // -----------------------------------------------------------------------------------------
    // Menus
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void OnlyOnePanelIsOpenAtATime()
    {
        var ribbon = new Ribbon();
        var group = new RibbonGroup { Ribbon = ribbon };
        ribbon.Register(group);

        ribbon.SetMenu("owner-a", [new RibbonMenuEntry { Text = "One" }], null);
        ribbon.IsMenuOpen.ShouldBeTrue();
        ribbon.IsGroupPopupOpen(group).ShouldBeFalse();

        ribbon.SetMenu(group.Id, null, group);
        ribbon.IsGroupPopupOpen(group).ShouldBeTrue();
        ribbon.IsMenuOpen.ShouldBeTrue();

        ribbon.SetMenu(null, null, null);
        ribbon.IsMenuOpen.ShouldBeFalse();
        ribbon.IsGroupPopupOpen(group).ShouldBeFalse();
    }


    [Fact]
    public void AnEntryWithChildrenOpensRatherThanActs()
    {
        var leaf = new RibbonMenuEntry { Text = "Bar" };
        var parent = new RibbonMenuEntry { Text = "Chart", Children = [leaf] };

        parent.HasChildren.ShouldBeTrue();
        leaf.HasChildren.ShouldBeFalse();

        // A separator is never a branch, whatever was hung off it.
        new RibbonMenuEntry { IsSeparator = true, Children = [leaf] }.HasChildren.ShouldBeFalse();
    }
}

