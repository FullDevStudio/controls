using System.Collections;
using System.Windows.Input;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.ParallaxCollectionView;

/// <summary>
/// A CollectionView with a parallax header that translates and (optionally) collapses
/// as the list scrolls. Pure cross-platform MAUI — no platform handlers.
/// </summary>
[ContentProperty(nameof(ItemTemplate))]
public class ParallaxCollectionView : ContentView
{
    /// <summary>Appended to the control's <see cref="Element.AutomationId"/> to name the inner list (the scroll target). Items come from your template and carry the ids it gives them.</summary>
    public const string ListAutomationIdSuffix = "_List";

    /// <summary>Appended to the control's <see cref="Element.AutomationId"/> to name the parallax header host.</summary>
    public const string HeaderAutomationIdSuffix = "_Header";

    readonly Grid root;
    readonly ContentView heroHost;
    readonly Grid spacerHeader;
    readonly CollectionView collection;

    public ParallaxCollectionView()
    {
        // The list's header is pure empty space that the hero shows through, so it must paint
        // nothing at all. A BoxView cannot do that: its Color maps to a shape fill, and a
        // transparent fill still renders the shape's default black on iOS - which is what turned
        // the whole parallax header into a black box. An empty layout draws nothing.
        spacerHeader = new Grid { BackgroundColor = Colors.Transparent, InputTransparent = true };

        collection = new CollectionView
        {
            BackgroundColor = Colors.Transparent,
            Header = spacerHeader,
            SelectionMode = SelectionMode.None
        };
        collection.Scrolled += OnScrolled;
        collection.SelectionChanged += OnSelectionChanged;
#if IOS
        collection.HandlerChanged += (_, _) => ConfigureNativeScrollToTop();
#endif

        heroHost = new ContentView
        {
            VerticalOptions = LayoutOptions.Start,
            InputTransparent = false
        };

        root = new Grid
        {
            Children = { heroHost, collection }
        };

        Content = root;
        ApplyHeaderHeight();

        AutomationIds.Link(this, collection, ListAutomationIdSuffix);
        AutomationIds.Link(this, heroHost, HeaderAutomationIdSuffix);

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(ParallaxCollectionView));
    }

    #region Bindable Properties

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IEnumerable),
        typeof(ParallaxCollectionView),
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ParallaxCollectionView), () =>
            {
                ((ParallaxCollectionView)b).collection.ItemsSource = (IEnumerable?)n;
            }));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
        nameof(ItemTemplate),
        typeof(DataTemplate),
        typeof(ParallaxCollectionView),
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ParallaxCollectionView), () =>
            {
                ((ParallaxCollectionView)b).collection.ItemTemplate = (DataTemplate?)n;
            }));

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static readonly BindableProperty ItemsLayoutProperty = BindableProperty.Create(
        nameof(ItemsLayout),
        typeof(IItemsLayout),
        typeof(ParallaxCollectionView),
        LinearItemsLayout.Vertical,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ParallaxCollectionView), () =>
            {
                ((ParallaxCollectionView)b).collection.ItemsLayout =
            (IItemsLayout?)n ?? LinearItemsLayout.Vertical;
            }));

    public IItemsLayout ItemsLayout
    {
        get => (IItemsLayout)GetValue(ItemsLayoutProperty);
        set => SetValue(ItemsLayoutProperty, value);
    }

    public static readonly BindableProperty HeaderTemplateProperty = BindableProperty.Create(
        nameof(HeaderTemplate),
        typeof(DataTemplate),
        typeof(ParallaxCollectionView),
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(ParallaxCollectionView), () =>
            {
                ((ParallaxCollectionView)b).ApplyHeaderTemplate();
            }));

    public DataTemplate? HeaderTemplate
    {
        get => (DataTemplate?)GetValue(HeaderTemplateProperty);
        set => SetValue(HeaderTemplateProperty, value);
    }

    public static readonly BindableProperty HeaderHeightProperty = BindableProperty.Create(
        nameof(HeaderHeight),
        typeof(double),
        typeof(ParallaxCollectionView),
        240.0,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(ParallaxCollectionView), () =>
            {
                ((ParallaxCollectionView)b).ApplyHeaderHeight();
            }));

    public double HeaderHeight
    {
        get => (double)GetValue(HeaderHeightProperty);
        set => SetValue(HeaderHeightProperty, value);
    }

    public static readonly BindableProperty MinHeaderHeightProperty = BindableProperty.Create(
        nameof(MinHeaderHeight),
        typeof(double),
        typeof(ParallaxCollectionView),
        0.0);

    public double MinHeaderHeight
    {
        get => (double)GetValue(MinHeaderHeightProperty);
        set => SetValue(MinHeaderHeightProperty, value);
    }

    public static readonly BindableProperty ParallaxFactorProperty = BindableProperty.Create(
        nameof(ParallaxFactor),
        typeof(double),
        typeof(ParallaxCollectionView),
        0.5,
        validateValue: (_, v) => (double)v >= 0);

    public double ParallaxFactor
    {
        get => (double)GetValue(ParallaxFactorProperty);
        set => SetValue(ParallaxFactorProperty, value);
    }

    public static readonly BindableProperty CollapseToStickyProperty = BindableProperty.Create(
        nameof(CollapseToSticky),
        typeof(bool),
        typeof(ParallaxCollectionView),
        false);

    public bool CollapseToSticky
    {
        get => (bool)GetValue(CollapseToStickyProperty);
        set => SetValue(CollapseToStickyProperty, value);
    }

    public static readonly BindableProperty FadeHeaderOnScrollProperty = BindableProperty.Create(
        nameof(FadeHeaderOnScroll),
        typeof(bool),
        typeof(ParallaxCollectionView),
        false);

    public bool FadeHeaderOnScroll
    {
        get => (bool)GetValue(FadeHeaderOnScrollProperty);
        set => SetValue(FadeHeaderOnScrollProperty, value);
    }

    public static readonly BindableProperty EmptyViewProperty = BindableProperty.Create(
        nameof(EmptyView),
        typeof(object),
        typeof(ParallaxCollectionView),
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ParallaxCollectionView), () =>
            {
                ((ParallaxCollectionView)b).collection.EmptyView = n;
            }));

    public object? EmptyView
    {
        get => GetValue(EmptyViewProperty);
        set => SetValue(EmptyViewProperty, value);
    }

    public static readonly BindableProperty EmptyViewTemplateProperty = BindableProperty.Create(
        nameof(EmptyViewTemplate),
        typeof(DataTemplate),
        typeof(ParallaxCollectionView),
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ParallaxCollectionView), () =>
            {
                ((ParallaxCollectionView)b).collection.EmptyViewTemplate = (DataTemplate?)n;
            }));

    public DataTemplate? EmptyViewTemplate
    {
        get => (DataTemplate?)GetValue(EmptyViewTemplateProperty);
        set => SetValue(EmptyViewTemplateProperty, value);
    }

    public static readonly BindableProperty SelectionModeProperty = BindableProperty.Create(
        nameof(SelectionMode),
        typeof(SelectionMode),
        typeof(ParallaxCollectionView),
        SelectionMode.None,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ParallaxCollectionView), () =>
            {
                ((ParallaxCollectionView)b).collection.SelectionMode = (SelectionMode)n;
            }));

    public SelectionMode SelectionMode
    {
        get => (SelectionMode)GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
    }

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem),
        typeof(object),
        typeof(ParallaxCollectionView),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(ParallaxCollectionView), () =>
            {
                ((ParallaxCollectionView)b).collection.SelectedItem = n;
            }));

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly BindableProperty ItemSelectedCommandProperty = BindableProperty.Create(
        nameof(ItemSelectedCommand),
        typeof(ICommand),
        typeof(ParallaxCollectionView));

    public ICommand? ItemSelectedCommand
    {
        get => (ICommand?)GetValue(ItemSelectedCommandProperty);
        set => SetValue(ItemSelectedCommandProperty, value);
    }

    #endregion

    public event EventHandler<ParallaxScrollEventArgs>? Scrolled;
    public event EventHandler<SelectionChangedEventArgs>? ItemSelected;

    public void ScrollTo(int index, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animate = true)
        => collection.ScrollTo(index, position: position, animate: animate);

    public void ScrollTo(object item, ScrollToPosition position = ScrollToPosition.MakeVisible, bool animate = true)
        => collection.ScrollTo(item, position: position, animate: animate);

    /// <summary>
    /// Scrolls the list back to the very top, including the parallax header. On iOS this drives
    /// the underlying scroll view's content offset directly (revealing the header); other
    /// platforms fall back to scrolling the first item to the top.
    /// </summary>
    public void ScrollToTop(bool animate = true)
    {
#if IOS
        var scrollView = FindScrollView(collection.Handler?.PlatformView as UIKit.UIView);
        if (scrollView is not null)
        {
            var top = -scrollView.AdjustedContentInset.Top;
            scrollView.SetContentOffset(new CoreGraphics.CGPoint(scrollView.ContentOffset.X, top), animate);
            return;
        }
#endif
        collection.ScrollTo(0, position: ScrollToPosition.Start, animate: animate);
    }

