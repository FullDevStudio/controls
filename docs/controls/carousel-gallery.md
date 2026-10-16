# CarouselGallery

[← All Shiny Controls](../../README.md)

A Netflix-style horizontal carousel with snap-to-center behavior, configurable scale transforms for focused/unfocused items, peek area insets, and position tracking. Uses native platform recycler views on MAUI (Android `RecyclerView`, iOS `UICollectionView`, Windows `ItemsRepeater`) and CSS `scroll-snap` on Blazor.

```xml
<shiny:CarouselGallery ItemsSource="{Binding Items}"
                       ItemWidth="280"
                       ItemHeight="160"
                       ItemSpacing="16"
                       PeekAreaInsets="40"
                       FocusedItemScale="1.0"
                       UnfocusedItemScale="0.85"
                       CurrentPosition="{Binding Position}"
                       ItemSelectedCommand="{Binding SelectCommand}"
                       HeightRequest="180">
    <shiny:CarouselGallery.ItemTemplate>
        <DataTemplate>
            <Border BackgroundColor="{Binding Color}" StrokeThickness="0">
                <Label Text="{Binding Title}" TextColor="White" HorizontalTextAlignment="Center" VerticalTextAlignment="Center" />
            </Border>
        </DataTemplate>
    </shiny:CarouselGallery.ItemTemplate>
</shiny:CarouselGallery>
```

| Property | Type | Default | Description |
|---|---|---|---|
| `FocusedItemScale` | `double` | `1.0` | Scale of the centered item |
| `UnfocusedItemScale` | `double` | `0.8` | Scale of off-center items |
| `ItemWidth` | `double` | required | Width of each carousel item |
| `ItemHeight` | `double` | required | Height of each carousel item |
| `CurrentPosition` | `int` | `0` | Current centered item index (TwoWay) |
| `PeekAreaInsets` | `Thickness` | `0` | Visible area of adjacent items (left and right; the item is centred vertically in the carousel) |
| `IsInfinite` | `bool` | `false` | Enable infinite loop scrolling |
| `SnapCount` | `int` | `1` | Number of items to snap into view at once. Set to `0` for free-scroll (Netflix-style) with no snapping |
| `PositionChangedCommand` | `ICommand` | `null` | Fires when position changes |

**Features:**
- Snap-to-center with smooth deceleration (configurable via `SnapCount`)
- Free-scroll mode (`SnapCount="0"`) for Netflix-style browsing without snapping
- Scale transforms for focused/unfocused items
- Peek area insets to show adjacent items
- Two-way position binding
- Infinite loop mode (MAUI)
- Dot indicators (Blazor)
