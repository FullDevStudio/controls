namespace Sample.Blazor.UITests.Features;

public class ConfettiTests : BlazorTest
{
    // one full-window canvas, added on the first burst and removed once every particle has landed
    ILocator Canvas => Page.Locator("canvas.shiny-confetti-canvas");
    ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
    ILocator Status => Page.Locator("section.demo-section", new() { HasTextString = "From code" }).Locator("p strong");

    [Test]
    public async Task Wrapped_Click_Bursts_Then_Cleans_Up()
    {
        await Open("confetti");
        await Expect(Canvas).ToHaveCountAsync(0);

        await Button("Burst").ClickAsync();
        await Expect(Canvas).ToHaveCountAsync(1);

        // the canvas covers the window but never eats clicks
        await Expect(Canvas).ToHaveCSSAsync("pointer-events", "none");
        await Expect(Canvas).ToHaveCSSAsync("position", "fixed");

        await Expect(Canvas).ToHaveCountAsync(0, new() { Timeout = 15_000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Wrapped_ShinyButton_Still_Bursts()
    {
        await Open("confetti");

        await Button("Stars (ShinyButton)").ClickAsync();
        await Expect(Canvas).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task DoubleTap_Trigger_Ignores_A_Single_Click()
    {
        await Open("confetti");

        var area = Page.GetByText("Double click in here");
        await area.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        await Expect(Canvas).ToHaveCountAsync(0);

        await area.DblClickAsync();
        await Expect(Canvas).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task FireFrom_Completes_When_The_Burst_Lands()
    {
        await Open("confetti");

        await Button("Burst from this button, then report").ClickAsync();
        await Expect(Status).ToHaveTextAsync("In the air...");
        await Expect(Canvas).ToHaveCountAsync(1);

        await Expect(Status).ToHaveTextAsync("Landed.", new() { Timeout = 15_000 });
        await Expect(Canvas).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clear_Removes_Everything_And_Completes_Pending_Bursts()
    {
        await Open("confetti");

        await Button("Burst from this button, then report").ClickAsync();
        await Expect(Status).ToHaveTextAsync("In the air...");

        await Button("Clear").ClickAsync();
        await Expect(Canvas).ToHaveCountAsync(0, new() { Timeout = 1_000 });
        await Expect(Status).ToHaveTextAsync("Landed.", new() { Timeout = 1_000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Service_Presets_Fire()
    {
        await Open("confetti");

        await Button("Fireworks").ClickAsync();
        await Expect(Canvas).ToHaveCountAsync(1);

        await Button("Clear").ClickAsync();
        await Expect(Canvas).ToHaveCountAsync(0);

        await Button("Side cannons").ClickAsync();
        await Expect(Canvas).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }
}
