using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Handlers;
using Shiny.Maui.Controls.Flyout;
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


    // --- Shell: installed before the page is built --------------------------------------------------

    /// <summary>
    /// A Shell with the flyout declared on it and one tab per page, none of them built yet: Shell
    /// creates a templated page only when it is about to show it. A headless Shell never raises
    /// <c>Navigated</c> (it waits for a window), so whatever these tests see installed was installed
    /// before the navigation.
    /// </summary>
    static (Shell Shell, ShellItem Item) ShellWithTabs(params Func<ContentPage>[] pages)
    {
        var shell = new Shell();
        ShinyFlyout.SetStartTemplate(shell, PanelTemplate());

        var item = new ShellItem();
        foreach (var page in pages)
            item.Items.Add(new ShellSection { Items = { new ShellContent { ContentTemplate = new DataTemplate(page) } } });

        shell.Items.Add(item);
        shell.CurrentItem = item;
        return (shell, item);
    }


    /// <summary>
    /// What a Shell renderer does with a tab: has its ShellContent create the page, then gives the
    /// page a handler, which is where a platform builds the native views.
    /// </summary>
    static (ContentPage Page, RecordingPageHandler Handler) BuildPage(ShellItem item, int tab)
    {
        var page = (ContentPage)((IShellContentController)item.Items[tab].Items[0]).GetOrCreateContent();
        var handler = new RecordingPageHandler();
        page.Handler = handler;
        return (page, handler);
    }


    /// <summary>
    /// Stands in for the platform: records what the page's content was when the handler mapped
    /// <c>Content</c>, which is where a platform builds the content's native views.
    /// </summary>
    sealed class RecordingPageHandler() : ViewHandler<IView, object>(mapper)
    {
        static readonly PropertyMapper<IView, RecordingPageHandler> mapper = new()
        {
            [nameof(IContentView.Content)] = (handler, view) => handler.ContentWhenBuilt = ((ContentPage)view).Content
        };

        public View? ContentWhenBuilt { get; private set; }

        protected override object CreatePlatformView() => new();
    }


    /// <summary>
    /// <c>Navigated</c> can come after the page is built (a push; <c>GoToAsync</c> on Windows), and then
    /// the content had been built for the bare page and was taken out of the native tree again by the
    /// move into the flyout. Installed before the handler, the content is built once, already inside
    /// the flyout.
    /// </summary>
    [Fact]
    public void AShellPageIsWrappedBeforeItIsBuilt()
    {
        var body = new Label { Text = "body" };
        var (_, item) = ShellWithTabs(() => new ContentPage { Content = body });

        var (page, handler) = BuildPage(item, 0);

        var view = ShinyFlyout.GetFlyoutView(page);
        view.ShouldNotBeNull();
        handler.ContentWhenBuilt.ShouldBe(view);
        view.Content.ShouldBe(body);
    }


    /// <summary>The state carries from tab to tab as it did when the navigation installed the flyout.</summary>
    [Fact]
    public void AShellPageWrappedEarlyCarriesTheStateFromThePageBefore()
    {
        var (_, item) = ShellWithTabs(
            () => new ContentPage { Content = new Label() },
            () => new ContentPage { Content = new Label() });

        var (first, _) = BuildPage(item, 0);
        ShinyFlyout.GetFlyoutView(first)!.GetPanel(FlyoutSide.Start)!.State = FlyoutPanelState.Expanded;

        item.CurrentItem = item.Items[1];
        var (second, _) = BuildPage(item, 1);

        ShinyFlyout.GetFlyoutView(second)!.GetPanel(FlyoutSide.Start)!.State.ShouldBe(FlyoutPanelState.Expanded);
    }


    /// <summary>
    /// A page Shell builds before it is shown — the next tab, on a platform that builds ahead — is left
    /// to its navigation, so it opens in the state the flyout is in by then rather than the one it was
    /// in when the page was built.
    /// </summary>
    [Fact]
    public void AShellPageBuiltAheadOfItsTabWaitsForItsNavigation()
    {
        var (shell, item) = ShellWithTabs(
            () => new ContentPage { Content = new Label() },
            () => new ContentPage { Content = new Label() });

        var (shown, _) = BuildPage(item, 0);
        var (ahead, _) = BuildPage(item, 1);
        ShinyFlyout.GetFlyoutView(ahead).ShouldBeNull();

        ShinyFlyout.GetFlyoutView(shown)!.GetPanel(FlyoutSide.Start)!.State = FlyoutPanelState.Expanded;
        item.CurrentItem = item.Items[1];
        ShinyFlyout.InstallOn(shell, ahead);

        ShinyFlyout.GetFlyoutView(ahead)!.GetPanel(FlyoutSide.Start)!.State.ShouldBe(FlyoutPanelState.Expanded);
    }


    [Fact]
    public void AShellPageThatBringsItsOwnFlyoutIsStillLeftAlone()
    {
        var (_, item) = ShellWithTabs(() => new ShinyFlyoutPage { Detail = new Label() });

        var (page, _) = BuildPage(item, 0);

        ShinyFlyout.GetFlyoutView(page).ShouldBeNull();
    }


    /// <summary>
    /// Shell reports a new page's elements to <c>DescendantAdded</c> right after the page itself. A
    /// wrap inside that report would have the flyout and the content reported twice, and a listener
    /// that subscribes per element (as the control feedback does) would subscribe twice.
    /// </summary>
    [Fact]
    public void WrappingAShellPageEarlyReportsEachElementOnce()
    {
        var (shell, item) = ShellWithTabs(
            () => new ContentPage { Content = new VerticalStackLayout { Children = { new Button { Text = "tap" } } } });

        var present = new HashSet<Element>();
        var reportedTwice = new List<Element>();
        shell.DescendantAdded += (_, e) =>
        {
            if (!present.Add(e.Element))
                reportedTwice.Add(e.Element);
        };
        shell.DescendantRemoved += (_, e) => present.Remove(e.Element);

        var (page, _) = BuildPage(item, 0);

        ShinyFlyout.GetFlyoutView(page).ShouldNotBeNull();
        reportedTwice.ShouldBeEmpty();
    }
}
