# CountryPicker

[← All Shiny Controls](../../README.md)

A country search control built on AutoCompleteEntry with flag emoji display, country name, and dial code. Searches all ISO 3166-1 countries.

| Empty | With Selection |
|:---:|:---:|
| ![Country & Address](../../assets/countryaddress1.png) | ![Country Selected](../../assets/countryaddress2.png) |

```xml
<shiny:CountryPicker SelectedCountry="{Binding Country}"
                     Placeholder="Select country..."
                     FontSize="16"
                     TextColor="Black" />
```

| Property | Type | Default | Description |
|---|---|---|---|
| SelectedCountry | Country | null | Selected country (TwoWay) |
| Placeholder | string | "Search countries..." | Placeholder text |
| MaxDropDownHeight | double | 200 | Max dropdown height |
| TextColor | Color/string | null | Text color |
| PlaceholderColor | Color/string | null | Placeholder color |
| DropDownBackgroundColor | Color/string | null | Dropdown background |
| DropDownBorderColor | Color/string | null | Dropdown border color |
| FontSize | double | 14 | Font size |
| FontFamily | string? | null | Font family (MAUI only) |
| CornerRadius | double | 4 | Dropdown corner radius (MAUI only) |
| InputClass | string? | null | Input CSS class (Blazor only) |
| DropDownClass | string? | null | Dropdown CSS class (Blazor only) |

Events: `CountrySelected` fires when a country is chosen.

The `Country` model provides: `Name`, `Iso2`, `Iso3`, `DialCode`, `FlagEmoji`.

### Automation ids (MAUI)

Set `AutomationId` on the control and its parts are named after it, for UI tests (Appium, UI Automation, DevFlow). Without an id, the parts get none.

| Part | Id |
|---|---|
| The search field | `<id>_Entry` |
| The country dropdown | `<id>_DropDown` |
| Each country, by ISO alpha-2 code | `<id>_Suggestion_CA` |
| The selected country's flag | `<id>_Flag` |

Countries are keyed by code rather than position, so `_Suggestion_CA` is Canada however the list is filtered.
