# VirtualizedGrid

[← All Shiny Controls](../../README.md)

A full-featured grouped grid with sticky section headers, virtualization, orientation-aware column counts, load-more, and cell padding. Uses native grid layouts on MAUI (Android `GridLayoutManager` with `StickyHeaderDecoration`, iOS `UICollectionViewCompositionalLayout` with pinned headers, Windows `ItemsRepeater` with `UniformGridLayout`) and CSS Grid with Blazor `Virtualize<T>` on Blazor (items are chunked into rows of `ColumnCount` cells and the rows are virtualized, so virtualization works correctly at any column count).

```xml
<shiny:VirtualizedGrid ItemsSource="{Binding Items}"
                       ColumnCount="3"
                       ItemSpacing="8"
                       CellPadding="4"
                       IsGroupingEnabled="True"
                       HasStickyHeaders="True"
                       ItemSelectedCommand="{Binding SelectCommand}">
    <shiny:VirtualizedGrid.GroupHeaderTemplate>
        <DataTemplate>
            <Label Text="{Binding .}" FontAttributes="Bold" Padding="8,4" />
        </DataTemplate>
    </shiny:VirtualizedGrid.GroupHeaderTemplate>
    <shiny:VirtualizedGrid.ItemTemplate>
        <DataTemplate>
            <Border BackgroundColor="{Binding Color}" StrokeThickness="0" Padding="12">
                <Label Text="{Binding Name}" TextColor="White" HorizontalTextAlignment="Center" />
            </Border>
        </DataTemplate>
    </shiny:VirtualizedGrid.ItemTemplate>
</shiny:VirtualizedGrid>
```

| Property | Type | Default | Description |
|---|---|---|---|
| `ColumnCount` | `int` | `1` | Number of grid columns |
| `PortraitColumnCount` | `int?` | `null` | Column count in portrait (uses `ColumnCount` if null) |
| `LandscapeColumnCount` | `int?` | `null` | Column count in landscape (uses `ColumnCount` if null) |
| `IsGroupingEnabled` | `bool` | `false` | Enable grouped layout with section headers |
| `GroupHeaderTemplate` | `DataTemplate` | `null` | Template for group headers |
| `HasStickyHeaders` | `bool` | `true` | Pin group headers while scrolling |
| `CellPadding` | `Thickness` | `0` | Padding inside each cell |
| `ShowLoadMoreButton` | `bool` | `false` | Show a load-more button at the end of the data |
| `LoadMoreButtonTemplate` | `DataTemplate` | `null` | Custom load-more button template; defaults to a centered "Load More" button |
| `IsLoadingMore` | `bool` | `false` | Loading state (OneWayToSource) |
| `ItemVisibleCommand` | `ICommand` | `null` | Fires when an item becomes visible |
| `ItemHiddenCommand` | `ICommand` | `null` | Fires when an item scrolls out of view |

Inherits all `CollectionControlBase` properties: `ItemsSource`, `ItemTemplate`, `ItemTemplateSelector`, `HeaderTemplate`, `FooterTemplate`, `EmptyViewTemplate`, `ItemSelectedCommand`, `LoadMoreCommand`, `LoadMoreThreshold`, `ItemSpacing`.

**Features:**
- Grouped data with sticky section headers that pin while scrolling
- Orientation-aware column count (portrait vs landscape)
- Built-in load-more button with loading state
- Item visibility tracking for analytics or lazy loading
- Full header, footer, and empty view templates

### Automation ids (MAUI)

Set `AutomationId` on the `VirtualizedGrid` and each item view that has no id of its own is named
`<id>_Item_<index>`, by the item's position in `ItemsSource` (numbered straight across the groups when grouping is on); the Load More button is `<id>_LoadMore`. Recycled views are renamed as they
are re-bound, and again when an insert or remove shifts the items. An id your `ItemTemplate` sets (or
binds) is left alone.
