using System.Windows.Input;
using Shiny.Maui.Controls.Collections;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.VirtualizedGrid;

public class VirtualizedGrid : CollectionControlBase
{
    public static readonly BindableProperty ColumnCountProperty = BindableProperty.Create(
        nameof(ColumnCount),
        typeof(int),
        typeof(VirtualizedGrid),
        1,
        validateValue: (_, v) => (int)v >= 1);

    public static readonly BindableProperty PortraitColumnCountProperty = BindableProperty.Create(
        nameof(PortraitColumnCount),
        typeof(int?),
        typeof(VirtualizedGrid),
        null);

    public static readonly BindableProperty LandscapeColumnCountProperty = BindableProperty.Create(
        nameof(LandscapeColumnCount),
        typeof(int?),
        typeof(VirtualizedGrid),
        null);

    public static readonly BindableProperty IsGroupingEnabledProperty = BindableProperty.Create(
        nameof(IsGroupingEnabled),
        typeof(bool),
        typeof(VirtualizedGrid),
        false);

    public static readonly BindableProperty GroupHeaderTemplateProperty = BindableProperty.Create(
        nameof(GroupHeaderTemplate),
        typeof(DataTemplate),
        typeof(VirtualizedGrid));

    public static readonly BindableProperty HasStickyHeadersProperty = BindableProperty.Create(
        nameof(HasStickyHeaders),
        typeof(bool),
        typeof(VirtualizedGrid),
        true);

    public static readonly BindableProperty CellPaddingProperty = BindableProperty.Create(
        nameof(CellPadding),
        typeof(Thickness),
        typeof(VirtualizedGrid),
        new Thickness(0));

    public static readonly BindableProperty ShowLoadMoreButtonProperty = BindableProperty.Create(
        nameof(ShowLoadMoreButton),
        typeof(bool),
        typeof(VirtualizedGrid),
        false);

    public static readonly BindableProperty LoadMoreButtonTemplateProperty = BindableProperty.Create(
        nameof(LoadMoreButtonTemplate),
        typeof(DataTemplate),
        typeof(VirtualizedGrid));

    public static readonly BindableProperty IsLoadingMoreProperty = BindableProperty.Create(
        nameof(IsLoadingMore),
        typeof(bool),
        typeof(VirtualizedGrid),
        false,
        BindingMode.OneWayToSource);

    public static readonly BindableProperty ItemVisibleCommandProperty = BindableProperty.Create(
        nameof(ItemVisibleCommand),
        typeof(ICommand),
        typeof(VirtualizedGrid));

    public static readonly BindableProperty ItemHiddenCommandProperty = BindableProperty.Create(
        nameof(ItemHiddenCommand),
        typeof(ICommand),
        typeof(VirtualizedGrid));

    public int ColumnCount
    {
        get => (int)GetValue(ColumnCountProperty);
        set => SetValue(ColumnCountProperty, value);
    }

    public int? PortraitColumnCount
    {
        get => (int?)GetValue(PortraitColumnCountProperty);
        set => SetValue(PortraitColumnCountProperty, value);
    }

    public int? LandscapeColumnCount
    {
        get => (int?)GetValue(LandscapeColumnCountProperty);
        set => SetValue(LandscapeColumnCountProperty, value);
    }

    public bool IsGroupingEnabled
    {
        get => (bool)GetValue(IsGroupingEnabledProperty);
        set => SetValue(IsGroupingEnabledProperty, value);
    }

    public DataTemplate? GroupHeaderTemplate
    {
        get => (DataTemplate?)GetValue(GroupHeaderTemplateProperty);
        set => SetValue(GroupHeaderTemplateProperty, value);
    }

    public bool HasStickyHeaders
    {
        get => (bool)GetValue(HasStickyHeadersProperty);
        set => SetValue(HasStickyHeadersProperty, value);
    }

    public Thickness CellPadding
    {
        get => (Thickness)GetValue(CellPaddingProperty);
        set => SetValue(CellPaddingProperty, value);
    }

    public bool ShowLoadMoreButton
    {
        get => (bool)GetValue(ShowLoadMoreButtonProperty);
        set => SetValue(ShowLoadMoreButtonProperty, value);
    }

