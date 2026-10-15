# VideoText

[← All Shiny Controls](../../README.md)

> Ships in the MediaElement packages: `Shiny.Maui.Controls.MediaElement` and `Shiny.Blazor.Controls.MediaElement`.

Display text whose letters are windows onto a looping video. The video plays at full strength inside the glyphs; everywhere else it sits under an **overlay** (black by default) whose colour and opacity you choose. Lower the opacity and the video shows faintly around the letters; clear the colour and everything around the letters is **transparent** instead, so whatever sits behind the control shows through. The text can stay centred or **marquee** left, right, up or down.

It's a decorative control. There's **no transport bar**, no seeking and no fullscreen. The video starts as soon as it opens and loops forever, and **sound is off** until you set `IsMuted="False"`.


## MAUI

```csharp
builder.UseShinyControls().UseShinyMediaElement(); // registers the VideoText handler too
```

```xml
<!-- xmlns:media="http://shiny.net/maui/media" -->
<media:VideoText Source="https://example.com/ocean.mp4"
                 Text="OCEAN"
                 HeightRequest="200" />

<media:VideoText Source="intro.mp4"
                 Text="BIG&#10;WAVES"
                 FontSize="72"
                 FontFamily="OpenSansSemibold"
                 IsMuted="False" />

<!-- a see-through overlay, scrolling left -->
<media:VideoText Source="intro.mp4"
                 Text="SHINY CONTROLS"
                 OverlayColor="#0F172A"
                 OverlayOpacity="0.7"
                 Marquee="Left"
                 MarqueeDuration="0:0:6"
                 HeightRequest="120" />

<!-- no overlay: transparent around the letters -->
<media:VideoText Source="intro.mp4" Text="OCEAN" OverlayColor="{x:Null}" />
```

| Property | Default | |
|---|---|---|
| `Source` | `null` | A `MediaSource`; a bare string is classified as a URI, a file path or a `Resources/Raw` asset, the same as on `MediaElement` |
| `Text` | `null` | The text cut out of the video. Only explicit line breaks split it; it is never wrapped |
| `OverlayColor` | `Black` | The sheet over the video around the letters. `null` (or a fully transparent colour) drops it and clips the video to the letters |
| `OverlayOpacity` | `1` | 0–1, clamped. Below 1 the video shows faintly around the letters |
| `Marquee` | `None` | `Left`, `Right`, `Up` or `Down` scrolls the text continuously across the control |
| `MarqueeDuration` | 8 s | One pass, from entering one edge to leaving the other |
| `FontSize` | `0` | Device-independent units. **`0` auto-fits** the text to 90% of the control on its tighter axis — or, with a marquee, on the cross axis only (the height for left/right, the width for up/down) |
| `FontFamily` | system | A registered MAUI font alias or a platform font name |
| `FontAttributes` | `Bold` | Thin strokes show very little video |
| `IsMuted` | `true` | Set `false` for sound |
| `FallbackTextColor` | label default | Text colour where no masked output exists (see below) |

The video is always scaled to **cover** the control (AspectFill), so set the size you want the text box to be.

### How it's drawn (and where)

The control uses the same `IMediaPlayerBackend` as `MediaElement`, with a native mask over the video output:

| Platform | Mask |
|---|---|
| iOS / Mac Catalyst | The text is rendered once to an image layer. With an overlay, a coloured layer sits above the `AVPlayerLayer` with the inverted text as its mask; without one, the text is the player layer's mask. A marquee animates that layer's position in Core Animation |
| macOS (AppKit) | The same, drawn through a `CGBitmapContext` |
| Android | ExoPlayer renders into a `TextureView`. The overlay is a layer with the glyphs cleared by `DST_OUT`; without one, the text is composited over the video with `DST_IN`. A marquee offsets the text each frame |
| Windows, Linux (GTK4) | **No mask yet.** The text is shown in `FallbackTextColor` on the overlay colour, the marquee moves the label, and no video is created. `VideoText.IsMaskSupported` reports which you got |

Android uses a `TextureView` because Media3's default `SurfaceView` is a separate window surface. Nothing drawn in the view hierarchy can mask it.

## Blazor

```razor
@using Shiny.Blazor.Controls.Media

<VideoText Source="https://example.com/ocean.mp4"
           Text="OCEAN"
           Style="height:220px;" />
```

| Parameter | Default | |
|---|---|---|
| `Source` | `null` | Video URL |
| `Text` | `null` | `\n` starts a new line |
| `FontSize` | `0` | CSS pixels; `0` auto-fits |
| `FontFamily` | `sans-serif` | Any CSS family with an overlay (web fonts included); system or generic only without one (see below) |
| `FontWeight` | `bold` | Any CSS `font-weight` |
| `FontStyle` | `null` | e.g. `italic` |
| `OverlayColor` | `black` | Any CSS colour. `null`, `transparent` or `none` drops the overlay for a CSS mask |
| `OverlayOpacity` | `1` | 0–1 |
| `Marquee` | `None` | `MarqueeDirection.Left` / `Right` / `Up` / `Down` |
| `MarqueeDuration` | 8 s | One pass |
| `IsMuted` | `true` | |
| `Preload` | `auto` | |
| `CssClass` / `Style` | | The container defaults to `height:200px`; your `Style` is appended after the mask, so it never replaces it |

With an overlay, the control lays an **inline SVG** over the video: a rectangle in the overlay colour with the text knocked out of it. Without one, the text is an SVG used as a CSS `mask-image` on the wrapper. Auto-fit measures the glyphs in the browser and sizes the SVG's `viewBox`, so static text follows any container size by itself; a marquee lays out in pixels, from the size a `ResizeObserver` reports, and scrolls with a CSS animation declared inside the SVG. Before the measurement arrives (a prerendered first paint, say), the widths are estimated and the text sits still.

- **Web fonts only reach the overlay.** The inline overlay can use any font the page has loaded. The no-overlay mask is an SVG loaded as an image, which can't see the page's `@font-face` fonts, so there it falls back to a system font.
- **CSS animation, not SMIL.** Chrome can leave an `<animateTransform>` in inline SVG frozen on its first frame after a re-render, so the marquee uses `@keyframes` instead, which also run inside the mask image.
- **Sound needs a user gesture.** Browsers block audible autoplay until the user has interacted with the page. With `IsMuted="false"`, a refused `play()` falls back to muted playback, so the letters keep moving instead of freezing.
