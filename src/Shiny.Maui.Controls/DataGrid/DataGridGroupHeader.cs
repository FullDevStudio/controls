namespace Shiny.Maui.Controls.DataGrid;

/// <summary>
/// A group header row in the flattened item list. Also the BindingContext of
/// <see cref="DataGrid.GroupHeaderTemplate"/>.
/// </summary>
public sealed class DataGridGroupHeader
{
    internal DataGridGroupHeader(
        object? key,
        string? keyText,
        string title,
        int count,
        bool collapsed,
        IReadOnlyList<object> items,
        int level,
        string path
    )
    {
        this.Key = key;
        this.KeyText = keyText;
        this.Title = title;
        this.Count = count;
        this.Collapsed = collapsed;
        this.Items = items;
        this.Level = level;
        this.Path = path;
    }

    public object? Key { get; }

    /// <summary>
    /// The key run through the grouped column's formatting, so a group header reads the same as the
    /// cells under it ("Salary: $45,000", not "Salary: 45000"). <c>Key</c> stays the raw value because
    /// it is what a caller matches on.
    /// </summary>
    public string? KeyText { get; }

    /// <summary>The grouped column's title.</summary>
    public string Title { get; }

    /// <summary>How many rows are in this group - including every row of its nested groups.</summary>
    public int Count { get; }

    public bool Collapsed { get; }

    public bool IsExpanded => !this.Collapsed;

    /// <summary>The rows in this group, nested groups included.</summary>
    public IReadOnlyList<object> Items { get; }

    /// <summary>Nesting depth - 0 for the outermost grouping level.</summary>
    public int Level { get; }

    /// <summary>
    /// Identity of this group among its siblings <i>and</i> its ancestors ("Sales" then "West"). Collapse
    /// state is tracked by it, so two nested groups that happen to share a key stay independent.
    /// </summary>
    public string Path { get; }

    /// <summary>Position among the grid's group headers in display order; names the header for UI tests.</summary>
    internal int Index { get; init; }

    public string CaretGlyph => this.Collapsed ? "▸" : "▾";

    public string Display => $"{this.Title}: {this.KeyText ?? this.Key?.ToString()} ({this.Count})";
}


/// <summary>A summary row in the flattened item list - the grid's own, or one group's.</summary>
sealed class DataGridSummaryRowItem
{
    public DataGridSummaryRowItem(DataGridSummaryRow definition, IReadOnlyList<object> items, DataGridGroupHeader? group)
    {
        this.Definition = definition;
        this.Items = items;
        this.Group = group;
    }

    public DataGridSummaryRow Definition { get; }
    public IReadOnlyList<object> Items { get; }
    public DataGridGroupHeader? Group { get; }

    public int Level => this.Group?.Level + 1 ?? 0;

    public DataGridSummaryContext Context
        => new(this.Items, this.Group is not null, this.Group?.Key, this.Group?.KeyText, this.Group?.Level ?? 0);

    /// <summary>The text for one column's slot, or null when this row leaves that column blank.</summary>
    public string? TextFor(DataGridColumn column)
    {
        var cell = this.Definition.CellFor(column);
        return cell?.ComputeText(column, this.Items);
    }
}


sealed class DataGridItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate RowTemplate { get; set; } = default!;
    public DataTemplate GroupTemplate { get; set; } = default!;
    /// <summary>One template per summary row definition - see <c>DataGrid.BuildSummaryTemplates</c>.</summary>
    public Dictionary<DataGridSummaryRow, DataTemplate> SummaryTemplates { get; set; } = new();

    public DataTemplate BlankTemplate { get; set; } = default!;
    public DataTemplate EditRowTemplate { get; set; } = default!;
    public DataTemplate DetailTemplate { get; set; } = default!;
    public DataTemplate DetailLoadingTemplate { get; set; } = default!;

    protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
    {
        if (item is DataGridGroupHeader)
            return this.GroupTemplate;
        if (item is DataGridSummaryRowItem summary)
        {
            return this.SummaryTemplates.TryGetValue(summary.Definition, out var template)
                ? template
                : this.BlankTemplate;
        }
        if (item is DataGridDetailRow detail)
            return detail.IsLoading ? this.DetailLoadingTemplate : this.DetailTemplate;
        if (item is DataGridRow { IsEditing: true })
            return this.EditRowTemplate;
        return this.RowTemplate;
    }
}
