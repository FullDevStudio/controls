using System.ComponentModel;
using System.Runtime.CompilerServices;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.Flyout;

/// <summary>
/// Declares a flyout once — on a <see cref="Shell"/>, a <see cref="NavigationPage"/> or a single page —
/// and installs it over every page that host shows.
/// </summary>
/// <remarks>
/// <para>
/// The panels are declared as <see cref="DataTemplate"/>s rather than as instances, and each page gets
/// its own. Sharing one panel instance across pages would mean re-parenting it on every navigation,
/// which rebuilds its native views and throws away scroll position and focus — so the template is the
/// contract, and the state (expanded, collapsed, hidden) is carried across pages instead.
/// </para>
/// <para>
/// The flyout is installed by wrapping the page's content, so a panel sits inside the page: with Shell
/// that means below the nav bar and above the tab bar, and <see cref="FlyoutPresentation.Push"/> pushes
/// the page content, not Shell's chrome. Set <c>Shell.FlyoutBehavior="Disabled"</c> so Shell's own
/// drawer stays out of the way, and <c>Shell.NavBarIsVisible="False"</c> if you want the panel to run
/// the full height of the window.
/// </para>
/// <code>
/// &lt;Shell xmlns:shiny="http://shiny.net/maui/controls"
///        FlyoutBehavior="Disabled"&gt;
///     &lt;shiny:ShinyFlyout.StartTemplate&gt;
///         &lt;DataTemplate&gt;
///             &lt;shiny:FlyoutPanel CollapsedState="Hidden" ExpandedWidth="300"&gt;...&lt;/shiny:FlyoutPanel&gt;
///         &lt;/DataTemplate&gt;
///     &lt;/shiny:ShinyFlyout.StartTemplate&gt;
/// &lt;/Shell&gt;
/// </code>
/// </remarks>
public static class ShinyFlyout
{
    /// <summary>
    /// Appended to a page's own <see cref="Element.AutomationId"/> to name the <see cref="FlyoutView"/>
    /// installed on it (and the one inside a <see cref="ShinyFlyoutPage"/>), whose scrim is then
    /// <c>&lt;page id&gt;_Flyout_Scrim</c>.
    /// </summary>
    public const string FlyoutAutomationIdSuffix = "_Flyout";

    static readonly ConditionalWeakTable<Page, FlyoutView> installed = new();
    static readonly ConditionalWeakTable<BindableObject, HostState> hostStates = new();
    static readonly ConditionalWeakTable<BindableObject, object> hooked = new();
    static readonly ConditionalWeakTable<Page, PropertyChangedEventHandler> awaitingContent = new();

    /// <summary>What carries across a navigation: the state each side was left in.</summary>
    sealed class HostState
    {
        public FlyoutPanelState? Start;
        public FlyoutPanelState? End;

        /// <summary>
        /// Every view installed from this host, so a view option changed after the fact reaches the
        /// pages already shown and not only the next one. Weak: a popped page must still be collectable.
        /// </summary>
        public readonly List<WeakReference<FlyoutView>> Views = [];

        public IEnumerable<FlyoutView> LiveViews()
        {
            this.Views.RemoveAll(x => !x.TryGetTarget(out _));
            foreach (var reference in this.Views)
            {
                if (reference.TryGetTarget(out var view))
                    yield return view;
            }
        }

        public FlyoutPanelState? For(FlyoutSide side) => side == FlyoutSide.Start ? this.Start : this.End;

        public void Set(FlyoutSide side, FlyoutPanelState state)
        {
            if (side == FlyoutSide.Start)
                this.Start = state;
            else
                this.End = state;
        }
    }


    public static readonly BindableProperty StartTemplateProperty = BindableProperty.CreateAttached(
        "StartTemplate",
        typeof(DataTemplate),
        typeof(ShinyFlyout),
        null,
        propertyChanged: OnTemplateChanged);

    public static DataTemplate? GetStartTemplate(BindableObject host) => (DataTemplate?)host.GetValue(StartTemplateProperty);

    public static void SetStartTemplate(BindableObject host, DataTemplate? value) => host.SetValue(StartTemplateProperty, value);

    public static readonly BindableProperty EndTemplateProperty = BindableProperty.CreateAttached(
        "EndTemplate",
        typeof(DataTemplate),
        typeof(ShinyFlyout),
        null,
        propertyChanged: OnTemplateChanged);

    public static DataTemplate? GetEndTemplate(BindableObject host) => (DataTemplate?)host.GetValue(EndTemplateProperty);

    public static void SetEndTemplate(BindableObject host, DataTemplate? value) => host.SetValue(EndTemplateProperty, value);


