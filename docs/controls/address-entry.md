# AddressEntry

[← All Shiny Controls](../../README.md)

An address search control built on AutoCompleteEntry that queries a geocoding provider (Nominatim/OpenStreetMap by default). Returns structured address data with coordinates.

```xml
<shiny:AddressEntry SelectedAddress="{Binding Address}"
                    Placeholder="Search address..."
                    CountryCodes="us,ca"
                    FontSize="16" />
```

| Property | Type | Default | Description |
|---|---|---|---|
| SelectedAddress | Address | null | Selected address (TwoWay) |
| SearchProvider | IAddressSearchProvider? | null | Custom search provider (defaults to Nominatim) |
| CountryCodes | string? | null | Comma-separated ISO country codes to filter results |
| Placeholder | string | "Search address..." | Placeholder text |
| MaxDropDownHeight | double | 250 | Max dropdown height |
| TextColor | Color/string | null | Text color |
| PlaceholderColor | Color/string | null | Placeholder color |
| DropDownBackgroundColor | Color/string | null | Dropdown background |
| DropDownBorderColor | Color/string | null | Dropdown border color |
| FontSize | double | 14 | Font size |
| FontFamily | string? | null | Font family (MAUI only) |
| CornerRadius | double | 4 | Dropdown corner radius (MAUI only) |
| InputClass | string? | null | Input CSS class (Blazor only) |
| DropDownClass | string? | null | Dropdown CSS class (Blazor only) |

Events: `AddressSelected` fires when an address is chosen.

The `Address` record provides: `DisplayName`, `HouseNumber`, `Street`, `City`, `State`, `PostalCode`, `Country`, `CountryCode`, `Latitude`, `Longitude`.

Implement `IAddressSearchProvider` for custom geocoding:

```csharp
public class MyGeoProvider : IAddressSearchProvider
{
    public Task<IList<Address>> SearchAsync(string query, string? countryCodes, CancellationToken ct)
    {
        // call your preferred geocoding API
    }
}
```

### Automation ids (MAUI)

Set `AutomationId` on the control and its parts are named after it, for UI tests (Appium, UI Automation, DevFlow). Without an id, the parts get none.

| Part | Id |
|---|---|
| The text field | `<id>_Entry` |
| The results dropdown | `<id>_DropDown` |
| Each matching address, by position | `<id>_Suggestion_0` |
