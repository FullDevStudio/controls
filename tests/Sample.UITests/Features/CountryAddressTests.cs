namespace Sample.UITests.Features;

/// <summary>
/// CountryPicker: searching the built-in country list and picking one fills SelectedCountry, which
/// the page renders as name / ISO codes / dial code. AddressEntry is not driven here — it searches
/// OpenStreetMap Nominatim over the network, which a device UI run cannot stub (the Blazor suite
/// covers it with a routed response).
/// </summary>
public class CountryAddressTests : UITest
{
    const string Route = "countryaddress";
    const string PageType = "CountryAddressPage";

    // The suggestion rows are Grids (flag + name) inside the dropdown's ScrollView; the name is the
    // label with letters in it, the flag is regional-indicator emoji only.
    async Task<string[]> CountryNames()
        => (await Driver.CssNow("#CountryPicker ScrollView Label"))
            .Select(l => l.Text ?? "")
            .Where(t => t.Any(Char.IsAsciiLetter))
            .ToArray();

    [Test]
    public async Task Typing_Filters_The_Country_List()
    {
        await Open(Route, PageType);

        await Driver.FillCss("#CountryPicker BorderlessEntry", "land");

        await Driver.WaitUntil(async () =>
        {
            var names = await CountryNames();
            return names.Length > 3
                && names.Contains("Finland")
                && names.Contains("Switzerland")
                && names.All(n => n.Contains("land", StringComparison.OrdinalIgnoreCase));
        }, "only countries containing 'land' to be suggested", timeoutSeconds: 15);
    }

    [Test]
    public async Task Picking_A_Country_Fills_The_Details()
    {
        await Open(Route, PageType);

        await Driver.FillCss("#CountryPicker BorderlessEntry", "Japan");
        await Driver.WaitUntil(async () => (await CountryNames()).SequenceEqual(["Japan"]), "Japan to be the only suggestion", timeoutSeconds: 15);

        await Driver.TapCss("#CountryPicker ScrollView Grid");

        await Driver.WaitForValue("CountryName", "Text", "Japan");
        await Driver.WaitForValue("CountryCodes", "Text", "JP / JPN");
        await Driver.WaitForValue("CountryDialCode", "Text", "+81");
        await Driver.WaitForValue("CountryDetails", "IsVisible", "True");
        await Driver.WaitForCssValue("#CountryPicker BorderlessEntry", "Text", "Japan");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Picked_Country_Becomes_The_Address_Filter()
    {
        await Open(Route, PageType);

        await Driver.FillCss("#CountryPicker BorderlessEntry", "Canada");
        await Driver.WaitUntil(async () => (await CountryNames()).SequenceEqual(["Canada"]), "Canada to be the only suggestion", timeoutSeconds: 15);
        await Driver.TapCss("#CountryPicker ScrollView Grid");

        await Driver.WaitForValue("CountryCodes", "Text", "CA / CAN");
        await Driver.WaitForValue("AddressEntry", "CountryCodes", "ca");
    }
}
