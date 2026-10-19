# Overlay & LoadingOverlay

[← All Shiny Controls](../../README.md)

Full-screen overlay controls. On MAUI, integrates with `OverlayHost`/`ShinyContentPage` (same backdrop system as FloatingPanel). On Blazor, wraps content with a CSS-based overlay. Supports optional frosted glass blur effect.

**MAUI (placed in ShinyContentPage.Panels):**

```xml
<shiny:ShinyContentPage ...>
    <ScrollView>...</ScrollView>

    <shiny:ShinyContentPage.Panels>
        <shiny:Overlay IsShown="{Binding IsOverlayVisible}" BlurRadius="10">
            <shiny:Overlay.OverlayContentTemplate>
                <DataTemplate>
                    <Label Text="Custom content" TextColor="White" />
                </DataTemplate>
            </shiny:Overlay.OverlayContentTemplate>
        </shiny:Overlay>

        <shiny:LoadingOverlay IsShown="{Binding IsBusy}"
                              Message="Loading..." />
    </shiny:ShinyContentPage.Panels>
</shiny:ShinyContentPage>
```

| Property | Type | Default | Description |
|---|---|---|---|
| IsShown | bool | false | Show/hide overlay (TwoWay) |
| AnimationDuration | uint | 250 | Fade animation duration in ms (MAUI) |
| BlurRadius | double | 0 | When > 0, applies a frosted glass blur behind the backdrop (MAUI uses FrostedGlassView; Blazor uses CSS backdrop-filter) |
| OverlayContentTemplate | DataTemplate | null | Custom overlay content (MAUI) |
| OverlayContent | RenderFragment | null | Custom overlay content (Blazor) |

MAUI backdrop color/opacity are controlled by `ShinyContentPage.BackdropColor` / `BackdropMaxOpacity`.

**LoadingOverlay additional properties:**

| Property | Type | Default | Description |
|---|---|---|---|
| IsIndeterminate | bool | true | Spinner mode (true) or progress bar mode (false) |
| Progress | double | 0 | Progress value 0–100 (when determinate) |
| Message | string? | null | Text displayed below spinner/progress bar |
| SpinnerColor | Color/string | White | Spinner color |

**Blazor (wrapper pattern):**

```razor
<LoadingOverlay IsShown="@isBusy" BlurRadius="8" IsIndeterminate="false" Progress="@progress" Message="Loading...">
    <p>Your page content here — gets overlaid when IsShown=true</p>
</LoadingOverlay>
```

## Automation ids (MAUI)

Set `AutomationId` on a `LoadingOverlay` and its parts are named after it, for UI tests (the backdrop belongs to the `OverlayHost`: `<host id>_Backdrop`):

| Part | Id |
|---|---|
| Message label | `<id>_Message` |
| Progress bar (determinate mode) | `<id>_Progress` |

On a `ShinyContentPage` with an `AutomationId`, the built-in loading overlay is `<page id>_Loading` (so its message is `<page id>_Loading_Message`) and the page's `OverlayHost` is `<page id>_OverlayHost`, whose backdrop is `<page id>_OverlayHost_Backdrop`.
