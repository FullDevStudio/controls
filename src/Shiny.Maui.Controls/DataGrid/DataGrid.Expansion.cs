using System.Collections;
using System.Globalization;
using Shiny.Maui.Controls.Themes;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.DataGrid;

/// <summary>
/// Row expansion - the detail ("breakdown") row and the hierarchical tree mode.
/// </summary>
/// <remarks>
/// Both features are the same mechanism seen from two sides: one set of expanded data items, and a
/// flatten step that decides what an expanded row reveals. A detail row emits a
/// <see cref="DataGridDetailRow"/> straight after its parent and renders
/// <see cref="RowDetailTemplate"/> across the full width; a tree row emits its children as ordinary
/// rows one level deeper. A grid can do both at once. Expansion is keyed on the *data item*, not the
/// row wrapper, so it survives the rebuild that sorting, filtering or paging triggers.
/// </remarks>
public partial class DataGrid
{
    const double ExpanderColumnWidth = 44;

    /// <summary>The tree caret's tappable box. Small enough not to eat the cell, big enough to hit.</summary>
    const double TreeCaretSize = 28;

    readonly HashSet<object> expandedItems = new();
    readonly Dictionary<object, IReadOnlyList<object>> loadedChildren = new();
    readonly HashSet<object> loadingChildren = new();
    readonly HashSet<object> loadedDetails = new();
    readonly HashSet<object> loadingDetails = new();

    // ---------- Properties ----------
    // The selector/loader delegates are BindableProperties rather than plain CLR properties so a XAML
    // page can bind them straight to a view model - {Binding} needs a BindableProperty to target.

