# ImageViewer

[← All Shiny Controls](../../README.md)

A full-screen image overlay with pinch-to-zoom, pan, double-tap zoom, and animated open/close transitions.

| Gallery | Viewer |
|:---:|:---:|
| ![Gallery](../../assets/imageviewer1.png) | ![Viewer](../../assets/imageviewer2.png) |

```xml
<Grid>
    <!-- Page content with tappable images -->
    <ScrollView>
        <VerticalStackLayout>
            <Image Source="photo.png">
                <Image.GestureRecognizers>
                    <TapGestureRecognizer Command="{Binding OpenViewerCommand}"
                                          CommandParameter="photo.png" />
                </Image.GestureRecognizers>
            </Image>
        </VerticalStackLayout>
    </ScrollView>

    <!-- ImageViewer overlays on top -->
    <shiny:ImageViewer Source="{Binding SelectedImage}"
                       IsOpen="{Binding IsViewerOpen}" />
</Grid>
```

**MAUI: where the lightbox is drawn.** The viewer is two things — a thumbnail, which is the control you place, and a full-screen lightbox, which is injected somewhere else when `IsOpen` goes true. If the viewer sits inside an `OverlayHost` (or a `ShinyContentPage`, which has one), that host is used. Otherwise it goes into a page-wide overlay layer that `ImageViewer` installs on the `ContentPage` itself, so the lightbox covers the page no matter what the page's layout is. The wrapper is installed when the viewer loads rather than when it opens — creating it re-parents the page's content, which would reset scroll positions if it happened on the tap.

> Before 1.3.0 the page fallback was "whatever `Grid` happens to be the page's root content". A page whose content was not a `Grid` had no host and opening silently did nothing; a root `Grid` with more than one cell got the full-screen lightbox dropped into cell (0,0).

**MAUI: remote images.** Both the thumbnail and the full-screen overlay are a `ShinyImage`, so setting `Uri` instead of `Source` brings the whole loading pipeline with it — placeholder artwork, a loading ring that fills to a real percentage, error artwork, and `IImageService` memory + disk caching. Opening the viewer resolves off the cache the thumbnail already filled rather than downloading the picture a second time.

```xml
<shiny:ImageViewer Uri="{Binding PhotoUrl}"
                   PlaceholderImage="blur_thumb.png"
                   ErrorImage="broken_image.png"
                   Aspect="AspectFill"
                   RingSize="32"
                   HeightRequest="180" />
```

| Property | Type | Description |
|---|---|---|
| Uri | string? | Remote or local image to load. `http`/`https` goes through `IImageService` (MAUI only) |
| Source | ImageSource? | An explicit source. Takes precedence over `Uri` and skips the service |
| IsOpen | bool | Show/hide the viewer (TwoWay) |
| Aspect | Aspect | Thumbnail aspect ratio mode (default: AspectFit) |
| OverlayAspect | Aspect | Aspect ratio mode inside the overlay (default: AspectFit) |
| MaxZoom | double | Maximum zoom scale (default: 5.0) |
| PlaceholderImage | ImageSource? | Artwork shown during the load, behind the ring |
| ErrorImage | ImageSource? | Artwork shown when the load fails |
| LoadingTemplate | DataTemplate? | Replaces the ring; binding context is the live `ImageLoadProgress` |
| ErrorTemplate | DataTemplate? | Replaces the error artwork |
| FadeInDuration | uint | Fade-in milliseconds once loaded (default: 150) |
| RingSize / RingColor / RingTrackColor / ProgressTextColor / ShowProgressText | — | Loading-ring styling |
| CacheEnabled / CacheDuration | bool / TimeSpan? | Per-image cache participation and expiry |
| State / Progress / IsLoading / LoadError | — | Read-only load state, mirrored from the thumbnail |
| ImageLoaded / ImageFailed | event | Raised once per load (thumbnail only, so one image is not announced twice) |
| CloseButtonTemplate | DataTemplate? | Custom close button (tapping closes viewer) |
| HeaderTemplate | DataTemplate? | Custom header overlay |
| FooterTemplate | DataTemplate? | Custom footer overlay |
| OpenViewerOnTap | bool | Whether tapping the thumbnail opens the overlay (default: true) |
| UseFeedback | bool | Enable/disable feedback on double-tap zoom (default: true) |

Method: `ReloadAsync()` re-fetches, skipping both cache tiers.

**Features:**
- Pinch-to-zoom with origin tracking
- Pan when zoomed (clamped to image bounds)
- Double-tap to zoom in (2.5x) / reset
- Animated fade open/close with backdrop
- Close button overlay
- Remote loading with caching, progress ring and placeholder/error artwork (MAUI)

On Blazor, `Source` is a URL string handed straight to `<img>` — the loading pipeline is MAUI-only for now.

### Automation ids (MAUI)

Set `AutomationId` on the control and its parts are named after it, for UI tests (Appium, UI Automation, DevFlow). Without an id the parts get none.

| Part | Id |
|---|---|
| Thumbnail (tap to open) | `<id>_Thumbnail` |
| Full-screen overlay (lives in the page's overlay layer) | `<id>_Popup` |
| Zoomable image | `<id>_Image` |
| Close button (default or `CloseButtonTemplate` root) | `<id>_Close` |
