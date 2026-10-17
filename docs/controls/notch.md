# NotchView

[← All Shiny Controls](../../README.md)

A Dynamic Island / camera-notch style surface that holds **anything** and morphs its width, height and corner radius to fit it. Available on both MAUI and Blazor, in the core packages.

There are no compact/expanded states to manage. Put a view inside it and the notch sizes itself to that view. Swap the content, or let the content change its own size (text grows, a row appears), and the notch springs to the new size. During the morph the content stays laid out at its final size and the surface clips it, so text never re-wraps frame by frame.

```xml
<Grid>
    <ScrollView>
        <!-- page content -->
    </ScrollView>

    <!-- Same Grid cell: the notch floats over the top of the page, top-centre by default -->
    <shiny:NotchView x:Name="Notch" Margin="0,12,0,0" Tapped="OnNotchTapped">
        <HorizontalStackLayout Spacing="8">
            <Ellipse Fill="Red" WidthRequest="10" HeightRequest="10" VerticalOptions="Center" />
            <Label Text="0:42" TextColor="White" FontAttributes="Bold" />
        </HorizontalStackLayout>
    </shiny:NotchView>
</Grid>
```

```csharp
// swap to bigger content - the notch grows to fit, the new content fades in
Notch.Content = new NowPlayingView();
Notch.Content = null; // back to the minimum size
```

```razor
<NotchView ContentKey="@state" OnClick="Expand" OnNotchSizeChanged="s => size = s">
    @if (state == "recording")
    {
        <span>● 0:42</span>
    }
    else
    {
        <NowPlaying />
    }
</NotchView>
```

| Property | Type (MAUI / Blazor) | Default | Description |
|---|---|---|---|
| Content / ChildContent | View / RenderFragment | null | Anything. The notch sizes itself to it |
| ContentKey | — / object | null | Blazor only. Change it when you swap content and the new content fades in (MAUI fades on any `Content` swap) |
| Shape | NotchShape | Island | `Island` rounds every corner; `Notch` squares the top corners so it hangs from the top edge |
| SurfaceColor | Color / string | Black | The fill. Black on every theme (it imitates a hole in the screen), so give the content light text. Blazor: `--shiny-notch-surface` |
| Stroke / StrokeThickness | Color? / string, double | none | Optional outline |
| ContentPadding | Thickness / string | 14,8 / "8px 14px" | Space between the edge and the content |
| MinNotchWidth / MinNotchHeight | double | 120 / 36 | The smallest size, and the size with no content |
| MaxNotchWidth / MaxNotchHeight | double / double? | unbounded | Caps. The parent's width also caps the notch; wider content wraps, taller content is clipped |
| CornerRadius | double | -1 | `-1` is automatic: a full pill while short, capped at 28 as it grows tall. Always clamped to half the height |
| MorphDuration | uint / int | 420 | Milliseconds per resize. `0` snaps |
| MorphEasing | Easing? / — | soft spring | MAUI only. Blazor uses a matching `cubic-bezier` |
| HasShadow | bool | true | Soft drop shadow |
| Command / CommandParameter | ICommand / — | null | MAUI: run on a tap of the surface |
| NotchSize / Size | Size / NotchSize | — | Read-only: the size it is at, or morphing towards |

| Event | MAUI | Blazor |
|---|---|---|
| Surface tapped | `Tapped` | `OnClick` (clicks inside the content bubble here too; use `@onclick:stopPropagation` on inner buttons) |
| Morph started, with the target size | `NotchSizeChanged` | `OnNotchSizeChanged` |
| Morph finished | `MorphCompleted` | — |

MAUI also has `Refresh()`, for a size change that does not invalidate measure (a font loaded late).

**Placement.** It positions like any other view: `HorizontalOptions="Center"` and `VerticalOptions="Start"` by default on MAUI, `margin-inline: auto` on Blazor. It sizes to its content and never scrolls, so put it next to the page's scroller (same Grid cell), not inside a fixed-height box.

**Accessibility.** Blazor turns the morph and the fade off under `prefers-reduced-motion`.

## In the MacBook camera notch (macOS)

`Shiny.Maui.Controls.Desktop` can put the same `NotchView` into the **hardware camera notch**, in a borderless window above the menu bar and every other app. The empty notch is exactly the size of the cut-out, so it disappears into it; give it content and it grows down out of the notch. On a display without a notch it floats as an island under the menu bar. See [Desktop → Notch window](desktop.md#notch-window-macos).

```csharp
#if MACOS
builder.UseDesktopNotch(o => o.MaxWidth = 520);

await notchWindow.ShowAsync(new Label { Text = "Recording 0:42", TextColor = Colors.White });
await notchWindow.SetContentAsync(new NowPlayingView());
#endif
```

**Platform notes**

- The desktop notch window is macOS AppKit (`net10.0-macos`) only. The API does not exist on other targets, so guard it with `#if MACOS`.
- On the AppKit head the window, not the surface, animates the resize (a native frame animation). A content swap there builds a fresh page, because AppKit never realizes a child added to an already-realized layout.
- The window never takes keyboard focus. It is for glancing and clicking, not typing.

<!-- TODO: capture screenshots for notch -->
