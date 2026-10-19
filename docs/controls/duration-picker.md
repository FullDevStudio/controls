# DurationPicker

[← All Shiny Controls](../../README.md)

A standalone duration picker control that opens a FloatingPanel for selection with hour/minute pickers and "hr"/"min" labels. Requires `ShinyContentPage` (or an `OverlayHost` in the visual tree).

```xml
<shiny:DurationPicker Duration="{Binding SelectedDuration, Mode=TwoWay}"
                      MinDuration="0:15:00"
                      MaxDuration="8:00:00"
                      MinuteInterval="5"
                      Placeholder="Choose duration" />
```

| Property | Type | Default | Description |
|---|---|---|---|
| `Duration` | `TimeSpan?` | `null` | Selected duration (TwoWay) |
| `MinDuration` | `TimeSpan` | `0:00:00` | Minimum duration |
| `MaxDuration` | `TimeSpan` | `24:00:00` | Maximum duration |
| `MinuteInterval` | `int` | `5` | Minute increment step |
| `Format` | `string` | `@"h\:mm"` | Display format string |
| `Placeholder` | `string` | `"Select duration"` | Text shown when no duration selected |

### Automation ids (MAUI)

Set `AutomationId` on the control and its parts are named after it, for UI tests (Appium, UI Automation, DevFlow). Without an id, the parts get none.

| Part | Id |
|---|---|
| The field that opens the popup | `<id>_Trigger` |
| The displayed duration / placeholder | `<id>_Value` |
| Popup content | `<id>_Popup` |
| Hours picker | `<id>_Hours` |
| Minutes picker | `<id>_Minutes` |
| Done button | `<id>_Done` |
| Cancel button | `<id>_Cancel` |
