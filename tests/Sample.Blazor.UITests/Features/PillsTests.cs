namespace Sample.Blazor.UITests.Features;

public class PillsTests : BlazorTest
{
    ILocator Pill(string text) => Page.Locator(".shiny-pill", new() { HasTextString = text }).First;

    static Task<string> Css(ILocator locator, string property)
        => locator.EvaluateAsync<string>("(el, p) => getComputedStyle(el).getPropertyValue(p)", property);

    [Test]
    public async Task Each_Type_Gets_Its_Own_Class_And_Colour()
    {
        await Open("pills");

        string[] types = ["Success", "Info", "Warning", "Caution", "Critical"];
        var backgrounds = new HashSet<string>();
        foreach (var type in types)
        {
            var pill = Pill(type);
            await Expect(pill).ToHaveClassAsync(new System.Text.RegularExpressions.Regex($"shiny-pill-{type.ToLowerInvariant()}"));
            backgrounds.Add(await Css(pill, "background-color"));
        }

        // every semantic type is visually distinct
        await Assert.That(backgrounds.Count).IsEqualTo(types.Length);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Custom_Colours_Override_The_Type()
    {
        await Open("pills");

        var brand = Pill("Brand");
        await Expect(brand).ToHaveCSSAsync("background-color", "rgb(49, 46, 129)");
        await Expect(brand).ToHaveCSSAsync("color", "rgb(224, 231, 255)");
        await Expect(brand).ToHaveCSSAsync("border-top-color", "rgb(67, 56, 202)");

        var mint = Pill("Mint");
        await Expect(mint).ToHaveCSSAsync("background-color", "rgb(167, 243, 208)");
        await Expect(mint).ToHaveCSSAsync("color", "rgb(6, 95, 70)");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task FontSize_And_Bold_Are_Applied()
    {
        await Open("pills");

        await Expect(Pill("Small")).ToHaveCSSAsync("font-size", "10px");
        await Expect(Pill("Default")).ToHaveCSSAsync("font-size", "12px");
        await Expect(Pill("Large")).ToHaveCSSAsync("font-size", "16px");
        await Expect(Pill("Bold")).ToHaveCSSAsync("font-weight", "600");

        // a bigger font makes a bigger pill
        var small = await Pill("Small").BoundingBoxAsync();
        var large = await Pill("Large").BoundingBoxAsync();
        await Assert.That(large!.Height).IsGreaterThan(small!.Height);
        await AssertNoConsoleErrors();
    }
}