    // The FlyoutView options, declared once on the host. Each is copied onto every view this host
    // installs, and a later change reaches the views already installed. The defaults match
    // FlyoutView's, so copying an unset one is the same as leaving it alone.

    /// <inheritdoc cref="FlyoutView.PushMode"/>
    public static readonly BindableProperty PushModeProperty = BindableProperty.CreateAttached(
        "PushMode",
        typeof(FlyoutPushMode),
        typeof(ShinyFlyout),
        FlyoutPushMode.Shift,
        propertyChanged: (b, _, n) => ForEachView(b, v => v.PushMode = (FlyoutPushMode)n));

    public static FlyoutPushMode GetPushMode(BindableObject host) => (FlyoutPushMode)host.GetValue(PushModeProperty);

    public static void SetPushMode(BindableObject host, FlyoutPushMode value) => host.SetValue(PushModeProperty, value);

    /// <inheritdoc cref="FlyoutView.ScrimColor"/>
    public static readonly BindableProperty ScrimColorProperty = BindableProperty.CreateAttached(
        "ScrimColor",
        typeof(Color),
        typeof(ShinyFlyout),
        null,
        propertyChanged: (b, _, n) => ForEachView(b, v => v.ScrimColor = (Color?)n));

    public static Color? GetScrimColor(BindableObject host) => (Color?)host.GetValue(ScrimColorProperty);

    public static void SetScrimColor(BindableObject host, Color? value) => host.SetValue(ScrimColorProperty, value);

    /// <inheritdoc cref="FlyoutView.ScrimOpacity"/>
    public static readonly BindableProperty ScrimOpacityProperty = BindableProperty.CreateAttached(
        "ScrimOpacity",
        typeof(double),
        typeof(ShinyFlyout),
        0.4,
        propertyChanged: (b, _, n) => ForEachView(b, v => v.ScrimOpacity = (double)n));

    public static double GetScrimOpacity(BindableObject host) => (double)host.GetValue(ScrimOpacityProperty);

    public static void SetScrimOpacity(BindableObject host, double value) => host.SetValue(ScrimOpacityProperty, value);

    /// <inheritdoc cref="FlyoutView.IsAnimationEnabled"/>
    public static readonly BindableProperty IsAnimationEnabledProperty = BindableProperty.CreateAttached(
        "IsAnimationEnabled",
        typeof(bool),
        typeof(ShinyFlyout),
        true,
        propertyChanged: (b, _, n) => ForEachView(b, v => v.IsAnimationEnabled = (bool)n));

    public static bool GetIsAnimationEnabled(BindableObject host) => (bool)host.GetValue(IsAnimationEnabledProperty);

    public static void SetIsAnimationEnabled(BindableObject host, bool value) => host.SetValue(IsAnimationEnabledProperty, value);


    static void ForEachView(BindableObject host, Action<FlyoutView> apply)
    {
        if (!hostStates.TryGetValue(host, out var state))
            return;

        foreach (var view in state.LiveViews())
            apply(view);
    }


    static void ApplyViewOptions(BindableObject host, FlyoutView view)
    {
        view.PushMode = GetPushMode(host);
        view.ScrimColor = GetScrimColor(host);
        view.ScrimOpacity = GetScrimOpacity(host);
        view.IsAnimationEnabled = GetIsAnimationEnabled(host);
    }


    /// <summary>The flyout installed over a page, or null if none was.</summary>
    public static FlyoutView? GetFlyoutView(Page page) => installed.TryGetValue(page, out var view) ? view : null;


    static void OnTemplateChanged(BindableObject bindable, object oldValue, object newValue)
    {
        Hook(bindable);
        InstallCurrent(bindable);
    }


    /// <summary>
    /// Subscribes to whatever "the page changed" looks like for this host. Done once per host — the
    /// two templates are separate properties and both land here.
    /// </summary>
    static void Hook(BindableObject host)
    {
        if (hooked.TryGetValue(host, out _))
            return;

        hooked.Add(host, new object());

        switch (host)
        {
            case Shell shell:
                shell.Navigated += (_, _) => Install(shell, shell.CurrentPage);
                break;

            case NavigationPage navigation:
                navigation.Pushed += (_, e) => Install(navigation, e.Page);
                navigation.Popped += (_, _) => Install(navigation, navigation.CurrentPage);
                navigation.PoppedToRoot += (_, _) => Install(navigation, navigation.CurrentPage);
                break;
        }
    }


