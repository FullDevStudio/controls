# CoverFlowView

[← All Shiny Controls](../../README.md)

An album-art **Cover Flow** carousel. The current item faces you in the centre, and the items on each side tilt toward it and stack up like records in a crate. Drag or flick to browse. Tapping a side item brings it to the centre, and tapping the centre item selects it. Both hosts have it, in the **core** packages.

On MAUI it is drawn with plain MAUI transforms (`TranslationX`, `RotationY`, `Scale`, `Opacity`, `ZIndex`), not a native handler, so the same code runs on every head. On Blazor, a small JS engine drives CSS 3D transforms. Both hosts use the same geometry.

## MAUI

```xml
<shiny:CoverFlowView ItemsSource="{Binding Albums}"
                     CurrentPosition="{Binding Position}"
                     ItemWidth="180" ItemHeight="180"
                     SideAngle="60" SideScale="0.85"
                     CenterSpacing="120" ItemSpacing="44"
                     Loop="True" ShowReflection="True"
                     ItemSelectedCommand="{Binding OpenAlbumCommand}"
                     PositionChangedCommand="{Binding PositionChangedCommand}">
    <shiny:CoverFlowView.ItemTemplate>
        <DataTemplate x:DataType="local:Album">
            <Image Source="{Binding Artwork}" Aspect="AspectFill" />
        </DataTemplate>
    </shiny:CoverFlowView.ItemTemplate>
    <shiny:CoverFlowView.CaptionTemplate>
        <DataTemplate x:DataType="local:Album">
            <Label Text="{Binding Title}" HorizontalTextAlignment="Center" />
        </DataTemplate>
    </shiny:CoverFlowView.CaptionTemplate>
</shiny:CoverFlowView>
```

- **Methods:** `Next()`, `Previous()` and `GoTo(int index, bool animate = true)`.
- **Events:** `PositionChanged` (`EventHandler<int>`) and `ItemSelected` (`EventHandler<CollectionItemEventArgs>`).
- `ItemTemplate` and `CaptionTemplate` both accept a `DataTemplateSelector`.
- `ItemsSource` changes are followed through `INotifyCollectionChanged`. They do not raise `PositionChanged`.

## Blazor

```razor
<CoverFlowView TItem="Album" Items="@albums"
               ItemWidth="200" ItemHeight="200"
               Loop="true" ShowReflection="true"
               @bind-CurrentPosition="position"
               ItemSelected="OpenAlbum">
    <ItemTemplate Context="a"><img src="@a.Artwork" alt="@a.Title" /></ItemTemplate>
    <CaptionTemplate Context="a"><strong>@a.Title</strong> — @a.Artist</CaptionTemplate>
</CoverFlowView>
```

- **Methods:** `NextAsync()`, `PreviousAsync()` and `GoToAsync(int index, bool animate = true)`.
- `EmptyTemplate` is shown when `Items` is null or empty. `AriaLabel` labels the region.
- `Perspective` (px, default `800`) is Blazor-only.
- Keyboard: ←/→ move, Home/End jump to the ends, and Enter or Space selects the centre item. A horizontal trackpad swipe or Shift+wheel also scrolls.
- Only the items near the centre are rendered, so a long list stays cheap. The caption follows the centre item live while you drag.
- With `prefers-reduced-motion`, it jumps to the new item without animating.

## Properties (same names on both hosts)

| Property | Default | Notes |
|---|---|---|
| `ItemsSource` / `Items` | — | The covers |
| `ItemTemplate` | — | How each cover is drawn |
| `CaptionTemplate` | `null` | Shown once below the covers, for the centre item |
| `ItemWidth` / `ItemHeight` | `200` / `200` | Size of a cover |
| `CurrentPosition` | `0` | Index of the centre item. Two-way. Clamped, or wrapped when `Loop` is on |
| `SideAngle` | `60` | Degrees each side cover turns toward the centre. Its outer edge comes toward you |
| `SideScale` | `0.85` | Scale of a side cover |
| `SideOpacity` | `1.0` | Opacity of the farthest visible cover. The others fade toward it in steps |
| `CenterSpacing` | `130` | Distance in px from the centre cover to the first side cover. It is also how far you drag to move one item |
| `ItemSpacing` | `50` | Distance in px between stacked side covers |
| `VisibleItems` | `4` | Covers shown on each side. One more fades in at the edge. Only these are created |
| `Loop` | `false` | Wrap around. `GoTo` takes the shorter way round |
| `ShowReflection` | `false` | A mirrored copy of each cover underneath it, fading out |
| `ReflectionOpacity` | `0.3` | How strong the reflection is |
| `AnimationDuration` | `300` | ms for one step. Longer jumps take up to twice as long |
| `IsSwipeEnabled` | `true` | Turns dragging on or off. Taps and the methods still work |
| `ItemSelectedCommand` / `PositionChangedCommand` | — | MAUI only. On Blazor, use `ItemSelected` and `CurrentPositionChanged` |

## Behaviour

- The position is a fractional number while you drag. When you let go, it settles on a whole index. A flick carries on for about a quarter of a second, scaled by its speed.
- Without `Loop`, dragging past either end stretches, then springs back.
- `PositionChanged` / `CurrentPositionChanged` fire once per settled index, not on every frame.
- A drag is never treated as a tap.
- On MAUI, tapping the centre while a move is still settling (straight after a swipe, Next, or a `CurrentPosition` change) finishes the move and selects the cover arriving there; a tap on any other cover goes to that cover. On Blazor, a press during a move catches it where it is, as a finger would, and it settles from there.
- On MAUI, a tap with no position (an accessibility activation, or UI automation such as DevFlow) counts as a tap on the centre cover, so it selects that item.
- Item templates are for display. The control handles every tap itself, so a button inside a cover does not get the tap.
- With `ShowReflection` on MAUI, the template is created twice per cover. The reflection fades into the theme's Surface colour, so place the control on a Surface-coloured background.