    public DataTemplate? LoadMoreButtonTemplate
    {
        get => (DataTemplate?)GetValue(LoadMoreButtonTemplateProperty);
        set => SetValue(LoadMoreButtonTemplateProperty, value);
    }

    public bool IsLoadingMore
    {
        get => (bool)GetValue(IsLoadingMoreProperty);
        set => SetValue(IsLoadingMoreProperty, value);
    }

    public ICommand? ItemVisibleCommand
    {
        get => (ICommand?)GetValue(ItemVisibleCommandProperty);
        set => SetValue(ItemVisibleCommandProperty, value);
    }

    public ICommand? ItemHiddenCommand
    {
        get => (ICommand?)GetValue(ItemHiddenCommandProperty);
        set => SetValue(ItemHiddenCommandProperty, value);
    }

    public event EventHandler<CollectionItemEventArgs>? ItemVisible;
    public event EventHandler<CollectionItemEventArgs>? ItemHidden;

    internal int GetEffectiveColumnCount()
    {
        var window = Window;
        if (window is null)
            return ColumnCount;

        var isLandscape = window.Width > window.Height;

        if (isLandscape && LandscapeColumnCount.HasValue)
            return LandscapeColumnCount.Value;

        if (!isLandscape && PortraitColumnCount.HasValue)
            return PortraitColumnCount.Value;

        return ColumnCount;
    }

    internal void RaiseItemVisible(object item, int index)
    {
        var args = new CollectionItemEventArgs(item, index);
        ItemVisible?.Invoke(this, args);
        if (ItemVisibleCommand?.CanExecute(item) == true)
            ItemVisibleCommand.Execute(item);
    }

    internal void RaiseItemHidden(object item, int index)
    {
        var args = new CollectionItemEventArgs(item, index);
        ItemHidden?.Invoke(this, args);
        if (ItemHiddenCommand?.CanExecute(item) == true)
            ItemHiddenCommand.Execute(item);
    }

    internal View CreateGroupHeaderView(object groupKey)
    {
        if (GroupHeaderTemplate is null)
        {
            var label = new Label { FontAttributes = FontAttributes.Bold };
            label.SetBinding(Label.TextProperty, static (object o) => o);
            label.BindingContext = groupKey;
            return label;
        }

        var content = GroupHeaderTemplate.CreateContent();
        var view = content as View ?? (content as ViewCell)?.View
            ?? throw new InvalidOperationException("GroupHeaderTemplate must produce a View or ViewCell.");
        view.BindingContext = groupKey;
        return view;
    }

    internal View CreateLoadMoreView()
    {
        if (LoadMoreButtonTemplate is not null)
        {
            var content = LoadMoreButtonTemplate.CreateContent();
            var view = content as View ?? (content as ViewCell)?.View
                ?? throw new InvalidOperationException("LoadMoreButtonTemplate must produce a View or ViewCell.");
            return view;
        }

        var button = new Button
        {
            Text = "Load More",
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 8)
        }.Neutralize();
        button.Clicked += (_, _) =>
        {
            IsLoadingMore = true;
            RaiseLoadMoreRequested();
            IsLoadingMore = false;
        };
        return button;
    }

    internal (IList<GroupedData> Groups, IList<object> FlatItems) GetGroupedData()
    {
        var items = GetItemsList();
        var groups = new List<GroupedData>();
        var flatItems = new List<object>();

        if (!IsGroupingEnabled || items.Count == 0)
        {
            flatItems.AddRange(items);
            return (groups, flatItems);
        }

        foreach (var item in items)
        {
            if (item is IGrouping<object, object> grouping)
            {
                var group = new GroupedData(grouping.Key, grouping.ToList());
                groups.Add(group);
            }
            else if (item is System.Collections.IEnumerable enumerable and not string)
            {
                // Try to treat as a group with the first property as key
                var groupItems = new List<object>();
                foreach (var child in enumerable)
                    groupItems.Add(child);
                if (groupItems.Count > 0)
                    groups.Add(new GroupedData(item, groupItems));
            }
            else
            {
                flatItems.Add(item);
            }
        }

        return (groups, flatItems);
    }
}

public class GroupedData
{
    public GroupedData(object key, IList<object> items)
    {
        Key = key;
        Items = items;
    }

    public object Key { get; }
    public IList<object> Items { get; }
}
