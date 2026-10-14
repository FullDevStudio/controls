# VideoText

Text whose letters are cut out of a looping video (Magic UI "Video Text"). Ships in the **MediaElement** add-on on both hosts — `Shiny.Maui.Controls.MediaElement` (`xmlns:media="http://shiny.net/maui/media"`, namespace `Shiny.Maui.Controls.Media`) and `Shiny.Blazor.Controls.MediaElement` (namespace `Shiny.Blazor.Controls.Media`).

Decorative only: **no transport controls, always autoplays and loops, muted by default**. Do not add play/pause buttons or wire `MediaElement` APIs to it — it exposes none.

## MAUI

Requires `.UseShinyMediaElement()` in `MauiProgram` (registers the handler).

```xml
<media:VideoText Source="https://example.com/ocean.mp4"
                 Text="OCEAN"
                 HeightRequest="200" />
```

Properties: `Source` (MediaSource, string-convertible), `Text` (use `&#10;` for line breaks — never wraps), `FontSize` (double, **0 = auto-fit**, the default), `FontFamily`, `FontAttributes` (default `Bold`), `IsMuted` (default `true`), `FallbackTextColor`.

- Always give it a size (`HeightRequest`, or a sized parent) — the video covers the control and the text fits into it.
- The area around the glyphs is transparent: put a background (gradient, image) behind it in a parent, not on the control.
- Masked on iOS, Mac Catalyst, macOS AppKit and Android. On Windows and Linux it shows plain text in `FallbackTextColor`; check `VideoText.IsMaskSupported` if a page must differ.

## Blazor

```razor
<VideoText Source="https://example.com/ocean.mp4" Text="OCEAN" Style="height:220px;" />
```

Parameters: `Source`, `Text` (`\n` for lines), `FontSize` (CSS px, 0 = auto-fit), `FontFamily` (default `sans-serif`), `FontWeight` (default `bold`), `FontStyle`, `IsMuted` (default `true`), `Preload`, `CssClass`, `Style` (container defaults to 200px tall).

- `FontFamily` must be a **system or generic** font — the mask is an SVG image and cannot use `@font-face` web fonts.
- Unmuted playback needs a user gesture; until then it plays muted rather than freezing.
