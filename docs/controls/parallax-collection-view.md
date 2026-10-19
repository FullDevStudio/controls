# ParallaxCollectionView (MAUI) / ParallaxList (Blazor)

[← All Shiny Controls](../../README.md)

A scrollable list with a hero header that translates at a configurable fraction of the scroll offset — the App-Store / profile-page parallax effect. Pure cross-platform implementation: MAUI wraps a real `CollectionView` and drives the hero from `CollectionView.Scrolled` (no platform handlers); Blazor uses a small JS scroll listener that mutates `transform`/`opacity` directly via `requestAnimationFrame`, so the parallax runs at native scroll framerate without re-rendering Razor components.

```xml
<shiny:ParallaxCollectionView ItemsSource="{Binding Items}"
                              HeaderHeight="260"
                              MinHeaderHeight="96"
                              ParallaxFactor="0.5"
                              CollapseToSticky="True"
                              FadeHeaderOnScroll="False"
                              SelectionMode="Single"
                              ItemSelectedCommand="{Binding SelectCommand}">
    <shiny:ParallaxCollectionView.HeaderTemplate>
        <DataTemplate>
            <Grid>
                <Grid.Background>
                    <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                        <GradientStop Color="#7C3AED" Offset="0.0" />
                        <GradientStop Color="#2563EB" Offset="0.5" />
                        <GradientStop Color="#0EA5E9" Offset="1.0" />
                    </LinearGradientBrush>
                </Grid.Background>
                <Label Text="Destinations" FontSize="28" FontAttributes="Bold"
                       TextColor="White" VerticalOptions="Center" HorizontalOptions="Center" />
            </Grid>
        </DataTemplate>
    </shiny:ParallaxCollectionView.HeaderTemplate>
    <shiny:ParallaxCollectionView.ItemTemplate>
        <DataTemplate>
            <Border Margin="16,6" Padding="16">
                <Label Text="{Binding Title}" FontAttributes="Bold" />
            </Border>
        </DataTemplate>
    </shiny:ParallaxCollectionView.ItemTemplate>
</shiny:ParallaxCollectionView>
```

```razor
<div style="height:600px;">
    <ParallaxList TItem="DestinationItem"
                  Items="@items"
                  HeaderHeight="260"
                  MinHeaderHeight="96"
                  ParallaxFactor="0.5"
                  CollapseToSticky="true"
                  Scrolled="@(e => visible = e.HeaderVisibleHeight)">
        <HeroTemplate>
            <div style="height:100%;background:linear-gradient(135deg,#7C3AED,#2563EB,#0EA5E9);
                        color:white;display:flex;align-items:center;justify-content:center;
                        font-size:28px;font-weight:700;">Destinations</div>
        </HeroTemplate>
        <ItemTemplate Context="item">
            <div style="margin:6px 16px;padding:16px;background:white;border-radius:14px;">
                <strong>@item.Title</strong>
            </div>
        </ItemTemplate>
    </ParallaxList>
</div>
```

| Property | MAUI Type | Blazor Type | Default | Description |
|---|---|---|---|---|
| `ItemsSource` / `Items` | `IEnumerable` | `IReadOnlyList<TItem>` | — | Collection of items |
| `ItemTemplate` | `DataTemplate` | `RenderFragment<TItem>` | — | Template per row |
| `HeaderTemplate` / `HeroTemplate` | `DataTemplate` | `RenderFragment` | — | Parallax hero template |
| `EmptyView` / `EmptyTemplate` | `object` / `DataTemplate` | `RenderFragment` | — | Empty state |
| `HeaderHeight` | `double` | `double` | 240 | Hero height (px) |
| `MinHeaderHeight` | `double` | `double` | 0 | Minimum visible hero height when collapsed |
| `ParallaxFactor` | `double` | `double` | 0.5 | Fraction of scroll offset applied to hero translation (0 = pinned, 1 = scrolls with content) |
| `CollapseToSticky` | `bool` | `bool` | false | Clamp hero to `MinHeaderHeight` once scrolled that far |
| `FadeHeaderOnScroll` | `bool` | `bool` | false | Fade hero from 100% → 0% opacity as it scrolls past |
| `ItemsLayout` (MAUI) | `IItemsLayout` | — | Vertical | Passthrough to inner `CollectionView` — use `GridItemsLayout` for multi-column lists |
| `SelectionMode` / `SelectedItem` / `ItemSelectedCommand` (MAUI) | — | — | — | Passthrough to inner `CollectionView` |
| `ItemSelected` (Blazor) | — | `EventCallback<TItem>` | — | Fired on row click |
| `Height` (Blazor) | — | `string` | — | CSS height for the scroll container; omit to fill parent |

Both hosts fire a `Scrolled` event with `ParallaxScrollEventArgs(verticalOffset, headerTranslation, headerVisibleHeight)` so you can drive sticky titles, fading nav chrome, etc.

When no `HeaderTemplate`/`HeroTemplate` is set, the header reserves **no** space (so you never get a blank band above the list). MAUI also exposes `ScrollTo(...)` and a `ScrollToTop(bool animate = true)` method that returns the list to the very top including the header.

### Automation ids (MAUI)

Set `AutomationId` on the `ParallaxCollectionView` and the inner list is `<id>_List` (the scroll
target) and the header host `<id>_Header`. Items are your `ItemTemplate`'s, so give them ids there
(`AutomationId="{Binding Id}"`).
