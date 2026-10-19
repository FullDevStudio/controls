# ColorPicker

[← All Shiny Controls](../../README.md)

A full-featured color picker with spectrum, hue bar, opacity slider, hex input, and preview swatch. Available as both an inline `ColorPicker` control and a `ColorPickerButton` that opens as a popup dialog.

| Button | Picker Dialog |
|:---:|:---:|
| ![Color Picker Button](../../assets/colorpicker1.png) | ![Color Picker Dialog](../../assets/colorpicker2.png) |

```xml
<shiny:ColorPickerButton SelectedColor="{Binding SelectedColor}"
                         Text="Pick Color"
                         ShowOpacity="True" />
```

| Property | Type | Default | Description |
|---|---|---|---|
| SelectedColor | Color | Red | Currently selected color — TwoWay |
| Text | string? | null | Button label text |
| ShowOpacity | bool | false | Show/hide opacity slider |
| CornerRadius | int | 8 | Button corner radius |
| ColorChangedCommand | ICommand? | null | Fires when color changes |

**Event:** `ColorChanged` (EventHandler\<Color\>)

Blazor has the same pair. `ColorPicker` is the panel; `ColorPickerButton` is the swatch that opens it,
which is what goes in a toolbar. The swatch sits on a checkerboard so a translucent colour reads as
translucent rather than as a slightly different flat one.

```razor
<ColorPickerButton Text="Text colour"
                   @bind-SelectedColor="hex"
                   ShowOpacity="false" />
```

`SelectedColor` is a hex string on Blazor rather than a `Color`: `#RRGGBB`, or `#AARRGGBB` when
`ShowOpacity` is on — alpha first, so it drops straight into `ArgbColor.FromUInt32`.

## Panel width

The picker is a fixed **320px** panel on both hosts, and both hosts now say so explicitly.

Nothing inside it has a width of its own — the spectrum, the hue bar and the opacity track are all
told to fill, and each one is a canvas (Blazor) or a `GraphicsView` (MAUI) with no content to measure.
That is right in a container that stretches, and wrong in one that shrink-wraps its content, which is
exactly what `ColorPickerButton` puts it in: an absolutely positioned popover on Blazor, a
centre-aligned `Border` on MAUI. In that position the only child that measured was the bottom row, so
the whole picker came out about the width of the hex box with the spectrum squeezed to a sliver —
visible anywhere the button is used, the Document Editor, Spreadsheet and Slide Editor toolbars
included.

Blazor pins the popover at `width: 320px`, capped at `calc(100vw - 16px)` so a narrow viewport still
fits. The cap is deliberately viewport-relative: a percentage would resolve against the 30px trigger.
MAUI gives the picker a `MinimumWidthRequest` of 320 rather than a `WidthRequest`, so it still fills a
host that is wider.

## Sizing the button

On MAUI the trigger shrink-wraps its label, down to a minimum width, so a button dropped into a stack
does not stretch across the row. Set a `WidthRequest` and the trigger fills it instead — which is what
a toolbar sizing several controls to a common width wants. Without that it would draw at its minimum
inside the width you asked for, and the remainder would show as a gap beside the button.

```xml
<shiny:ColorPickerButton WidthRequest="150" />
```

## Automation ids (MAUI)

Set `AutomationId` on the control and its parts are named after it, for UI tests (Appium, UI Automation, DevFlow). With no id on the control, the parts get none.

| Part | Id |
|---|---|
| ColorPicker: saturation/brightness spectrum | `<id>_Spectrum` |
| ColorPicker: hue bar | `<id>_Hue` |
| ColorPicker: opacity slider | `<id>_Opacity` |
| ColorPicker: preview swatch | `<id>_Preview` |
| ColorPicker: hex entry | `<id>_Hex` |
| ColorPickerButton: the trigger | `<id>_Button` |
| ColorPickerButton: popup card | `<id>_Popup` |
| ColorPickerButton: Done | `<id>_Done` |
| ColorPickerButton: the picker in the popup (its parts follow it, e.g. `<id>_Picker_Hex`) | `<id>_Picker` |

The popup is moved into the page when it opens, so these ids are how a test finds it. The constants are `ColorPicker.HexAutomationIdSuffix`, `ColorPickerButton.PopupAutomationIdSuffix` and so on.
