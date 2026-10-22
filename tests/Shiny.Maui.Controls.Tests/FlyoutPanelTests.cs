using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shiny.Maui.Controls.Flyout;
using Shiny.Maui.Controls.Infrastructure;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>What the panel itself decides: which body is showing, and what a page-wide install wraps.</summary>
[Collection(ApplicationResourcesCollection.Name)]
public class FlyoutPanelTests
{
    public FlyoutPanelTests()
    {
        TestDispatcherProvider.Install();
        _ = new Application();
    }


    [Fact]
    public void CollapsedShowsTheRailInsteadOfTheBody()
    {
        var body = new Label { Text = "body" };
        var rail = new Label { Text = "rail" };
        var panel = new FlyoutPanel { PanelContent = body, RailContent = rail };
        _ = new FlyoutView { Start = panel, IsAnimationEnabled = false };

        panel.State = FlyoutPanelState.Collapsed;
        rail.Parent.ShouldNotBeNull();
        IsShowing(rail).ShouldBeTrue();
        IsShowing(body).ShouldBeFalse();

        panel.State = FlyoutPanelState.Expanded;
        IsShowing(rail).ShouldBeFalse();
        IsShowing(body).ShouldBeTrue();
    }


    /// <summary>
    /// Without rail content there is nothing to swap to, so the collapsed panel keeps showing the
    /// leading edge of the body rather than going blank.
    /// </summary>
    [Fact]
    public void CollapsedWithoutRailContentKeepsTheBody()
    {
        var body = new Label { Text = "body" };
        var panel = new FlyoutPanel { PanelContent = body };
        _ = new FlyoutView { Start = panel, IsAnimationEnabled = false };

        panel.State = FlyoutPanelState.Collapsed;

        IsShowing(body).ShouldBeTrue();
    }


    [Fact]
    public void AssigningToASideSetsTheSide()
    {
        var panel = new FlyoutPanel();
        var view = new FlyoutView { End = panel };

        panel.Side.ShouldBe(FlyoutSide.End);
        panel.Host.ShouldBe(view);
    }


    [Fact]
    public void ReplacingAPanelReleasesTheOldOne()
    {
        var first = new FlyoutPanel();
        var view = new FlyoutView { Start = first };

        view.Start = new FlyoutPanel();

        first.Host.ShouldBeNull();
        view.GetPanel(FlyoutSide.Start).ShouldNotBe(first);
    }


    /// <summary>Every ancestor between the view and the panel has to be visible for it to be on screen.</summary>
    static bool IsShowing(View view)
    {
        Element? current = view;
        while (current is not null)
        {
            if (current is View v && !v.IsVisible)
                return false;

            if (current is FlyoutPanel)
                return true;

            current = current.Parent;
        }
        return true;
    }
}


/// <summary>The declare-once install: what it wraps, and what it carries from page to page.</summary>
[Collection(ApplicationResourcesCollection.Name)]
public class ShinyFlyoutInstallTests
{
    public ShinyFlyoutInstallTests()
    {
        TestDispatcherProvider.Install();
        _ = new Application();
    }


    static DataTemplate PanelTemplate() => new(() => new FlyoutPanel
    {
        State = FlyoutPanelState.Collapsed,
        PanelContent = new Label { Text = "nav" }
    });


    [Fact]
    public void InstallingOnAPageWrapsItsContent()
    {
        var body = new Label { Text = "body" };
        var page = new ContentPage { Content = body };

        ShinyFlyout.SetStartTemplate(page, PanelTemplate());

        var view = ShinyFlyout.GetFlyoutView(page);
        view.ShouldNotBeNull();
        page.Content.ShouldBe(view);
        view.Content.ShouldBe(body);
        view.GetPanel(FlyoutSide.Start).ShouldNotBeNull();
    }


    /// <summary>
    /// A <see cref="ShinyContentPage"/> keeps its own root grid — that is where the overlay host for
    /// toasts and dialogs lives, and wrapping it would put the flyout above them.
    /// </summary>
    [Fact]
    public void InstallingOnAShinyContentPageWrapsThePageContent()
    {
        var body = new Label { Text = "body" };
        var page = new ShinyContentPage { PageContent = body };

        ShinyFlyout.SetStartTemplate(page, PanelTemplate());

        var view = ShinyFlyout.GetFlyoutView(page);
        view.ShouldNotBeNull();
        page.PageContent.ShouldBe(view);
        view.Content.ShouldBe(body);
    }


    /// <summary>
    /// XAML applies properties in document order, so a template declared above the content is set
    /// while the page still has none. Installing then would wrap nothing and be overwritten by the
    /// content assignment a line later.
    /// </summary>
    [Fact]
    public void InstallingBeforeTheContentExistsWaitsForIt()
    {
        var page = new ContentPage();

        ShinyFlyout.SetStartTemplate(page, PanelTemplate());
        ShinyFlyout.GetFlyoutView(page).ShouldBeNull();

        var body = new Label { Text = "body" };
        page.Content = body;

        var view = ShinyFlyout.GetFlyoutView(page);
        view.ShouldNotBeNull();
        page.Content.ShouldBe(view);
        view.Content.ShouldBe(body);
    }


