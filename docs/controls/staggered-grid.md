# StaggeredGrid

[← All Shiny Controls](../../README.md)

A Pinterest-style masonry/waterfall layout that arranges variable-height items in columns. Uses native staggered layout managers on MAUI (Android `StaggeredGridLayoutManager`, iOS custom `WaterfallLayout`, Windows `WaterfallVirtualizingLayout`) and CSS `column-count` on Blazor.

```xml
<shiny:StaggeredGrid ItemsSource="{Binding Items}"
                     ColumnCount="3"
                     ColumnSpacing="12"
                     RowSpacing="12"
                     ItemSelectedCommand="{Binding SelectCommand}">
    <shiny:StaggeredGrid.ItemTemplate>
        <DataTemplate>
            <Border BackgroundColor="{Binding Color}" HeightRequest="{Binding Height}" StrokeThickness="0">
                <Label Text="{Binding Title}" TextColor="White" Padding="12" />
            </Border>
        </DataTemplate>
    </shiny:StaggeredGrid.ItemTemplate>
</shiny:StaggeredGrid>
```

| Property | Type | Default | Description |
|---|---|---|---|
| `ColumnCount` | `int` | `2` | Number of columns (minimum 1) |
| `ColumnSpacing` | `double` | `0` | Horizontal gap between columns |
| `RowSpacing` | `double` | `0` | Vertical gap between items |

Inherits all `CollectionControlBase` properties: `ItemsSource`, `ItemTemplate`, `ItemTemplateSelector`, `HeaderTemplate`, `FooterTemplate`, `EmptyViewTemplate`, `ItemSelectedCommand`, `LoadMoreCommand`, `LoadMoreThreshold`, `ItemSpacing`.

### Automation ids (MAUI)

Set `AutomationId` on the `StaggeredGrid` and each item view that has no id of its own is named
`<id>_Item_<index>`, by the item's position in `ItemsSource`. Recycled views are renamed as they
are re-bound, and again when an insert or remove shifts the items. An id your `ItemTemplate` sets (or
binds) is left alone.
