# AutoCompleteEntry

[← All Shiny Controls](../../README.md)

A text input with debounced search, dropdown suggestions, busy indicator, and custom item templates. Supports both local filtering and remote search via a command/callback. Available on both MAUI and Blazor with full styling control.

![AutoCompleteEntry](../../assets/autocomplete1.png)

```xml
<shiny:AutoCompleteEntry
    Text="{Binding SearchText}"
    Placeholder="Search..."
    ItemsSource="{Binding Results}"
    SelectedItem="{Binding SelectedResult}"
    SearchCommand="{Binding SearchCommand}"
    TextMemberPath="Name"
    DebounceInterval="300"
    Threshold="2"
    MaxDropDownHeight="250"
    FontSize="16"
    TextColor="Black"
    DropDownBackgroundColor="White"
    DropDownBorderColor="LightGray"
    CornerRadius="8" />
```

| Property | Type | Default | Description |
|---|---|---|---|
| Text | string | "" | Current text value (TwoWay) |
| Placeholder | string? | null | Placeholder text |
| PlaceholderColor | Color/string | null | Placeholder text color |
| ItemsSource | IList | null | Suggestion items |
| SelectedItem | object? | null | Currently selected item (TwoWay) |
| SearchCommand | ICommand / EventCallback\<string\> | null | Remote search command |
| TextMemberPath | string? | null | Property name to display from items (read by reflection on MAUI — under full trimming leave it unset and override the item's `ToString()`) |
| ItemTemplate | DataTemplate / RenderFragment\<object\> | null | Custom dropdown item template |
| IsBusy | bool | false | Show/hide the loading spinner (TwoWay) |
| DebounceInterval | int | 300 | Debounce delay (ms) |
| Threshold | int | 1 | Minimum characters before searching |
| MaxDropDownHeight | double | 200 | Maximum dropdown height (px) |
| TextColor | Color/string | null | Input text color |
| FontSize | double | 14 | Input font size |
| FontFamily | string? | null | Input font family (MAUI only) |
| FontAttributes | FontAttributes | None | Bold/italic (MAUI only) |
| DropDownBackgroundColor | Color/string | White | Dropdown background |
| DropDownBorderColor | Color/string | LightGray | Dropdown border color |
| CornerRadius | double | 4 | Dropdown border radius (MAUI only) |
| SpinnerColor | Color/string | Grey | Loading spinner color |
| CssClass | string? | null | Root CSS class (Blazor only) |
| InputClass | string? | null | Input element CSS class (Blazor only) |
| DropDownClass | string? | null | Dropdown CSS class (Blazor only) |
| AdditionalAttributes | IDictionary | null | Unmatched HTML attributes (Blazor only) |

**Dropdown placement (MAUI):** the suggestion list floats in the page's overlay layer, directly under the field and at the field's width (or above it when there is more room above). It is not a row of the control, so a form inside a `ScrollView` gets no nested scroller and nothing below the field is pushed down when it opens. It follows the field while the page scrolls. Off a `ContentPage` (no overlay to float in) it falls back to an inline row. The floating card's `AutomationId` is the control's plus `_DropDown`, for UI tests.

Events: `ItemSelected` fires when a suggestion is chosen.

**Blazor CSS Custom Properties** — Override these on a parent element or the component itself to theme without parameters:

| Variable | Default | Controls |
|---|---|---|
| `--shiny-ac-text` | inherit | Input text color |
| `--shiny-ac-ph` | #9CA3AF | Placeholder color |
| `--shiny-ac-dd-bg` | #fff | Dropdown background |
| `--shiny-ac-dd-border` | #D1D5DB | Dropdown border |
| `--shiny-ac-spinner` | #9CA3AF | Spinner color |
| `--shiny-ac-font-size` | inherit | Input font size |
| `--shiny-ac-dd-max-h` | 200px | Dropdown max height |
