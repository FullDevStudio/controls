using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.DataGrid;

/// <summary>
/// Summary (total) rows - under the grid, and inside each group.
/// </summary>
/// <remarks>
/// One set of <see cref="SummaryRows"/> definitions serves both: the grid's own rows aggregate every
/// processed row and are rendered under the <see cref="CollectionView"/>, while a group's rows
/// aggregate that group's items and are flattened into the item list beside the rows they total (see
/// <see cref="GroupSummaryPlacement"/>). A row that declares no cell for a column leaves it blank,
/// which is what lets "Total" sit in one column and the number in the next.
/// </remarks>
public partial class DataGrid
{
    readonly VerticalStackLayout footerStack;

    /// <summary>
    /// The row synthesized from the legacy column-level <c>Aggregate</c>/<c>FooterTemplate</c>. Cached
    /// for the length of a rebuild because the item list and the template map are keyed on the *same*
    /// definition instance - handing out a fresh one per call would leave every summary item without a
    /// template to render it.
    /// </summary>
    DataGridSummaryRow? implicitSummaryRow;
    bool implicitSummaryBuilt;

    /// <summary>
    /// Summary rows, top to bottom. Each holds <see cref="DataGridSummaryCell"/>s pointing at columns.
    /// When empty, a single row is synthesized from any column-level <c>Aggregate</c>/<c>FooterTemplate</c>.
    /// </summary>
    public ObservableCollection<DataGridSummaryRow> SummaryRows { get; } = new();

