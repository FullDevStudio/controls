# Fab & FabMenu

[← All Shiny Controls](../../README.md)

A Material Design-style floating action button, plus an expanding multi-action menu that animates up from the main FAB.

Menu items render as **pills**: the label lives *inside* one capsule with a tinted circular icon chip on
the edge nearest the main FAB, so the whole row is a single tap target instead of a detached label chip
plus a circle. Every chip is inset so its centre lands on the main FAB's vertical axis — the items read as
one column. An item with no `Text` collapses to a plain circle of `Size`. Items fade, rise and scale in
from that axis with a staggered spring, and the main FAB spins 45° (`IconRotation`) while the menu is open
— the classic "+" turning into an "×" — unless it carries a `Text` label, where a rotated word would just
read as broken.

| Closed | Menu Open |
|:---:|:---:|
| ![FAB Closed](../../assets/fab-closed.png) | ![FAB Menu Open](../../assets/fab-open.png) |

```xml
<!-- Single Fab -->
<shiny:Fab Icon="add.png"
           Text="Add Item"
           FabBackgroundColor="#4CAF50"
           TextColor="White"
           Command="{Binding AddCommand}"
           HorizontalOptions="End"
           VerticalOptions="End"
           Margin="24" />

<!-- FabMenu with child items -->
<shiny:FabMenu IsOpen="{Binding IsMenuOpen}"
               Icon="plus.png"
               FabBackgroundColor="#2196F3"
               HorizontalOptions="End"
               VerticalOptions="End"
               Margin="24">
    <shiny:FabMenuItem Icon="share.png"  Text="Share"  Command="{Binding ShareCommand}" />
    <shiny:FabMenuItem Icon="edit.png"   Text="Edit"   Command="{Binding EditCommand}" />
    <shiny:FabMenuItem Icon="delete.png" Text="Delete" Command="{Binding DeleteCommand}" />
</shiny:FabMenu>
```

**Fab** properties:

| Property | Type | Default | Description |
|---|---|---|---|
| Icon | ImageSource? | null | Button icon |
| Text | string? | null | Optional label; when null the Fab is a perfect circle. A short label (e.g. `+`) still renders circular; the Fab stretches into a pill only when the label needs more than Size |
| Command | ICommand? | null | Invoked when the Fab is tapped |
| CommandParameter | object? | null | Parameter passed to the Command |
| FabBackgroundColor | Color | #2196F3 | Fill color |
| BorderColor | Color? | null | Outline stroke color |
| BorderThickness | double | 0 | Outline stroke thickness |
| TextColor | Color | White | Label color |
| FontSize | double | 14 | Label font size |
| FontAttributes | FontAttributes | None | Label font attributes |
| Size | double | 56 | Height of the Fab (diameter when circular) |
| IconSize | double | 24 | Icon image size |
| HasShadow | bool | true | Show drop shadow |
| UseFeedback | bool | true | Feedback on tap |

Events: `Clicked`.

**FabMenu** properties (plus all main-Fab pass-throughs above):

| Property | Type | Default | Description |
|---|---|---|---|
| IsOpen | bool | false | Two-way bindable; opens/closes the menu with animation. A change made mid-animation is applied when the running animation finishes |
| Items | `IList<FabMenuItem>` | empty | Menu items (content property — place items directly inside the FabMenu) |
| FabSize | double | 56 | Main FAB button size (diameter) |
| HasShadow | bool | true | Drop shadow on the main FAB |
| MenuAlignment | LayoutOptions | End | Horizontal alignment of the menu stack (Start for left-aligned, End for right-aligned) |
| HasBackdrop | bool | true | Show a dim backdrop while open |
| BackdropColor | Color | Black | Backdrop color |
| BackdropOpacity | double | 0.4 | Backdrop peak opacity |
| CloseOnBackdropTap | bool | true | Close when backdrop is tapped |
| CloseOnItemTap | bool | true | Close after any item is tapped |
| AnimationDuration | uint | 200 | Open/close animation duration (ms) |
| IconRotation | double | 45 | Degrees the main FAB rotates while open (0 disables; ignored when the main FAB has `Text`) |
| UseFeedback | bool | true | Feedback on toggle |

Events: `ItemTapped` — fires the `FabMenuItem` that was tapped.

Methods: `Open()`, `Close()`, `Toggle()`.

**FabMenuItem** properties:

| Property | Type | Default | Description |
|---|---|---|---|
| Icon | ImageSource? | null | Icon rendered in the circular chip |
| Text | string? | null | Label inside the pill; when null the item collapses to a plain circle |
| Command | ICommand? | null | Invoked when tapped |
| CommandParameter | object? | null | Parameter for the Command |
| FabBackgroundColor | Color | theme Primary | Icon chip fill — and the whole pill's fill when the item has no `Text` |
| BorderColor | Color? | theme OutlineVariant | Pill outline stroke |
| BorderThickness | double | 1 | Pill outline thickness (0 for a borderless pill) |
| TextColor | Color | theme OnSurface | Label text color |
| LabelBackgroundColor | Color | theme SurfaceContainerHigh | Pill body fill behind the label |
| FontSize | double | 13 | Label font size |
| FontAttributes | FontAttributes | None | Label font attributes |
| Size | double | 44 | Pill height (diameter when the item has no `Text`) |
| IconSize | double | 20 | Icon image size |
| HasShadow | bool | true | Drop shadow on the pill |
| UseFeedback | bool | true | Feedback on tap |

**Placement tip**: `FabMenu` should live in a `Grid` that fills the page (the same placement pattern as `ImageViewer`) so the backdrop can cover the page content. Alternatively, use `ShinyContentPage` with `OverlayHost` for easier overlay management.

**Blazor** matches the MAUI look and API. `Items` is a `List<FabMenuItem>` of plain data objects
(`Icon` is an inline emoji / SVG string or an image URL), and the same knobs are parameters: `FabSize`,
`HasShadow`, `IconRotation`, and `MenuAlignment` (`"end"` default, `"start"` to grow from the left).
Colors default to the theme CSS variables (`--shiny-color-primary`, `--shiny-color-surface-container-high`,
`--shiny-color-on-surface`, `--shiny-color-outline-variant`), the open backdrop adds a 2px blur, and
`prefers-reduced-motion` collapses every transition.

```razor
<FabMenu Items="items" Icon="+" ItemTapped="OnItemTapped" />

@code {
    readonly List<FabMenuItem> items = new()
    {
        new FabMenuItem { Text = "New Note",  Icon = "📝", FabBackgroundColor = "#10B981" },
        new FabMenuItem { Text = "New Photo", Icon = "📷", FabBackgroundColor = "#F59E0B" },
    };
}
```

### Automation ids (MAUI)

Set `AutomationId` on a `FabMenu` and its parts are named after it, for UI tests. An item with its own `AutomationId` keeps it. A plain `Fab` is a single tap target, so its own id is the one to tap.

| Part | Id |
|---|---|
| Main button (opens / closes) | `<id>_Fab` |
| Backdrop (closes when tapped) | `<id>_Backdrop` |
| Each unnamed item, by index in `Items` | `<id>_Item_<index>` |