#if IOS
    // iOS scrolls the single scrolls-to-top-enabled scroll view to the top when the user taps
    // the status bar. Re-assert ScrollsToTop on the underlying UICollectionView so the gesture
    // targets this list even when it is nested inside the parallax wrapper (issue #7).
    void ConfigureNativeScrollToTop()
    {
        var scrollView = FindScrollView(collection.Handler?.PlatformView as UIKit.UIView);
        if (scrollView is not null)
            scrollView.ScrollsToTop = true;
    }

    static UIKit.UIScrollView? FindScrollView(UIKit.UIView? view)
    {
        switch (view)
        {
            case null:
                return null;
            case UIKit.UIScrollView scroll:
                return scroll;
            default:
                foreach (var sub in view.Subviews)
                {
                    var found = FindScrollView(sub);
                    if (found is not null)
                        return found;
                }
                return null;
        }
    }
#endif

    void ApplyHeaderHeight()
    {
        // With no header template there is nothing to show, so reserve zero space.
        // Otherwise the empty (transparent) header region renders as a blank band over
        // the page background — the "gray area at the top" reported in issue #8.
        var h = HeaderTemplate is null ? 0 : Math.Max(0, HeaderHeight);
        spacerHeader.HeightRequest = h;
        heroHost.HeightRequest = h;
    }

    void ApplyHeaderTemplate()
    {
        if (HeaderTemplate is null)
        {
            heroHost.Content = null;
        }
        else
        {
            var content = HeaderTemplate.CreateContent();
            var view = content as View ?? (content as ViewCell)?.View;
            if (view is not null)
            {
                view.BindingContext ??= BindingContext;
                heroHost.Content = view;
            }
        }

        // Header presence drives the reserved height (see ApplyHeaderHeight / issue #8).
        ApplyHeaderHeight();
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (heroHost.Content is not null)
            heroHost.Content.BindingContext = BindingContext;
    }

    void OnScrolled(object? sender, ItemsViewScrolledEventArgs e)
    {
        var offset = Math.Max(0, e.VerticalOffset);
        var travel = HeaderHeight - MinHeaderHeight;
        var translation = -offset * ParallaxFactor;

        if (CollapseToSticky && travel > 0)
        {
            var maxNegative = -travel;
            if (translation < maxNegative)
                translation = maxNegative;
        }

        heroHost.TranslationY = translation;

        if (FadeHeaderOnScroll && HeaderHeight > 0)
        {
            var fade = 1.0 - Math.Min(1.0, offset / HeaderHeight);
            heroHost.Opacity = fade;
        }
        else if (heroHost.Opacity != 1)
        {
            heroHost.Opacity = 1;
        }

        var visible = Math.Max(MinHeaderHeight, HeaderHeight + translation);
        Scrolled?.Invoke(this, new ParallaxScrollEventArgs(offset, translation, visible));
    }

    void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        SelectedItem = collection.SelectedItem;
        ItemSelected?.Invoke(this, e);

        var item = e.CurrentSelection.Count > 0 ? e.CurrentSelection[0] : null;
        if (item is not null && ItemSelectedCommand?.CanExecute(item) == true)
            ItemSelectedCommand.Execute(item);
    }
}
