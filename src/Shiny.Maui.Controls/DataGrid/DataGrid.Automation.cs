using System.Runtime.CompilerServices;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.DataGrid;

/// <summary>
/// Names the grid's parts after its <see cref="Element.AutomationId"/> for UI tests. Rows and cells
/// are recycled by the CollectionView, so they are named from the row they are showing (its
/// <see cref="DataGridRow.Index"/> - the position on the current page) and renamed every time the
/// CollectionView binds them to another one.
/// </summary>
public partial class DataGrid
{
    /// <summary>Appended to the grid's <see cref="Element.AutomationId"/>, with the visible column's index, to name each header cell (the sort tap target).</summary>
    public const string HeaderAutomationIdSuffix = "_Header";

    /// <summary>Appended to the grid's id, with the column index, to name a header's filter-menu glyph.</summary>
    public const string HeaderFilterAutomationIdSuffix = "_HeaderFilter";

    /// <summary>Appended to the grid's id, with the column index, to name a header's group-by glyph.</summary>
    public const string HeaderGroupAutomationIdSuffix = "_HeaderGroup";

    /// <summary>Appended to the grid's id, with the column index, to name a header's move-left arrow.</summary>
    public const string HeaderMoveLeftAutomationIdSuffix = "_HeaderMoveLeft";

    /// <summary>Appended to the grid's id, with the column index, to name a header's move-right arrow.</summary>
    public const string HeaderMoveRightAutomationIdSuffix = "_HeaderMoveRight";

    /// <summary>Appended to the grid's id, with the column index, to name a header's resize handle.</summary>
    public const string HeaderResizeAutomationIdSuffix = "_HeaderResize";

    /// <summary>Appended to the grid's id to name the header's select-all check box.</summary>
    public const string SelectAllAutomationIdSuffix = "_SelectAll";

    /// <summary>Appended to the grid's id, with the column index, to name each filter-row entry.</summary>
    public const string FilterEntryAutomationIdSuffix = "_FilterEntry";

    /// <summary>Appended to the grid's id to name the toolbar's quick-search entry.</summary>
    public const string SearchAutomationIdSuffix = "_Search";

    /// <summary>Appended to the grid's id to name the column filter popup.</summary>
    public const string FilterPopupAutomationIdSuffix = "_FilterPopup";

    /// <summary>Appended to the grid's id to name the filter popup's operator picker.</summary>
    public const string FilterOperatorAutomationIdSuffix = "_FilterOperator";

    /// <summary>Appended to the grid's id to name the filter popup's value entry.</summary>
    public const string FilterValueAutomationIdSuffix = "_FilterValue";

    /// <summary>Appended to the grid's id to name the filter popup's Clear button.</summary>
    public const string FilterClearAutomationIdSuffix = "_FilterClear";

    /// <summary>Appended to the grid's id to name the filter popup's Apply button.</summary>
    public const string FilterApplyAutomationIdSuffix = "_FilterApply";

    /// <summary>Appended to the grid's id, with the row's index on the page, to name each data row (the selection tap target).</summary>
    public const string RowAutomationIdSuffix = "_Row";

    /// <summary>Appended to the grid's id, with the row index, to name a row's multi-select check box.</summary>
    public const string RowCheckBoxAutomationIdSuffix = "_RowCheckBox";

    /// <summary>Appended to the grid's id, with the row index, to name a row's detail-row expander.</summary>
    public const string RowExpandAutomationIdSuffix = "_RowExpand";

    /// <summary>Appended to the grid's id, with the row index, to name a tree row's expand/collapse caret.</summary>
    public const string RowToggleAutomationIdSuffix = "_RowToggle";

    /// <summary>
    /// Appended to the grid's id to name each cell as <c>&lt;id&gt;_Cell_&lt;row&gt;_&lt;column&gt;</c>. A text
    /// cell's label carries it (so its text can be read); a templated cell's root does.
    /// </summary>
    public const string CellAutomationIdSuffix = "_Cell";

    /// <summary>Appended to the grid's id to name the row being edited inline.</summary>
    public const string EditRowAutomationIdSuffix = "_EditRow";

    /// <summary>Appended to the grid's id, with the column index, to name each editor in the row being edited.</summary>
    public const string EditAutomationIdSuffix = "_Edit";

    /// <summary>Appended to the grid's id to name the inline edit bar's Save button.</summary>
    public const string SaveAutomationIdSuffix = "_Save";

    /// <summary>Appended to the grid's id to name the inline edit bar's Cancel button.</summary>
    public const string CancelAutomationIdSuffix = "_Cancel";

    /// <summary>Appended to the grid's id, with the group's index in display order, to name each group header (the collapse tap target).</summary>
    public const string GroupAutomationIdSuffix = "_Group";

    /// <summary>Appended to the grid's id to name each footer summary cell as <c>&lt;id&gt;_Footer_&lt;summary row&gt;_&lt;column&gt;</c>.</summary>
    public const string FooterAutomationIdSuffix = "_Footer";

    /// <summary>Appended to the grid's id to name the pager's range label ("1-20 of 85").</summary>
    public const string PageRangeAutomationIdSuffix = "_PageRange";

