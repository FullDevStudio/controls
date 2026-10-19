# SecurityPin

[← All Shiny Controls](../../README.md)

A PIN entry control with individually rendered cells that captures input through a hidden Entry. Digits remain visible by default and can optionally be masked with any character.

![SecurityPin](../../assets/securitypin.png)

```xml
<shiny:SecurityPin Length="4"
                   HideCharacter="*"
                   Value="{Binding Pin}"
                   Keyboard="Numeric"
                   Completed="OnPinCompleted" />
```

| Property | Type | Default | Description |
|---|---|---|---|
| Length | int | 4 | Number of PIN cells |
| Value | string | "" | Current PIN value (TwoWay) |
| Keyboard | Keyboard | Numeric | Keyboard type for input |
| HideCharacter | string? | null | When set, masks entered characters; when null/empty, shows actual values |
| CellSize | double | 50 | Width/height of each cell |
| CellSpacing | double | 8 | Space between cells |
| CellCornerRadius | double | 8 | Border corner radius |
| CellBorderColor | Color? | null | Cell border color; unset follows the theme's `outline` |
| CellFocusedBorderColor | Color? | null | Border color for the active cell; unset uses `primary` (or `CellBorderColor` when only that is set) |
| CellBackgroundColor | Color? | null | Cell fill color; unset follows `surface-container-low` |
| CellTextColor | Color? | null | Entered character color; unset follows `on-surface` |
| FontSize | double | 24 | Character font size |

| UseFeedback | bool | Enable/disable feedback on digit entry (click) and completion (long press) (default: true) |

Events: `Completed` fires with a `SecurityPinCompletedEventArgs` once the entered value reaches `Length`.

Methods: `Focus()`, `Unfocus()`, `Clear()`.

## Dark mode

`CellBackgroundColor` defaults to `surface-container-low` rather than a literal white, so the cells
follow the app's scheme. Pass your own to pin it — and pass `CellTextColor` alongside it, or the
theme's ink may not read against your fill. See [Styling & theming](styling.md#dark-mode).

### Automation ids (MAUI)

Set `AutomationId` on the control and its parts are named after it, for UI tests (Appium, UI Automation, DevFlow). Without an id, the parts get none.

| Part | Id |
|---|---|
| The hidden entry that takes keystrokes | `<id>_Entry` |
| The row of cells (tap to raise the keyboard) | `<id>_Cells` |
| Each cell, by position | `<id>_Cell_0` |
