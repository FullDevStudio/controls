# PillView

[← All Shiny Controls](../../README.md)

Pill/chip/tag elements for displaying categories, filters, or status indicators with predefined or custom color schemes.

![Pills](../../assets/pills.png)

```xml
<shiny:PillView Text="Success" Type="Success" />
<shiny:PillView Text="Warning" Type="Warning" />
<shiny:PillView Text="Custom" PillColor="Purple" PillTextColor="White" />
```

| Pill Type | Description |
|---|---|
| None | Default/neutral |
| Success | Green |
| Info | Blue |
| Warning | Yellow |
| Caution | Orange |
| Critical | Red |

Each `PillType` maps to a well-known style key (e.g. `ShinyPillSuccessStyle`) that can be overridden in your app's `ResourceDictionary` to customize the preset themes.

## Automation ids (MAUI)

Set `AutomationId` on the pill and its label is named after it, for UI tests:

| Part | Id |
|---|---|
| Text label | `<id>_Text` |
