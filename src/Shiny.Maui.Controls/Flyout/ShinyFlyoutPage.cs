using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.Flyout;

/// <summary>
/// A page built around a <see cref="FlyoutView"/> — the drop-in replacement for MAUI's
/// <see cref="Microsoft.Maui.Controls.FlyoutPage"/> when you want a panel that can collapse to a rail,
/// sit on either edge, and push the content rather than only float over it.
/// </summary>
/// <remarks>
/// <para>
/// It is a <see cref="ShinyContentPage"/>, so the overlay host, floating panels and the built-in
/// loading overlay all come with it, and the flyout sits underneath them.
/// </para>
/// <para>
/// <b>Detail is a view, not a page.</b> Only <c>Window</c>, <c>Shell</c>, <c>NavigationPage</c>,
/// <c>TabbedPage</c> and <c>FlyoutPage</c> can parent a <c>Page</c> in MAUI, so — unlike
/// <c>FlyoutPage.Detail</c> — this cannot host a <c>NavigationPage</c>. Navigate with Shell, put the
/// page inside a <c>NavigationPage</c> and use <see cref="ShinyFlyout"/> to install the same flyout
/// on every page, or swap <see cref="Detail"/> yourself.
/// </para>
/// </remarks>
[ContentProperty(nameof(Detail))]
public class ShinyFlyoutPage : ShinyContentPage
{
    readonly FlyoutView flyout;

    public ShinyFlyoutPage()
    {
        this.flyout = new FlyoutView();
        base.PageContent = this.flyout;
        AutomationIds.Link(this, this.flyout, ShinyFlyout.FlyoutAutomationIdSuffix);

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(ShinyFlyoutPage));
    }


    /// <summary>The flyout itself, for anything the passthrough properties below do not cover.</summary>
    public FlyoutView FlyoutView => this.flyout;


    public static readonly BindableProperty DetailProperty = BindableProperty.Create(
        nameof(Detail),
        typeof(View),
        typeof(ShinyFlyoutPage),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyFlyoutPage), () =>
        {
            ((ShinyFlyoutPage)b).flyout.Content = (View?)n;
        }));

    /// <summary>The page body the panels sit beside.</summary>
    public View? Detail
    {
        get => (View?)this.GetValue(DetailProperty);
        set => this.SetValue(DetailProperty, value);
    }

    /// <summary>Backing store for <see cref="PushMode"/>.</summary>
    public static readonly BindableProperty PushModeProperty = BindableProperty.Create(
        nameof(PushMode),
        typeof(FlyoutPushMode),
        typeof(ShinyFlyoutPage),
        FlyoutPushMode.Shift,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyFlyoutPage), () =>
        {
            ((ShinyFlyoutPage)b).flyout.PushMode = (FlyoutPushMode)n;
        }));

    /// <inheritdoc cref="FlyoutView.PushMode"/>
    public FlyoutPushMode PushMode
    {
        get => (FlyoutPushMode)this.GetValue(PushModeProperty);
        set => this.SetValue(PushModeProperty, value);
    }

    public static readonly BindableProperty StartProperty = BindableProperty.Create(
        nameof(Start),
        typeof(FlyoutPanel),
        typeof(ShinyFlyoutPage),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyFlyoutPage), () =>
        {
            ((ShinyFlyoutPage)b).flyout.Start = (FlyoutPanel?)n;
        }));

    /// <summary>The panel on the leading edge — left in a left-to-right layout.</summary>
    public FlyoutPanel? Start
    {
        get => (FlyoutPanel?)this.GetValue(StartProperty);
        set => this.SetValue(StartProperty, value);
    }

    public static readonly BindableProperty EndProperty = BindableProperty.Create(
        nameof(End),
        typeof(FlyoutPanel),
        typeof(ShinyFlyoutPage),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyFlyoutPage), () =>
        {
            ((ShinyFlyoutPage)b).flyout.End = (FlyoutPanel?)n;
        }));

    /// <summary>The panel on the trailing edge — right in a left-to-right layout.</summary>
    public FlyoutPanel? End
    {
        get => (FlyoutPanel?)this.GetValue(EndProperty);
        set => this.SetValue(EndProperty, value);
    }


    /// <summary>Expands the panel, or returns it to its <see cref="FlyoutPanel.CollapsedState"/>.</summary>
    public Task ToggleAsync(FlyoutSide side = FlyoutSide.Start) => this.flyout.ToggleAsync(side);

    public Task SetStateAsync(FlyoutSide side, FlyoutPanelState state) => this.flyout.SetStateAsync(side, state);

    public FlyoutPanelState GetState(FlyoutSide side = FlyoutSide.Start) => this.flyout.GetState(side);

    /// <summary>
    /// Hides <see cref="ShinyContentPage.PageContent"/> — on this page the body is
    /// <see cref="Detail"/>, and the page content is the flyout that wraps it.
    /// </summary>
    public new View? Content
    {
        get => this.Detail;
        set => this.Detail = value;
    }
}