    /// <summary>
    /// A page the tab bar (or a docked ProgressLine) has already given an overlay root. The flyout
    /// has to go inside that root, next to the layers: wrapped around it, the tab bar would sit in the
    /// flyout's content, and the next layer request would wrap the page in a second root.
    /// </summary>
    [Fact]
    public void InstallingOnAPageWithAnOverlayRootWrapsTheContentInsideIt()
    {
        var body = new Label { Text = "body" };
        var page = new ContentPage { Content = body };
        var root = PageOverlay.GetOrCreateRoot(page);
        var layer = PageOverlay.GetOrCreateLayer<PageOverlay.TabBarLayer>(root, PageOverlay.Layers.TabBar);

        var host = new NavigationPage();
        ShinyFlyout.SetStartTemplate(host, PanelTemplate());
        ShinyFlyout.InstallOn(host, page);

        var view = ShinyFlyout.GetFlyoutView(page);
        view.ShouldNotBeNull();
        page.Content.ShouldBeSameAs(root);
        layer.Parent.ShouldBeSameAs(root);
        view.Parent.ShouldBeSameAs(root);
        view.Content.ShouldBeSameAs(body);
        PageOverlay.GetOrCreateRoot(page).ShouldBeSameAs(root);
    }


    /// <summary>
    /// Each page builds its own panel from the template, so the thing that has to survive a
    /// navigation is the state — a drawer left open must still be open on the page you land on.
    /// </summary>
    [Fact]
    public void CarriesTheStateFromOnePageToTheNext()
    {
        var host = new NavigationPage();
        ShinyFlyout.SetStartTemplate(host, PanelTemplate());

        var first = new ContentPage { Content = new Label() };
        ShinyFlyout.InstallOn(host, first);
        ShinyFlyout.GetFlyoutView(first)!.GetPanel(FlyoutSide.Start)!.State = FlyoutPanelState.Expanded;

        var second = new ContentPage { Content = new Label() };
        ShinyFlyout.InstallOn(host, second);

        ShinyFlyout.GetFlyoutView(second)!.GetPanel(FlyoutSide.Start)!.State.ShouldBe(FlyoutPanelState.Expanded);
    }


    /// <summary>
    /// The view options are declared on the host, so an app never has to find the installed
    /// <see cref="FlyoutView"/> after each navigation to set them.
    /// </summary>
    [Fact]
    public void HostViewOptionsAreCopiedOntoEveryInstalledView()
    {
        var host = new NavigationPage();
        ShinyFlyout.SetStartTemplate(host, PanelTemplate());
        ShinyFlyout.SetPushMode(host, FlyoutPushMode.Resize);
        ShinyFlyout.SetScrimColor(host, Colors.Red);
        ShinyFlyout.SetScrimOpacity(host, 0.7);
        ShinyFlyout.SetIsAnimationEnabled(host, false);

        foreach (var page in new[] { new ContentPage { Content = new Label() }, new ContentPage { Content = new Label() } })
        {
            ShinyFlyout.InstallOn(host, page);

            var view = ShinyFlyout.GetFlyoutView(page)!;
            view.PushMode.ShouldBe(FlyoutPushMode.Resize);
            view.ScrimColor.ShouldBe(Colors.Red);
            view.ScrimOpacity.ShouldBe(0.7);
            view.IsAnimationEnabled.ShouldBeFalse();
        }
    }


    [Fact]
    public void ChangingAHostViewOptionReachesViewsAlreadyInstalled()
    {
        var host = new NavigationPage();
        ShinyFlyout.SetStartTemplate(host, PanelTemplate());

        var page = new ContentPage { Content = new Label() };
        ShinyFlyout.InstallOn(host, page);
        var view = ShinyFlyout.GetFlyoutView(page)!;
        view.PushMode.ShouldBe(FlyoutPushMode.Shift);

        ShinyFlyout.SetPushMode(host, FlyoutPushMode.Resize);
        view.PushMode.ShouldBe(FlyoutPushMode.Resize);

        ShinyFlyout.SetPushMode(host, FlyoutPushMode.Shift);
        view.PushMode.ShouldBe(FlyoutPushMode.Shift);
    }


    /// <summary>XAML may apply the option after the template; the order must not matter.</summary>
    [Fact]
    public void AViewOptionSetAfterTheTemplateStillApplies()
    {
        var page = new ContentPage { Content = new Label() };

        ShinyFlyout.SetStartTemplate(page, PanelTemplate());
        ShinyFlyout.SetPushMode(page, FlyoutPushMode.Resize);

        ShinyFlyout.GetFlyoutView(page)!.PushMode.ShouldBe(FlyoutPushMode.Resize);
    }


    [Fact]
    public void APageThatBringsItsOwnFlyoutIsLeftAlone()
    {
        var page = new ShinyFlyoutPage { Detail = new Label() };

        ShinyFlyout.SetStartTemplate(page, PanelTemplate());

        ShinyFlyout.GetFlyoutView(page).ShouldBeNull();
    }


    [Fact]
    public void EachPageGetsItsOwnPanelInstance()
    {
        var template = PanelTemplate();
        var first = new ContentPage { Content = new Label() };
        var second = new ContentPage { Content = new Label() };

        ShinyFlyout.SetStartTemplate(first, template);
        ShinyFlyout.SetStartTemplate(second, template);

        var one = ShinyFlyout.GetFlyoutView(first)!.GetPanel(FlyoutSide.Start);
        var two = ShinyFlyout.GetFlyoutView(second)!.GetPanel(FlyoutSide.Start);

        one.ShouldNotBeNull();
        two.ShouldNotBeNull();
        one.ShouldNotBe(two);
    }


    [Fact]
    public void ATemplateThatDoesNotMakeAPanelSaysSo()
    {
        var page = new ContentPage { Content = new Label() };
        var template = new DataTemplate(() => new Label());

        Should.Throw<InvalidOperationException>(() => ShinyFlyout.SetStartTemplate(page, template));
    }
}
