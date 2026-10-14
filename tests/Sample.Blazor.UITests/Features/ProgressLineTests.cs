using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class ProgressLineTests : BlazorTest
{
    // the two lines the page declares; a third appears while the service is running
    ILocator Lines => Page.Locator(".shiny-pl");
    ILocator WindowLine => Page.Locator(".shiny-pl.shiny-pl--top:not(.shiny-pl--container)").First;
    ILocator ContainerLine => Page.Locator(".shiny-pl.shiny-pl--container");
    ILocator Slider => Page.Locator("input[type=range]");
    ILocator Status => Page.Locator("section.demo-section", new() { HasTextString = "Driven from code" }).Locator("p strong");
    ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    [Test]
    public async Task Slider_Drives_Both_Declared_Lines()
    {
        await Open("progressline");

        await Expect(WindowLine).ToHaveAttributeAsync("aria-valuenow", "40");

        await Slider.FillAsync("75");
        await Expect(WindowLine).ToHaveAttributeAsync("aria-valuenow", "75");
        await Expect(ContainerLine).ToHaveAttributeAsync("aria-valuenow", "75");
        await Expect(WindowLine).ToHaveAttributeAsync("style", new Regex(@"--pl-progress: 75%"));
        await Expect(Page.GetByText("75%", new() { Exact = true })).ToBeVisibleAsync();

        await Slider.FillAsync("10");
        await Expect(WindowLine).ToHaveAttributeAsync("aria-valuenow", "10");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Window_Line_Is_Pinned_To_The_Viewport_Top()
    {
        await Open("progressline");

        var box = (await WindowLine.BoundingBoxAsync())!;
        await Assert.That(box.Y).IsLessThan(1);
        await Assert.That(box.Height).IsEqualTo(4);

        // it does not scroll away with the content
        await Page.Mouse.WheelAsync(0, 400);
        await Page.WaitForTimeoutAsync(300);
        box = (await WindowLine.BoundingBoxAsync())!;
        await Assert.That(box.Y).IsLessThan(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Container_Line_Runs_Along_Its_Containers_Bottom_Edge()
    {
        await Open("progressline");

        var line = (await ContainerLine.BoundingBoxAsync())!;
        var panel = (await ContainerLine.Locator("xpath=..").BoundingBoxAsync())!;

        await Assert.That(line.Height).IsEqualTo(6);
        await Assert.That(Math.Abs(line.Y + line.Height - (panel.Y + panel.Height))).IsLessThan(2);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Reported_Progress_Shows_A_Service_Line_Until_It_Completes()
    {
        await Open("progressline");
        await Expect(Lines).ToHaveCountAsync(2);

        await Button("Reported percentages").ClickAsync();
        await Expect(Lines).ToHaveCountAsync(3);
        await Expect(Status).ToHaveTextAsync(new Regex(@"^Uploading \d+%$"));

        await Expect(Status).ToHaveTextAsync("Uploaded", new() { Timeout = 10_000 });
        await Expect(Lines).ToHaveCountAsync(2, new() { Timeout = 5_000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Bottom_Run_Puts_A_Line_On_The_Viewport_Bottom()
    {
        await Open("progressline");

        await Button("Bottom edge, gradient + shimmer").ClickAsync();
        var line = Page.Locator(".shiny-pl.shiny-pl--bottom:not(.shiny-pl--container)");
        await Expect(line).ToHaveCountAsync(1);

        var viewport = Page.ViewportSize!;
        var box = (await line.BoundingBoxAsync())!;
        await Assert.That(Math.Abs(box.Y + box.Height - viewport.Height)).IsLessThan(2);

        await Expect(Status).ToHaveTextAsync("Done", new() { Timeout = 10_000 });
        await Expect(line).ToHaveCountAsync(0, new() { Timeout = 5_000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Service_Line_Uses_The_Configured_Colour()
    {
        await Open("progressline");

        // RunReported configures BarColor = "#22C55E"
        await Button("Reported percentages").ClickAsync();
        var fill = Lines.Nth(2).Locator(".shiny-pl-fill");
        await Expect(fill).ToHaveCSSAsync("background-color", "rgb(34, 197, 94)");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Trickle_Run_Puts_A_Line_On_The_Viewport_Top()
    {
        await Open("progressline");
        await Page.Mouse.WheelAsync(0, 400);

        await Button("Trickle (no reported progress)").ClickAsync();
        await Expect(Lines).ToHaveCountAsync(3);
        await Expect(Status).ToHaveTextAsync("Trickling…");

        var box = (await Lines.Nth(2).BoundingBoxAsync())!;
        await Assert.That(box.Y).IsLessThan(1);

        await Expect(Status).ToHaveTextAsync("Done", new() { Timeout = 10_000 });
        await Expect(Lines).ToHaveCountAsync(2, new() { Timeout = 5_000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Indeterminate_Run_Sweeps()
    {
        await Open("progressline");

        await Button("Indeterminate sweep").ClickAsync();
        var line = Page.Locator(".shiny-pl.shiny-pl--indeterminate");
        await Expect(line).ToHaveCountAsync(1);

        var animation = await line.Locator(".shiny-pl-fill").EvaluateAsync<string>("el => getComputedStyle(el).animationName");
        await Assert.That(animation).IsNotEqualTo("none");

        await Expect(Status).ToHaveTextAsync("Done", new() { Timeout = 10_000 });
        await Expect(line).ToHaveCountAsync(0, new() { Timeout = 5_000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Overlapping_Runs_Keep_The_Line_Up_Until_The_Slower_One_Ends()
    {
        await Open("progressline");

        await Button("Two overlapping operations").ClickAsync();
        await Expect(Status).ToHaveTextAsync("Two operations…");
        await Expect(Lines).ToHaveCountAsync(3);

        // the quick run (0.8s) has finished; the slow one (4s) still holds the line up
        await Page.WaitForTimeoutAsync(2000);
        await Expect(Status).ToHaveTextAsync("Two operations…");
        await Expect(Lines).ToHaveCountAsync(3);

        await Expect(Status).ToHaveTextAsync("Both finished", new() { Timeout = 10_000 });
        await Expect(Lines).ToHaveCountAsync(2, new() { Timeout = 5_000 });
        await AssertNoConsoleErrors();
    }
}
