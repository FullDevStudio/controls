using Shiny.Maui.Controls.FloatingPanel;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls;

/// <summary>
/// A ContentPage with a built-in OverlayHost. Set page content via <see cref="PageContent"/>
/// and add FloatingPanels via <see cref="Panels"/>. The overlay layer is automatically
/// managed — no need to wrap your layout in a Grid + OverlayHost manually.
/// </summary>
[ContentProperty(nameof(PageContent))]
public class ShinyContentPage : ContentPage
{
    /// <summary>
    /// Appended to the page's own <see cref="Element.AutomationId"/> to name the built-in loading overlay,
    /// so a UI test can wait for it to go away.
    /// </summary>
    public const string LoadingAutomationIdSuffix = "_Loading";

    /// <summary>
    /// Appended to the page's id to name its <see cref="OverlayHost"/>, whose backdrop is then
    /// <c>&lt;page id&gt;_OverlayHost_Backdrop</c>.
    /// </summary>
    public const string OverlayHostAutomationIdSuffix = "_OverlayHost";

    readonly Grid rootGrid;
    readonly OverlayHost overlayHost;
    readonly LoadingOverlay loadingOverlay;

    public ShinyContentPage()
    {
        overlayHost = new OverlayHost();

        // A built-in loading overlay every page gets for free — driven by IsLoading and
        // customizable via the Loading* passthrough properties below. It never dismisses on a
        // backdrop tap (loading state is code-controlled) and is brought to the front when shown.
        loadingOverlay = new LoadingOverlay { CloseOnBackdropTap = false };
        overlayHost.Children.Add(loadingOverlay);
        AutomationIds.Link(this, loadingOverlay, LoadingAutomationIdSuffix);
        AutomationIds.Link(this, overlayHost, OverlayHostAutomationIdSuffix);

        rootGrid = new Grid
        {
            Children = { overlayHost }
        };
        base.Content = rootGrid;

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(ShinyContentPage));
    }

    public static readonly BindableProperty PageContentProperty = BindableProperty.Create(
        nameof(PageContent),
        typeof(View),
        typeof(ShinyContentPage),
        null,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(ShinyContentPage), () => OnPageContentChanged(b, o, n)));

    public View? PageContent
    {
        get => (View?)GetValue(PageContentProperty);
        set => SetValue(PageContentProperty, value);
    }

    /// <summary>
    /// The OverlayHost for this page. Add FloatingPanels directly,
    /// or use the <see cref="Panels"/> collection for XAML convenience.
    /// </summary>
    public OverlayHost OverlayHost => overlayHost;

    public static readonly BindableProperty BackdropColorProperty = BindableProperty.Create(
        nameof(BackdropColor),
        typeof(Color),
        typeof(ShinyContentPage),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyContentPage), () =>
            {
                ((ShinyContentPage)b).overlayHost.BackdropColor = (Color?)n;
            }));

    /// <summary>Leave unset to follow the active theme's scrim.</summary>
    public Color? BackdropColor
    {
        get => (Color?)GetValue(BackdropColorProperty);
        set => SetValue(BackdropColorProperty, value);
    }

    public static readonly BindableProperty BackdropMaxOpacityProperty = BindableProperty.Create(
        nameof(BackdropMaxOpacity),
        typeof(double),
        typeof(ShinyContentPage),
        0.5,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyContentPage), () =>
            {
                ((ShinyContentPage)b).overlayHost.BackdropMaxOpacity = (double)n;
            }));

    public double BackdropMaxOpacity
    {
        get => (double)GetValue(BackdropMaxOpacityProperty);
        set => SetValue(BackdropMaxOpacityProperty, value);
    }

    /// <summary>
    /// Collection of FloatingPanels to display in the overlay layer.
    /// </summary>
    public IList<IView> Panels => overlayHost.Children;

    #region Built-in loading overlay

    /// <summary>
    /// The page's built-in <see cref="Shiny.Maui.Controls.LoadingOverlay"/>. Prefer the
    /// <c>Loading*</c> passthrough properties; use this for anything they don't cover.
    /// </summary>
    public LoadingOverlay LoadingOverlay => loadingOverlay;

    /// <summary>Show/hide the built-in loading overlay. Bind this to your busy flag.</summary>
    public static readonly BindableProperty IsLoadingProperty = BindableProperty.Create(
        nameof(IsLoading), typeof(bool), typeof(ShinyContentPage), false,
        BindingMode.TwoWay,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyContentPage), () =>
            {
            var page = (ShinyContentPage)b;
            if ((bool)n)
                page.BringLoadingOverlayToFront();
            page.loadingOverlay.IsShown = (bool)n;
        }));
    public bool IsLoading
    {
        get => (bool)GetValue(IsLoadingProperty);
        set => SetValue(IsLoadingProperty, value);
    }

    /// <summary>Optional message shown beneath the spinner/progress bar.</summary>
    public static readonly BindableProperty LoadingMessageProperty = BindableProperty.Create(
        nameof(LoadingMessage), typeof(string), typeof(ShinyContentPage), null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyContentPage), () =>
            {
                ((ShinyContentPage)b).loadingOverlay.Message = (string?)n;
            }));
    public string? LoadingMessage
    {
        get => (string?)GetValue(LoadingMessageProperty);
        set => SetValue(LoadingMessageProperty, value);
    }

    /// <summary>When true (default) shows a spinner; when false shows a determinate progress bar.</summary>
    public static readonly BindableProperty LoadingIsIndeterminateProperty = BindableProperty.Create(
        nameof(LoadingIsIndeterminate), typeof(bool), typeof(ShinyContentPage), true,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyContentPage), () =>
            {
                ((ShinyContentPage)b).loadingOverlay.IsIndeterminate = (bool)n;
            }));
    public bool LoadingIsIndeterminate
    {
        get => (bool)GetValue(LoadingIsIndeterminateProperty);
        set => SetValue(LoadingIsIndeterminateProperty, value);
    }

    /// <summary>Progress value (0–100) used when <see cref="LoadingIsIndeterminate"/> is false.</summary>
    public static readonly BindableProperty LoadingProgressProperty = BindableProperty.Create(
        nameof(LoadingProgress), typeof(double), typeof(ShinyContentPage), 0.0,
        BindingMode.TwoWay,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyContentPage), () =>
            {
                ((ShinyContentPage)b).loadingOverlay.Progress = (double)n;
            }));
    public double LoadingProgress
    {
        get => (double)GetValue(LoadingProgressProperty);
        set => SetValue(LoadingProgressProperty, value);
    }

    /// <summary>Overrides the spinner/progress accent color.</summary>
    public static readonly BindableProperty LoadingSpinnerColorProperty = BindableProperty.Create(
        nameof(LoadingSpinnerColor), typeof(Color), typeof(ShinyContentPage), null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyContentPage), () =>
            {
                ((ShinyContentPage)b).loadingOverlay.SpinnerColor = (Color?)n;
            }));
    public Color? LoadingSpinnerColor
    {
        get => (Color?)GetValue(LoadingSpinnerColorProperty);
        set => SetValue(LoadingSpinnerColorProperty, value);
    }

    /// <summary>Frosted-glass blur radius for the loading backdrop (0 = plain dim).</summary>
    public static readonly BindableProperty LoadingBlurRadiusProperty = BindableProperty.Create(
        nameof(LoadingBlurRadius), typeof(double), typeof(ShinyContentPage), 0.0,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyContentPage), () =>
            {
                ((ShinyContentPage)b).loadingOverlay.BlurRadius = (double)n;
            }));
    public double LoadingBlurRadius
    {
        get => (double)GetValue(LoadingBlurRadiusProperty);
        set => SetValue(LoadingBlurRadiusProperty, value);
    }

    /// <summary>
    /// Replaces the default spinner/progress/message content with your own template (passed through to
    /// <see cref="Overlay.OverlayContentTemplate"/>). The <c>IsLoading</c> show/hide + backdrop still apply.
    /// </summary>
    public static readonly BindableProperty LoadingContentTemplateProperty = BindableProperty.Create(
        nameof(LoadingContentTemplate), typeof(DataTemplate), typeof(ShinyContentPage), null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ShinyContentPage), () =>
            {
                ((ShinyContentPage)b).loadingOverlay.OverlayContentTemplate = (DataTemplate?)n;
            }));
    public DataTemplate? LoadingContentTemplate
    {
        get => (DataTemplate?)GetValue(LoadingContentTemplateProperty);
        set => SetValue(LoadingContentTemplateProperty, value);
    }

    void BringLoadingOverlayToFront()
    {
        // Keep the loading overlay above any FloatingPanels/Overlays added after it in the host.
        if (overlayHost.Children.Contains(loadingOverlay) &&
            overlayHost.Children.IndexOf(loadingOverlay) != overlayHost.Children.Count - 1)
        {
            overlayHost.Children.Remove(loadingOverlay);
            overlayHost.Children.Add(loadingOverlay);
        }
    }

    #endregion

    /// <summary>
    /// Hides the base Content property — use <see cref="PageContent"/> instead.
    /// </summary>
    public new View? Content
    {
        get => PageContent;
        set => PageContent = value;
    }

    static void OnPageContentChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var page = (ShinyContentPage)bindable;

        if (oldValue is View oldView)
            page.rootGrid.Children.Remove(oldView);

        if (newValue is View newView)
        {
            // Insert content behind the overlay host
            page.rootGrid.Children.Insert(0, newView);
        }
    }
}
