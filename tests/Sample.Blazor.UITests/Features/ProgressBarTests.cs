using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class ProgressBarTests : BlazorTest
{
    ILocator Bar(string section) => Page.Locator("section.demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = section, Exact = true }) })
        .Locator(".shiny-pb").First;

    // the value is a double, so 45 + 10 can come out as 55.00000000000001%
    static Regex Width(int percent) => new($@"width: ({percent}(\.\d+)?|{percent - 1}\.9\d+)%");

    ILocator ValueLabel => Page.GetByText(new Regex(@"^Value: \d+$"));
    ILocator Increment => Page.GetByRole(AriaRole.Button, new() { Name = "+ 10", Exact = true });
    ILocator Decrement => Page.GetByRole(AriaRole.Button, new() { Name = "- 10", Exact = true });

    [Test]
    public async Task Increment_And_Decrement_Move_Every_Bar()
    {
        await Open("progressbar");

        await Expect(ValueLabel).ToHaveTextAsync("Value: 45");
        await Expect(Bar("Basic").Locator(".shiny-pb-fill")).ToHaveAttributeAsync("style", Width(45));

        await Increment.ClickAsync();
        await Expect(ValueLabel).ToHaveTextAsync("Value: 55");
        await Expect(Bar("Basic").Locator(".shiny-pb-fill")).ToHaveAttributeAsync("style", Width(55));
        await Expect(Bar("With Text").Locator(".shiny-pb-text")).ToHaveTextAsync("55%");
        await Expect(Bar("Gradient + Text").Locator(".shiny-pb-text")).ToHaveTextAsync("55%");

        await Decrement.ClickAsync();
        await Decrement.ClickAsync();
        await Expect(ValueLabel).ToHaveTextAsync("Value: 35");
        await Expect(Bar("With Text").Locator(".shiny-pb-text")).ToHaveTextAsync("35%");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Fill_Width_Tracks_The_Value()
    {
        await Open("progressbar");

        var track = Bar("Basic").Locator(".shiny-pb-track");
        var fill = Bar("Basic").Locator(".shiny-pb-fill");

        await Increment.ClickAsync(); // 55
        await Increment.ClickAsync(); // 65
        await Expect(ValueLabel).ToHaveTextAsync("Value: 65");

        // the fill animates its width; wait for it to land on 65% of the track
        await Page.WaitForFunctionAsync(
            "([t, f]) => Math.abs(f.getBoundingClientRect().width / t.getBoundingClientRect().width - 0.65) < 0.02",
            new[] { await track.ElementHandleAsync(), await fill.ElementHandleAsync() });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Value_Clamps_At_Zero()
    {
        await Open("progressbar");

        for (var i = 0; i < 6; i++)
            await Decrement.ClickAsync();

        await Expect(ValueLabel).ToHaveTextAsync("Value: 0");
        await Expect(Bar("With Text").Locator(".shiny-pb-text")).ToHaveTextAsync("0%");
        await Expect(Bar("Basic").Locator(".shiny-pb-fill")).ToHaveAttributeAsync("style", Width(0));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Value_Clamps_At_One_Hundred()
    {
        await Open("progressbar");

        for (var i = 0; i < 7; i++)
            await Increment.ClickAsync();

        await Expect(ValueLabel).ToHaveTextAsync("Value: 100");
        await Expect(Bar("With Text").Locator(".shiny-pb-text")).ToHaveTextAsync("100%");
        await Expect(Bar("Basic").Locator(".shiny-pb-fill")).ToHaveAttributeAsync("style", Width(100));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Segments_Fill_In_Steps()
    {
        await Open("progressbar");

        var fills = Bar("Segments (steps)").Locator(".shiny-pb-segment .shiny-pb-fill");
        await Expect(fills).ToHaveCountAsync(10);

        // 45% of ten segments: four full, one half, five empty
        await Expect(fills.Nth(3)).ToHaveAttributeAsync("style", Width(100));
        await Expect(fills.Nth(4)).ToHaveAttributeAsync("style", Width(50));
        await Expect(fills.Nth(5)).ToHaveAttributeAsync("style", Width(0));

        await Increment.ClickAsync(); // 55: five full, one half
        await Expect(fills.Nth(4)).ToHaveAttributeAsync("style", Width(100));
        await Expect(fills.Nth(5)).ToHaveAttributeAsync("style", Width(50));
        await Expect(fills.Nth(6)).ToHaveAttributeAsync("style", Width(0));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Indeterminate_Bar_Animates_And_Ignores_The_Buttons()
    {
        await Open("progressbar");

        var bar = Bar("Indeterminate");
        await Expect(bar).ToHaveClassAsync(new Regex("shiny-pb--indeterminate"));

        var animation = await bar.Locator(".shiny-pb-fill").EvaluateAsync<string>("el => getComputedStyle(el).animationName");
        await Assert.That(animation).IsNotEqualTo("none");

        await Increment.ClickAsync();
        await Expect(bar).ToHaveClassAsync(new Regex("shiny-pb--indeterminate"));
        await AssertNoConsoleErrors();
    }
}
