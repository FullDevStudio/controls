# VideoText

[← All Shiny Controls](../../README.md)

> Ships in the MediaElement packages: `Shiny.Maui.Controls.MediaElement` and `Shiny.Blazor.Controls.MediaElement`.

Display text whose letters are windows onto a looping video: the video plays only inside the glyphs, and everything around them is **transparent**, so whatever sits behind the control shows between the letters.

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
```

| Property | Default | |
|---|---|---|
| `Source` | `null` | A `MediaSource`; a bare string is classified as a URI, a file path or a `Resources/Raw` asset, the same as on `MediaElement` |
| `Text` | `null` | The text cut out of the video. Only explicit line breaks split it; it is never wrapped |
| `FontSize` | `0` | Device-independent units. **`0` auto-fits** the text to 90% of the control on its tighter axis |
| `FontFamily` | system | A registered MAUI font alias or a platform font name |
| `FontAttributes` | `Bold` | Thin strokes show very little video |
| `IsMuted` | `true` | Set `false` for sound |
| `FallbackTextColor` | label default | Text colour where no masked output exists (see below) |

The video is always scaled to **cover** the control (AspectFill), so set the size you want the text box to be.

### How it's drawn (and where)

The control uses the same `IMediaPlayerBackend` as `MediaElement`, with a native mask over the video output:

| Platform | Mask |
|---|---|
| iOS / Mac Catalyst | A `CALayer` mask (the text rendered to an image) on the `AVPlayerLayer` |
| macOS (AppKit) | The same, drawn through a `CGBitmapContext` |
| Android | ExoPlayer renders into a `TextureView`, and the text is composited over it with `DST_IN` in its own layer |
| Windows, Linux (GTK4) | **No mask yet.** The text is shown in `FallbackTextColor` and no video is created. `VideoText.IsMaskSupported` reports which you got |

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
| `FontFamily` | `sans-serif` | **A system font or generic family only** (see below) |
| `FontWeight` | `bold` | Any CSS `font-weight` |
| `FontStyle` | `null` | e.g. `italic` |
| `IsMuted` | `true` | |
| `Preload` | `auto` | |
| `CssClass` / `Style` | | The container defaults to `height:200px`; your `Style` is appended after the mask, so it never replaces it |

The text is an SVG used as a CSS `mask-image`. Auto-fit measures the glyphs in the browser and sizes the SVG's `viewBox`, so the mask follows any container size without a resize observer. Before the measurement arrives (a prerendered first paint, say), the widths are estimated.

- **Web fonts don't reach the mask.** An SVG loaded as an image can't see the page's `@font-face` fonts, so the mask falls back to a system font. Use an installed or generic family.
- **Sound needs a user gesture.** Browsers block audible autoplay until the user has interacted with the page. With `IsMuted="false"`, a refused `play()` falls back to muted playback, so the letters keep moving instead of freezing.
