using System.Collections.Concurrent;

namespace Sample.Blazor.UITests.Features;

/// <summary>
/// CountryPicker and AddressEntry on the web. The address search goes to OpenStreetMap Nominatim;
/// those requests are answered by a Playwright route here, so the tests are deterministic, never
/// touch the network, and can assert the query the control sent (the picked country becomes the
/// countrycodes filter).
/// </summary>
public class CountryAddressTests : BlazorTest
{
    const string NominatimJson =
        """
        [
          {
            "display_name": "1600 Pennsylvania Avenue NW, Washington, District of Columbia, 20500, United States",
            "lat": "38.8976633", "lon": "-77.0365739",
            "address": { "house_number": "1600", "road": "Pennsylvania Avenue NW", "city": "Washington",
                         "state": "District of Columbia", "postcode": "20500", "country": "United States", "country_code": "us" }
          },
          {
            "display_name": "Pennsylvania Station, New York, New York, 10001, United States",
            "lat": "40.7506", "lon": "-73.9935",
            "address": { "road": "Pennsylvania Plaza", "city": "New York", "state": "New York",
                         "postcode": "10001", "country": "United States", "country_code": "us" }
          }
        ]
        """;

    ILocator CountrySection
        => Page.Locator(".demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Country", Exact = true }) });

    ILocator AddressSection
        => Page.Locator(".demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Address", Exact = true }) });

    async Task<ConcurrentQueue<string>> StubNominatim()
    {
        var requests = new ConcurrentQueue<string>();
        await Page.RouteAsync("**/nominatim.openstreetmap.org/**", async route =>
        {
            requests.Enqueue(route.Request.Url);
            await route.FulfillAsync(new()
            {
                Status = 200,
                ContentType = "application/json",
                Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
                Body = NominatimJson
            });
        });
        return requests;
    }

    async Task PickCountry(string search, string name)
    {
        var input = CountrySection.GetByPlaceholder("Search countries...");
        await input.PressSequentiallyAsync(search);
        await CountrySection.Locator(".shiny-ac-item", new() { Has = Page.Locator(".shiny-country-item-name", new() { HasTextString = name }) }).First.ClickAsync();
    }

    [Test]
    public async Task Focus_Lists_Every_Country_And_Typing_Filters()
    {
        await Open("countryaddress");
        var input = CountrySection.GetByPlaceholder("Search countries...");
        var names = CountrySection.Locator(".shiny-country-item-name");

        await input.FocusAsync();
        await Expect(names.First).ToBeVisibleAsync();
        await Assert.That(await names.CountAsync()).IsGreaterThan(150);

        await input.PressSequentiallyAsync("land");
        await Expect(names.Filter(new() { HasNotTextRegex = new System.Text.RegularExpressions.Regex("land", System.Text.RegularExpressions.RegexOptions.IgnoreCase) })).ToHaveCountAsync(0);
        await Expect(names.Filter(new() { HasTextString = "Switzerland" })).ToHaveCountAsync(1);
        await Assert.That(await names.CountAsync()).IsGreaterThan(3);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Picking_A_Country_Shows_Its_Details()
    {
        await Open("countryaddress");

        await PickCountry("Japan", "Japan");

        var state = CountrySection.Locator(".state");
        await Expect(state).ToContainTextAsync("Name: Japan");
        await Expect(state).ToContainTextAsync("Code: JP / JPN");
        await Expect(state).ToContainTextAsync("Dial: +81");
        await Expect(CountrySection.Locator(".shiny-country-flag")).ToHaveTextAsync("🇯🇵");
        await Expect(CountrySection.GetByPlaceholder("Search countries...")).ToHaveValueAsync("Japan");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Address_Search_Shows_Results_And_Picks_One()
    {
        var requests = await StubNominatim();
        await Open("countryaddress");
        var input = AddressSection.GetByPlaceholder("Start typing an address...");

        await input.PressSequentiallyAsync("Pennsylvania");

        var items = AddressSection.Locator(".shiny-ac-item");
        await Expect(items).ToHaveTextAsync([
            "1600, Pennsylvania Avenue NW, Washington, District of Columbia",
            "Pennsylvania Plaza, New York, New York"
        ]);
        await Assert.That(requests.Last()).Contains("q=Pennsylvania");
        await Assert.That(requests.Last()).DoesNotContain("countrycodes");

        await items.First.ClickAsync();

        var state = AddressSection.Locator(".state");
        await Expect(state).ToContainTextAsync("Street: 1600 Pennsylvania Avenue NW");
        await Expect(state).ToContainTextAsync("City: Washington");
        await Expect(state).ToContainTextAsync("Postal: 20500");
        await Expect(state).ToContainTextAsync("Coords: 38.89766, -77.03657");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Address_Search_Waits_For_Three_Characters()
    {
        var requests = await StubNominatim();
        await Open("countryaddress");

        await AddressSection.GetByPlaceholder("Start typing an address...").PressSequentiallyAsync("Pe");
        await Page.WaitForTimeoutAsync(1200);

        await Assert.That(requests.Count).IsEqualTo(0);
        await Expect(AddressSection.Locator(".shiny-ac-item")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Picked_Country_Filters_The_Address_Search()
    {
        var requests = await StubNominatim();
        await Open("countryaddress");

        await PickCountry("United States", "United States");
        await Expect(CountrySection.Locator(".state")).ToContainTextAsync("Code: US / USA");

        await AddressSection.GetByPlaceholder("Start typing an address...").PressSequentiallyAsync("Pennsylvania");
        await Expect(AddressSection.Locator(".shiny-ac-item").First).ToBeVisibleAsync();

        await Assert.That(requests.Last()).Contains("countrycodes=us");
        await AssertNoConsoleErrors();
    }
}