    void OnSummaryRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        this.PushBindingContextToSummaryRows();
        this.RebuildAll();
    }

    /// <summary>
    /// Same reasoning as the columns: summary rows are <see cref="BindableObject"/>s outside the visual
    /// tree, so a <c>{Binding}</c> on a cell's <c>Text</c> has nothing to resolve against unless the
    /// grid hands its context down.
    /// </summary>
    void PushBindingContextToSummaryRows()
    {
        foreach (var row in this.SummaryRows)
        {
            SetInheritedBindingContext(row, this.BindingContext);
            foreach (var cell in row.Cells)
                SetInheritedBindingContext(cell, this.BindingContext);
        }
    }

    /// <summary>
    /// The rows to render for one scope. With nothing declared, the legacy per-column
    /// <c>Aggregate</c>/<c>FooterTemplate</c> still produce the single footer row they always did.
    /// </summary>
    internal IReadOnlyList<DataGridSummaryRow> EffectiveSummaryRows(bool group)
    {
        if (this.SummaryRows.Count > 0)
            return this.SummaryRows.Where(r => r.AppliesTo(group)).ToList();

        if (!this.implicitSummaryBuilt)
        {
            this.implicitSummaryRow = this.BuildImplicitSummaryRow();
            this.implicitSummaryBuilt = true;
        }

        return this.implicitSummaryRow is null
            ? Array.Empty<DataGridSummaryRow>()
            : new[] { this.implicitSummaryRow };
    }

    void InvalidateImplicitSummaryRow()
    {
        this.implicitSummaryRow = null;
        this.implicitSummaryBuilt = false;
    }

    DataGridSummaryRow? BuildImplicitSummaryRow()
    {
        var columns = this.Columns
            .Where(c => c.IsVisible && (c.Aggregate is not null || c.FooterTemplate is not null))
            .ToList();

        if (columns.Count == 0)
            return null;

        var row = new DataGridSummaryRow();
        foreach (var column in columns)
        {
            row.Cells.Add(new DataGridSummaryCell
            {
                Column = column.Id,
                Definition = column.Aggregate,
                LegacyTemplate = column.FooterTemplate
            });
        }
        return row;
    }

    /// <summary>Emits one group's summary rows into the flattened item list.</summary>
    void AppendSummaryRows(DataGridGroupHeader header)
    {
        foreach (var definition in this.EffectiveSummaryRows(group: true))
            this.displayItems.Add(new DataGridSummaryRowItem(definition, header.Items, header));
    }

    /// <summary>One <see cref="DataTemplate"/> per definition, so each row's own alignment and weight
    /// survive <see cref="CollectionView"/> recycling - only the numbers are bound.</summary>
    Dictionary<DataGridSummaryRow, DataTemplate> BuildSummaryTemplates()
    {
        var map = new Dictionary<DataGridSummaryRow, DataTemplate>();
        foreach (var definition in this.EffectiveSummaryRows(group: true))
        {
            var captured = definition;
            map[captured] = new DataTemplate(() => this.BuildSummaryRowView(captured));
        }
        return map;
    }

    View BuildSummaryRowView(DataGridSummaryRow definition)
    {
        var grid = new Grid
        {
            ColumnDefinitions = this.BuildColumnDefinitions(),
            ColumnSpacing = 0
        };
        grid.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.SurfaceContainer);

        var topLine = new BoxView { HeightRequest = 1, VerticalOptions = LayoutOptions.Start };
        topLine.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.OutlineVariant);

        this.LayoutCells(
            grid,
            this.LeadingPlaceholders(),
            column => this.BuildSummaryCell(column, definition, item: null),
            this.StyleContainerPane
        );

        var container = new Grid();
        container.Add(grid);
        container.Add(topLine);
        return container;
    }

    /// <summary>
    /// One summary slot. <paramref name="item"/> is null inside a <see cref="DataTemplate"/> - the text
    /// is bound so a recycled view picks up whichever group lands in it - and set for the grid's own
    /// footer, which is built fresh on every rebuild and can bake the value straight in.
    /// </summary>
    View BuildSummaryCell(DataGridColumn column, DataGridSummaryRow definition, DataGridSummaryRowItem? item)
    {
        var cell = definition.CellFor(column);
        if (cell is null)
            return new Label();

        if (cell.CellTemplate is not null)
        {
            var content = (View)cell.CellTemplate.CreateContent();
            if (item is null)
                content.SetBinding(BindableObject.BindingContextProperty, static (DataGridSummaryRowItem i) => i.Context);
            else
                content.BindingContext = item.Context;
            return content;
        }

        if (cell.LegacyTemplate is not null)
        {
            var content = (View)cell.LegacyTemplate.CreateContent();
            // A column FooterTemplate predates summary rows and knows nothing about them; it keeps
            // the grid's own context rather than being handed a summary it can't bind to.
            if (item is not null)
                content.BindingContext = this.BindingContext;
            return content;
        }

        var label = new Label
        {
            FontAttributes = cell.Bold ? FontAttributes.Bold : FontAttributes.None,
            Padding = this.CellPadding,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = this.ResolveAlignment(column, cell.Alignment)
        };
        label.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurfaceVariant);

        if (item is null)
            label.SetBinding(Label.TextProperty, static (DataGridSummaryRowItem i) => i, converter: new DataGridSummaryTextConverter(column));
        else
            label.Text = item.TextFor(column);

        return label;
    }

    /// <summary>The grid's own summary rows as built views - one <see cref="Grid"/> per row.</summary>
    internal IReadOnlyList<View> FooterViews => this.footerStack.Children.OfType<View>().ToList();

    /// <summary>Rebuilds the grid's own summary rows under the item list.</summary>
    void RebuildFooter()
    {
        this.footerStack.Children.Clear();

        var definitions = this.EffectiveSummaryRows(group: false);
        this.footerWrapper.IsVisible = definitions.Count > 0;
        if (definitions.Count == 0)
            return;

        var items = this.ProcessedData();
        foreach (var definition in definitions)
        {
            var grid = new Grid
            {
                ColumnDefinitions = this.BuildColumnDefinitions(),
                ColumnSpacing = 0
            };

            var item = new DataGridSummaryRowItem(definition, items, null);
            this.LayoutCells(
                grid,
                this.LeadingPlaceholders(),
                column => this.BuildSummaryCell(column, definition, item),
                this.StyleSurfacePane
            );
            this.footerStack.Children.Add(grid);
        }
    }
}


/// <summary>Reads one column's slot out of the summary row the recycled view is currently showing.</summary>
sealed class DataGridSummaryTextConverter : IValueConverter
{
    readonly DataGridColumn column;

    public DataGridSummaryTextConverter(DataGridColumn column) => this.column = column;

    public object? Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => value is DataGridSummaryRowItem item ? item.TextFor(this.column) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}