    static void InstallCurrent(BindableObject host)
    {
        var page = host switch
        {
            Shell shell => shell.CurrentPage,
            NavigationPage navigation => navigation.CurrentPage,
            Page page1 => page1,
            _ => null
        };

        Install(host, page);
    }


    /// <summary>
    /// The same entry point a navigation takes, exposed so the carry-across-pages behaviour can be
    /// exercised without a platform navigation stack.
    /// </summary>
    internal static void InstallOn(BindableObject host, Page page) => Install(host, page);


    static void Install(BindableObject host, Page? page)
    {
        if (page is null)
            return;

        // A nav container may be what is current; the flyout belongs on the leaf that is showing.
        var target = PageOverlay.LeafPage(page) ?? page as ContentPage;
        if (target is null || installed.TryGetValue(target, out _))
            return;

        // A page that already declares its own flyout keeps it — installing a second one would put
        // two drawers on one page, each unaware of the other's scrim.
        if (target is ShinyFlyoutPage)
            return;

        var startTemplate = GetStartTemplate(host);
        var endTemplate = GetEndTemplate(host);
        if (startTemplate is null && endTemplate is null)
            return;

        // Declaring the flyout on the page itself puts it in the markup ahead of the page's content,
        // and XAML applies properties in document order — so at this point the content usually does
        // not exist yet. Wrapping nothing and being overwritten a line later is the failure that
        // makes: wait for the content instead.
        if (ContentOf(target) is null)
        {
            DeferUntilContent(host, target);
            return;
        }

        var view = new FlyoutView();
        AutomationIds.Link(target, view, FlyoutAutomationIdSuffix);
        ApplyViewOptions(host, view);
        Wrap(target, view);

        var state = hostStates.GetOrCreateValue(host);
        state.Views.Add(new WeakReference<FlyoutView>(view));
        view.Start = Build(startTemplate, FlyoutSide.Start, state);
        view.End = Build(endTemplate, FlyoutSide.End, state);
        view.StateChanged += (_, e) => state.Set(e.Side, e.NewState);

        installed.Add(target, view);
    }


    static View? ContentOf(ContentPage page) => page switch
    {
        ShinyContentPage shiny => shiny.PageContent,
        { Content: PageOverlay.ShinyOverlayRoot root } => PageOverlay.ContentOf(root),
        _ => page.Content
    };


    static void DeferUntilContent(BindableObject host, ContentPage page)
    {
        if (awaitingContent.TryGetValue(page, out _))
            return;

        PropertyChangedEventHandler handler = null!;
        handler = (_, e) =>
        {
            if (e.PropertyName is not (nameof(ContentPage.Content) or nameof(ShinyContentPage.PageContent)))
                return;

            if (ContentOf(page) is null)
                return;

            page.PropertyChanged -= handler;
            awaitingContent.Remove(page);
            Install(host, page);
        };

        awaitingContent.Add(page, handler);
        page.PropertyChanged += handler;
    }


    /// <summary>
    /// Re-parents the page's content into the flyout. On a <see cref="ShinyContentPage"/> that is its
    /// <c>PageContent</c>, not <c>Content</c> — the page's own root grid carries the overlay host, and
    /// wrapping that instead would put the flyout above every toast and dialog on the page.
    /// </summary>
    static void Wrap(ContentPage page, FlyoutView view)
    {
        if (page is ShinyContentPage shiny)
        {
            var existing = shiny.PageContent;
            shiny.PageContent = null;
            view.Content = existing;
            shiny.PageContent = view;
        }
        else if (page.Content is PageOverlay.ShinyOverlayRoot root && PageOverlay.ContentOf(root) is { } content)
        {
            // The tab bar or a docked ProgressLine got here first. The flyout goes inside the root, in
            // the content's place: wrapped around it, their layers would sit in the flyout's content,
            // and the next layer request would wrap the page in a second root.
            var index = root.Children.IndexOf(content);
            root.Children.RemoveAt(index);
            view.Content = content;
            root.Children.Insert(index, view);
        }
        else
        {
            var existing = page.Content;
            page.Content = null;
            view.Content = existing;
            page.Content = view;
        }
    }


    static FlyoutPanel? Build(DataTemplate? template, FlyoutSide side, HostState state)
    {
        if (template is null)
            return null;

        if (template.CreateContent() is not FlyoutPanel panel)
        {
            throw new InvalidOperationException(
                $"ShinyFlyout.{side}Template must create a {nameof(FlyoutPanel)}."
            );
        }

        panel.Side = side;

        // Applied before the panel is handed to the host, so a page navigated to with the drawer
        // already open does not animate it open again.
        if (state.For(side) is { } carried)
            panel.State = carried;

        return panel;
    }
}