    public static readonly BindableProperty RowDetailTemplateProperty = BindableProperty.Create(
        nameof(RowDetailTemplate), typeof(DataTemplate), typeof(DataGrid), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(DataGrid), () =>
            {
                ((DataGrid)b).RebuildAll();
            }));

    public static readonly BindableProperty RowDetailLoaderProperty = BindableProperty.Create(
        nameof(RowDetailLoader), typeof(Func<object, Task>), typeof(DataGrid), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(DataGrid), () =>
            {
                ((DataGrid)b).RebuildRows();
            }));

    public static readonly BindableProperty RowDetailLoadingTemplateProperty = BindableProperty.Create(
        nameof(RowDetailLoadingTemplate), typeof(DataTemplate), typeof(DataGrid), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(DataGrid), () =>
            {
                ((DataGrid)b).RebuildRows();
            }));

    static readonly BindablePropertyKey IsBusyPropertyKey = BindableProperty.CreateReadOnly(
        nameof(IsBusy), typeof(bool), typeof(DataGrid), false);

    /// <summary>Read-only: bind to it, the grid owns the value.</summary>
    public static readonly BindableProperty IsBusyProperty = IsBusyPropertyKey.BindableProperty;

    public static readonly BindableProperty ExpandModeProperty = BindableProperty.Create(
        nameof(ExpandMode), typeof(DataGridExpandMode), typeof(DataGrid), DataGridExpandMode.Multiple);

    public static readonly BindableProperty TreeIndentSizeProperty = BindableProperty.Create(
        nameof(TreeIndentSize), typeof(double), typeof(DataGrid), 20d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(DataGrid), () =>
            {
                ((DataGrid)b).RebuildRows();
            }));

    public static readonly BindableProperty ExpandOnRowTapProperty = BindableProperty.Create(
        nameof(ExpandOnRowTap), typeof(bool), typeof(DataGrid), false);

    public static readonly BindableProperty IsRowExpandableProperty = BindableProperty.Create(
        nameof(IsRowExpandable), typeof(Func<object, bool>), typeof(DataGrid), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(DataGrid), () =>
            {
                ((DataGrid)b).RebuildRows();
            }));

    public static readonly BindableProperty ChildrenSelectorProperty = BindableProperty.Create(
        nameof(ChildrenSelector), typeof(Func<object, IEnumerable?>), typeof(DataGrid), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(DataGrid), () =>
            {
                ((DataGrid)b).RebuildAll();
            }));

    public static readonly BindableProperty ChildrenLoaderProperty = BindableProperty.Create(
        nameof(ChildrenLoader), typeof(Func<object, Task<IEnumerable>>), typeof(DataGrid), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(DataGrid), () =>
            {
                ((DataGrid)b).RebuildAll();
            }));

    public static readonly BindableProperty HasChildrenSelectorProperty = BindableProperty.Create(
        nameof(HasChildrenSelector), typeof(Func<object, bool>), typeof(DataGrid), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(DataGrid), () =>
            {
                ((DataGrid)b).RebuildRows();
            }));

    /// <summary>
    /// Content shown in a full-width row underneath an expanded row - the "breakdown" view. The
    /// template's BindingContext is the row's data item, so it can host any controls you like.
    /// Setting it adds an expander column at the leading edge.
    /// </summary>
    public DataTemplate? RowDetailTemplate
    {
        get => (DataTemplate?)this.GetValue(RowDetailTemplateProperty);
        set => this.SetValue(RowDetailTemplateProperty, value);
    }

    /// <summary>Whether one row or many can be expanded at a time (default <see cref="DataGridExpandMode.Multiple"/>).</summary>
    public DataGridExpandMode ExpandMode
    {
        get => (DataGridExpandMode)this.GetValue(ExpandModeProperty);
        set => this.SetValue(ExpandModeProperty, value);
    }

    /// <summary>Indent applied per hierarchy level in tree mode (default 20).</summary>
    public double TreeIndentSize
    {
        get => (double)this.GetValue(TreeIndentSizeProperty);
        set => this.SetValue(TreeIndentSizeProperty, value);
    }

    /// <summary>
    /// Tapping anywhere on a row toggles its expansion. Off by default - the caret is the affordance,
    /// so a tap stays free for selection/editing.
    /// </summary>
    public bool ExpandOnRowTap
    {
        get => (bool)this.GetValue(ExpandOnRowTapProperty);
        set => this.SetValue(ExpandOnRowTapProperty, value);
    }

    /// <summary>
    /// Per-item veto on expansion. Return false and the row shows no caret and cannot be expanded -
    /// use it for rows with nothing to break down.
    /// </summary>
    public Func<object, bool>? IsRowExpandable
    {
        get => (Func<object, bool>?)this.GetValue(IsRowExpandableProperty);
        set => this.SetValue(IsRowExpandableProperty, value);
    }

    /// <summary>
    /// Turns the grid into a tree: returns the child items of a row, or null/empty for a leaf.
    /// Children go through the same filter/sort pipeline as their parents, one level at a time.
    /// </summary>
    public Func<object, IEnumerable?>? ChildrenSelector
    {
        get => (Func<object, IEnumerable?>?)this.GetValue(ChildrenSelectorProperty);
        set => this.SetValue(ChildrenSelectorProperty, value);
    }

    /// <summary>
    /// Lazily loads a row's children the first time it is expanded (a spinner glyph shows meanwhile).
    /// Results are cached for the lifetime of the grid - call <see cref="InvalidateChildren"/> to drop
    /// them. <see cref="ChildrenSelector"/> gets first refusal: the loader only runs for items it
    /// returns null for, so a tree can mix in-memory branches with fetched ones.
    /// </summary>
    public Func<object, Task<IEnumerable>>? ChildrenLoader
    {
        get => (Func<object, Task<IEnumerable>>?)this.GetValue(ChildrenLoaderProperty);
        set => this.SetValue(ChildrenLoaderProperty, value);
    }

    /// <summary>
    /// Reports whether a row has children without materializing them - required with
    /// <see cref="ChildrenLoader"/> if you want leaves to render without a caret before the first load.
    /// </summary>
    public Func<object, bool>? HasChildrenSelector
    {
        get => (Func<object, bool>?)this.GetValue(HasChildrenSelectorProperty);
        set => this.SetValue(HasChildrenSelectorProperty, value);
    }

    /// <summary>
    /// Fetches whatever the detail row needs, the first time that row is expanded. The caret turns
    /// into a spinner while it runs and the detail row shows <see cref="RowDetailLoadingTemplate"/>;
    /// <see cref="RowDetailTemplate"/> is not built until it completes, so the template can assume its
    /// data has arrived. Each item is loaded once - call <see cref="InvalidateRowDetail"/> to refetch.
    /// </summary>
    /// <remarks>
    /// The loader returns no value: where the data lands is the app's business. Fill an observable
    /// property on the item (or a lookup keyed by it) and let the template bind to it as usual, which
    /// keeps the template's BindingContext the row's item rather than some wrapper.
    /// </remarks>
    public Func<object, Task>? RowDetailLoader
    {
        get => (Func<object, Task>?)this.GetValue(RowDetailLoaderProperty);
        set => this.SetValue(RowDetailLoaderProperty, value);
    }

    /// <summary>
    /// Shown in the detail row while <see cref="RowDetailLoader"/> runs - a skeleton, say. Its
    /// BindingContext is the row's item. Defaults to a centered spinner.
    /// </summary>
    public DataTemplate? RowDetailLoadingTemplate
    {
        get => (DataTemplate?)this.GetValue(RowDetailLoadingTemplateProperty);
        set => this.SetValue(RowDetailLoadingTemplateProperty, value);
    }

    /// <summary>
    /// True while any row is waiting on <see cref="ChildrenLoader"/> or <see cref="RowDetailLoader"/>.
    /// Bind a page-level busy indicator to it; the per-row spinners are drawn by the grid either way.
    /// Distinct from <see cref="IsLoading"/>, which you set to cover the grid while *its* data loads.
    /// </summary>
    public bool IsBusy
    {
        get => (bool)this.GetValue(IsBusyProperty);
        private set => this.SetValue(IsBusyPropertyKey, value);
    }

    /// <summary>The data items currently expanded.</summary>
    public IReadOnlyList<object> ExpandedItems => this.expandedItems.ToList();

    public event EventHandler<object>? RowExpanded;
    public event EventHandler<object>? RowCollapsed;
    public event EventHandler<bool>? IsBusyChanged;

    /// <summary>Raised when <see cref="ChildrenLoader"/> throws; the row is collapsed again.</summary>
    public event EventHandler<DataGridLoadFailedEventArgs>? ChildrenLoadFailed;

    /// <summary>Raised when <see cref="RowDetailLoader"/> throws; the row is collapsed again.</summary>
    public event EventHandler<DataGridLoadFailedEventArgs>? RowDetailLoadFailed;

    // ---------- Public API ----------

    public bool IsRowExpanded(object item) => this.expandedItems.Contains(item);

    /// <summary>Expands a row - loading its children first when <see cref="ChildrenLoader"/> is set.</summary>
    public void ExpandRow(object item)
    {
        if (!this.CanExpand(item) || !this.expandedItems.Add(item))
            return;

        if (this.ExpandMode == DataGridExpandMode.Single)
        {
            foreach (var other in this.expandedItems.Where(i => !Equals(i, item)).ToList())
                this.expandedItems.Remove(other);
        }

        this.RowExpanded?.Invoke(this, item);

        // A row can owe both - a tree node with a breakdown of its own - so neither short-circuits
        // the other, and each clears its own spinner when it lands.
        var loading = false;
        if (this.NeedsChildrenLoad(item))
        {
            loading = true;
            this.LoadChildrenAsync(item);
        }
        if (this.NeedsDetailLoad(item))
        {
            loading = true;
            this.LoadDetailAsync(item);
        }

        if (!loading)
            this.RebuildRows();
    }

    /// <summary>True while this row is waiting on a children or detail load.</summary>
    public bool IsRowBusy(object item)
        => this.loadingChildren.Contains(item) || this.loadingDetails.Contains(item);

    public void CollapseRow(object item)
    {
        if (!this.expandedItems.Remove(item))
            return;

        this.RowCollapsed?.Invoke(this, item);
        this.RebuildRows();
    }

    public void ToggleRow(object item)
    {
        if (this.IsRowExpanded(item))
            this.CollapseRow(item);
        else
            this.ExpandRow(item);
    }

    /// <summary>
    /// Expands every row the grid currently knows about. In tree mode that means every already-loaded
    /// level - rows still waiting on <see cref="ChildrenLoader"/> are not fetched, since the depth of
    /// a lazily loaded tree is unbounded. Detail loads *are* started (one per expanded row, so the
    /// work is bounded by what is on screen).
    /// </summary>
    public void ExpandAll()
    {
        if (this.ExpandMode == DataGridExpandMode.Single)
            return;

        var pending = new List<object>();
        foreach (var item in this.EnumerateKnownItems())
        {
            if (!this.CanExpand(item) || this.NeedsChildrenLoad(item))
                continue;

            this.expandedItems.Add(item);
            if (this.NeedsDetailLoad(item))
                pending.Add(item);
        }

        this.RebuildRows();
        foreach (var item in pending)
            this.LoadDetailAsync(item);
    }

    public void CollapseAll()
    {
        if (this.expandedItems.Count == 0)
            return;

        this.expandedItems.Clear();
        this.RebuildRows();
    }

    /// <summary>Drops cached lazily-loaded children so the next expand re-fetches them.</summary>
    public void InvalidateChildren(object? item = null)
    {
        if (item is null)
            this.loadedChildren.Clear();
        else
            this.loadedChildren.Remove(item);

        this.RebuildRows();
    }

    /// <summary>
    /// Forgets that a row's detail was loaded, so the next expand runs <see cref="RowDetailLoader"/>
    /// again. Pass null for every row. A row that is expanded right now reloads immediately.
    /// </summary>
    public void InvalidateRowDetail(object? item = null)
    {
        var affected = item is null
            ? this.loadedDetails.ToList()
            : this.loadedDetails.Contains(item) ? new List<object> { item } : new List<object>();

        if (item is null)
            this.loadedDetails.Clear();
        else
            this.loadedDetails.Remove(item);

        this.RebuildRows();
        foreach (var stale in affected.Where(this.IsRowExpanded))
            this.LoadDetailAsync(stale);
    }

    // ---------- Internals ----------

    /// <summary>True when a detail row can appear - which is also what puts the expander column in.</summary>
    bool HasRowDetail => this.RowDetailTemplate is not null;

    bool TreeEnabled => this.ChildrenSelector is not null || this.ChildrenLoader is not null;

    /// <summary>Tree carets live inline in the first column; only the detail row gets its own column.</summary>
    bool HasExpanderColumn => this.HasRowDetail;

    int LeadingColumnCount => (this.HasExpanderColumn ? 1 : 0) + (this.HasMultiSelect ? 1 : 0);

    bool ExpansionEnabled => this.HasRowDetail || this.TreeEnabled;

    bool CanExpand(object item)
    {
        if (!this.ExpansionEnabled)
            return false;
        if (this.IsRowExpandable is not null && !this.IsRowExpandable(item))
            return false;

        return this.HasRowDetail || this.HasChildrenOf(item);
    }

    bool HasChildrenOf(object item)
    {
        if (!this.TreeEnabled)
            return false;
        if (this.HasChildrenSelector is not null)
            return this.HasChildrenSelector(item);
        if (this.loadedChildren.TryGetValue(item, out var cached))
            return cached.Count > 0;

        var sync = this.SelectorChildren(item);
        if (sync is not null)
            return sync.Count > 0;

        // The selector passed on this one, so only the loader knows - offer the caret rather than
        // hide a branch that may well have something in it.
        return this.ChildrenLoader is not null;
    }

    /// <summary>What <see cref="ChildrenSelector"/> says, or null for "not mine - ask the loader".</summary>
    IReadOnlyList<object>? SelectorChildren(object item)
        => this.ChildrenSelector?.Invoke(item)?.Cast<object>().ToList();

    IReadOnlyList<object> RawChildren(object item)
        => this.loadedChildren.TryGetValue(item, out var cached)
            ? cached
            : this.SelectorChildren(item) ?? Array.Empty<object>();

    /// <summary>
    /// The loader only covers what the selector declined. Without that the loader would take over
    /// every branch, and a tree could never mix in-memory levels with fetched ones.
    /// </summary>
    bool NeedsChildrenLoad(object item)
        => this.ChildrenLoader is not null
            && !this.loadedChildren.ContainsKey(item)
            && this.SelectorChildren(item) is null;

    bool NeedsDetailLoad(object item)
        => this.RowDetailLoader is not null
            && this.HasRowDetail
            && !this.loadedDetails.Contains(item)
            && !this.loadingDetails.Contains(item);

    async void LoadChildrenAsync(object item)
    {
        if (this.ChildrenLoader is null)
            return;

        this.loadingChildren.Add(item);
        this.OnBusyChanged();
        this.RebuildRows();
        try
        {
            var children = await this.ChildrenLoader(item).ConfigureAwait(true);
            this.loadedChildren[item] = children is null
                ? Array.Empty<object>()
                : children.Cast<object>().ToList();
        }
        catch (Exception ex)
        {
            this.expandedItems.Remove(item);
            this.ChildrenLoadFailed?.Invoke(this, new DataGridLoadFailedEventArgs(item, ex));
        }
        finally
        {
            this.loadingChildren.Remove(item);
            this.OnBusyChanged();
            this.RebuildRows();
        }
    }

    async void LoadDetailAsync(object item)
    {
        if (this.RowDetailLoader is null)
            return;

        this.loadingDetails.Add(item);
        this.OnBusyChanged();
        this.RebuildRows();
        try
        {
            await this.RowDetailLoader(item).ConfigureAwait(true);
            this.loadedDetails.Add(item);
        }
        catch (Exception ex)
        {
            this.expandedItems.Remove(item);
            this.RowDetailLoadFailed?.Invoke(this, new DataGridLoadFailedEventArgs(item, ex));
        }
        finally
        {
            this.loadingDetails.Remove(item);
            this.OnBusyChanged();
            this.RebuildRows();
        }
    }

    void OnBusyChanged()
    {
        var busy = this.loadingChildren.Count > 0 || this.loadingDetails.Count > 0;
        if (busy == this.IsBusy)
            return;

        this.IsBusy = busy;
        this.IsBusyChanged?.Invoke(this, busy);
    }

    /// <summary>Every item reachable without a load - the roots plus any already-loaded subtree.</summary>
    IEnumerable<object> EnumerateKnownItems()
    {
        foreach (var root in this.ProcessedData())
        {
            yield return root;
            if (!this.TreeEnabled)
                continue;

            foreach (var descendant in this.Descendants(root))
                yield return descendant;
        }
    }

    IEnumerable<object> Descendants(object item)
    {
        if (this.NeedsChildrenLoad(item))
            yield break;

        foreach (var child in this.RawChildren(item))
        {
            yield return child;
            foreach (var descendant in this.Descendants(child))
                yield return descendant;
        }
    }

    /// <summary>
    /// Appends a subtree to the flattened display list. Each level is filtered and sorted on its own -
    /// sorting a tree globally would tear children away from their parents.
    /// </summary>
    int AppendTreeRows(IReadOnlyList<object> items, int level, int index)
    {
        foreach (var item in items)
        {
            var row = this.CreateRow(item, level, index++);
            this.dataRows.Add(row);
            this.displayItems.Add(row);
            this.AppendDetailRow(row);

            if (row.HasChildren && row.IsExpanded && !this.loadingChildren.Contains(item))
                index = this.AppendTreeRows(this.ProcessLevel(this.RawChildren(item)), level + 1, index);
        }
        return index;
    }

    DataGridRow CreateRow(object item, int level, int index)
    {
        var row = new DataGridRow(item)
        {
            Index = index,
            Level = level,
            HasChildren = this.HasChildrenOf(item),
            HasDetail = this.HasRowDetail && (this.IsRowExpandable?.Invoke(item) ?? true),
            IsExpanded = this.IsRowExpanded(item),
            IsLoadingChildren = this.loadingChildren.Contains(item),
            IsLoadingDetail = this.loadingDetails.Contains(item)
        };
        row.PropertyChanged += this.OnRowPropertyChanged;
        return row;
    }

    void AppendDetailRow(DataGridRow row)
    {
        if (row.HasDetail && row.IsExpanded)
            this.displayItems.Add(new DataGridDetailRow(row.Data, this.loadingDetails.Contains(row.Data)));
    }

    // ---------- Views ----------

    /// <summary>The caret in the leading expander column, shown for rows that can open a detail row.</summary>
    View BuildExpanderCell()
    {
        var caret = new Label
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        }.WithFontSize(ShinyThemeKeys.Type.TitleMediumSize);
        caret.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurfaceVariant);
        caret.SetBinding(Label.TextProperty, static (DataGridRow r) => r.DetailCaretGlyph);
        caret.SetBinding(VisualElement.IsVisibleProperty, static (DataGridRow r) => r.ShowDetailCaret);

        // The whole cell is the target, not just the glyph - a caret-sized hit box is a miss on a phone.
        var host = new Grid { MinimumHeightRequest = 44 };
        host.Add(caret);
        host.Add(BuildBusySpinner(children: false));
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, _) =>
        {
            if (((View)s!).BindingContext is DataGridRow row && row.HasDetail)
                this.ToggleRow(row.Data);
        };
        host.GestureRecognizers.Add(tap);
        return host;
    }

    /// <summary>Wraps a tree column's cell with its indent and expand caret.</summary>
    View WrapTreeCell(View cell, out View toggle)
    {
        var indent = new BoxView { BackgroundColor = Colors.Transparent, HeightRequest = 1 };
        indent.SetBinding(VisualElement.WidthRequestProperty, static (DataGridRow r) => r.Level,
            converter: new DataGridIndentConverter(this.TreeIndentSize));

        var caret = new Label
        {
            WidthRequest = TreeCaretSize,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        }.WithFontSize(ShinyThemeKeys.Type.TitleMediumSize);
        caret.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurfaceVariant);
        caret.SetBinding(Label.TextProperty, static (DataGridRow r) => r.TreeCaretGlyph);
        caret.SetBinding(VisualElement.IsVisibleProperty, static (DataGridRow r) => r.ShowTreeCaret);

        var caretHost = new Grid { WidthRequest = TreeCaretSize, MinimumHeightRequest = TreeCaretSize };
        caretHost.Add(caret);
        caretHost.Add(BuildBusySpinner(children: true));
        var tap = new TapGestureRecognizer();
        tap.Tapped += (s, _) =>
        {
            if (((View)s!).BindingContext is DataGridRow row && row.HasChildren)
                this.ToggleRow(row.Data);
        };
        caretHost.GestureRecognizers.Add(tap);
        toggle = caretHost;

        var grid = new Grid
        {
            ColumnSpacing = 0,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            }
        };
        grid.Add(indent, 0, 0);
        grid.Add(caretHost, 1, 0);
        grid.Add(cell, 2, 0);
        return grid;
    }

    /// <summary>
    /// The spinner that takes the caret's place while that row's load runs - the caret *is* the
    /// button, so the progress belongs on it rather than somewhere else on the row.
    /// </summary>
    static ActivityIndicator BuildBusySpinner(bool children)
    {
        var spinner = new ActivityIndicator
        {
            WidthRequest = 14,
            HeightRequest = 14,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        spinner.SetDynamicResource(ActivityIndicator.ColorProperty, ShinyThemeKeys.Color.Primary);
        // Typed bindings need the lambda written at the call, so the two loads branch here rather
        // than passing a property name in.
        if (children)
        {
            spinner.SetBinding(ActivityIndicator.IsRunningProperty, static (DataGridRow r) => r.IsLoadingChildren);
            spinner.SetBinding(VisualElement.IsVisibleProperty, static (DataGridRow r) => r.IsLoadingChildren);
        }
        else
        {
            spinner.SetBinding(ActivityIndicator.IsRunningProperty, static (DataGridRow r) => r.IsLoadingDetail);
            spinner.SetBinding(VisualElement.IsVisibleProperty, static (DataGridRow r) => r.IsLoadingDetail);
        }
        return spinner;
    }

    /// <summary>The detail row while its loader runs.</summary>
    View BuildDetailLoadingRowView()
    {
        View content;
        if (this.RowDetailLoadingTemplate is not null)
        {
            content = (View)this.RowDetailLoadingTemplate.CreateContent();
            content.SetBinding(BindableObject.BindingContextProperty, static (DataGridDetailRow r) => r.Data);
        }
        else
        {
            var spinner = new ActivityIndicator
            {
                IsRunning = true,
                WidthRequest = 18,
                HeightRequest = 18,
                HorizontalOptions = LayoutOptions.Start,
                VerticalOptions = LayoutOptions.Center
            };
            spinner.SetDynamicResource(ActivityIndicator.ColorProperty, ShinyThemeKeys.Color.Primary);
            content = spinner;
        }

        return this.WrapDetailRow(content);
    }

    /// <summary>
    /// The full-width detail row. Its content is pinned to the leading edge under
    /// <see cref="HorizontalScroll"/> for the same reason a group header is - a row that spans every
    /// column would otherwise slide out of view with the columns.
    /// </summary>
    View BuildDetailRowView()
    {
        var content = this.RowDetailTemplate is null
            ? new Grid()
            : (View)this.RowDetailTemplate.CreateContent();

        content.SetBinding(BindableObject.BindingContextProperty, static (DataGridDetailRow r) => r.Data);
        return this.WrapDetailRow(content);
    }

    View WrapDetailRow(View content)
    {
        var inner = new Grid { Padding = this.CellPadding };
        inner.Add(content);
        if (this.FrozenEnabled)
            this.TrackPane(inner, start: true);

        var bottomLine = new BoxView { HeightRequest = 1, VerticalOptions = LayoutOptions.End };
        bottomLine.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.OutlineVariant);

        var container = new Grid();
        container.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.SurfaceContainerLow);
        container.Add(inner);
        container.Add(bottomLine);
        return container;
    }
}

/// <summary>Reports a failed <see cref="DataGrid.ChildrenLoader"/> or <see cref="DataGrid.RowDetailLoader"/>.</summary>
public sealed class DataGridLoadFailedEventArgs : EventArgs
{
    public DataGridLoadFailedEventArgs(object item, Exception exception)
    {
        this.Item = item;
        this.Exception = exception;
    }

    public object Item { get; }
    public Exception Exception { get; }
}

/// <summary>Turns a row's hierarchy level into the width of its indent spacer.</summary>
sealed class DataGridIndentConverter : IValueConverter
{
    readonly double indentSize;

    public DataGridIndentConverter(double indentSize) => this.indentSize = indentSize;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int level ? level * this.indentSize : 0d;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