    /// <summary>Appended to the grid's id to name the pager's first-page button.</summary>
    public const string PageFirstAutomationIdSuffix = "_PageFirst";

    /// <summary>Appended to the grid's id to name the pager's previous-page button.</summary>
    public const string PagePreviousAutomationIdSuffix = "_PagePrevious";

    /// <summary>Appended to the grid's id to name the pager's next-page button.</summary>
    public const string PageNextAutomationIdSuffix = "_PageNext";

    /// <summary>Appended to the grid's id to name the pager's last-page button.</summary>
    public const string PageLastAutomationIdSuffix = "_PageLast";

    /// <summary>Appended to the grid's id to name the loading overlay.</summary>
    public const string LoadingAutomationIdSuffix = "_Loading";

    // Each named part keeps its namer alive through this table (keyed on the part), and the grid only
    // holds the namers weakly - so a header rebuilt away or a row the CollectionView dropped is
    // collected as usual, and a change of the grid's id renames whatever is still alive.
    readonly ConditionalWeakTable<Element, Action> partNamers = new();
    readonly List<WeakReference<Action>> liveNamers = new();
    int pruneNamersAt = 256;

    void InitAutomationIds()
    {
        AutomationIds.Link(this, this.quickSearchEntry, SearchAutomationIdSuffix);
        AutomationIds.Link(this, this.filterPopup, FilterPopupAutomationIdSuffix);
        AutomationIds.Link(this, this.loadingOverlay, LoadingAutomationIdSuffix);
        if (this.pagerRangeLabel is not null)
            AutomationIds.Link(this, this.pagerRangeLabel, PageRangeAutomationIdSuffix);
        if (this.pagerFirst is not null)
            AutomationIds.Link(this, this.pagerFirst, PageFirstAutomationIdSuffix);
        if (this.pagerPrev is not null)
            AutomationIds.Link(this, this.pagerPrev, PagePreviousAutomationIdSuffix);
        if (this.pagerNext is not null)
            AutomationIds.Link(this, this.pagerNext, PageNextAutomationIdSuffix);
        if (this.pagerLast is not null)
            AutomationIds.Link(this, this.pagerLast, PageLastAutomationIdSuffix);

        AutomationIds.Track(this, this.RenameParts);
    }

    /// <summary>Names <paramref name="part"/> now and again whenever the grid's id changes.</summary>
    void NamePart(Element part, Action apply)
    {
        this.partNamers.AddOrUpdate(part, apply);
        this.liveNamers.Add(new WeakReference<Action>(apply));
        if (this.liveNamers.Count >= this.pruneNamersAt)
        {
            this.liveNamers.RemoveAll(w => !w.TryGetTarget(out _));
            this.pruneNamersAt = Math.Max(256, this.liveNamers.Count * 2);
        }
        apply();
    }

    void NamePart(Element part, string suffix)
        => this.NamePart(part, () => AutomationIds.Set(part, AutomationIds.For(this, suffix)));

    void NamePart(Element part, string suffix, int index)
        => this.NamePart(part, () => AutomationIds.Set(part, AutomationIds.For(this, suffix, index)));

    void NamePart(Element part, string suffix, int index, int column)
        => this.NamePart(part, () => AutomationIds.Set(part, IndexedId(suffix, index, column)));

    string? IndexedId(string suffix, int index, int column)
        => AutomationIds.For(this, suffix, index) is { } b ? $"{b}_{column}" : null;

    void RenameParts()
    {
        foreach (var weak in this.liveNamers.ToList())
        {
            if (weak.TryGetTarget(out var apply))
                apply();
        }
        this.liveNamers.RemoveAll(w => !w.TryGetTarget(out _));
    }

    /// <summary>
    /// Names a recycled data row and its parts from whichever <see cref="DataGridRow"/> it is bound to,
    /// and again on every re-bind. <paramref name="parts"/> carries a column index of -1 for a part
    /// that is per row rather than per cell.
    /// </summary>
    void NameRow(View root, List<(Element Part, string Suffix, int Column)> parts)
    {
        void Apply()
        {
            var index = (root.BindingContext as DataGridRow)?.Index;
            AutomationIds.Set(root, index is int i ? AutomationIds.For(this, RowAutomationIdSuffix, i) : null);
            foreach (var (part, suffix, column) in parts)
            {
                var id = index is not int r ? null
                    : column < 0 ? AutomationIds.For(this, suffix, r)
                    : this.IndexedId(suffix, r, column);
                AutomationIds.Set(part, id);
            }
        }

        root.BindingContextChanged += (_, _) => Apply();
        this.NamePart(root, Apply);
    }

    /// <summary>Same as <see cref="NameRow"/> for a recycled group header, by its <see cref="DataGridGroupHeader.Index"/>.</summary>
    void NameGroupHeader(View root)
    {
        void Apply()
            => AutomationIds.Set(root, root.BindingContext is DataGridGroupHeader h
                ? AutomationIds.For(this, GroupAutomationIdSuffix, h.Index)
                : null);

        root.BindingContextChanged += (_, _) => Apply();
        this.NamePart(root, Apply);
    }

    int ColumnIndex(DataGridColumn column) => IndexOf(this.VisibleColumns, column);
}
