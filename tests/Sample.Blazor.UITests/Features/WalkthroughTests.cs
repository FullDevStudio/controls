namespace Sample.Blazor.UITests.Features;

/// <summary>Walkthrough on <c>/walkthrough</c>: step order, Next/Back/Skip, conditional steps, target clicks, auto-start.</summary>
// The tests share the browser context's localStorage, where the tour remembers it has run; running
// them one at a time keeps a Skip in one from racing the auto-start check in another.
[NotInParallel(nameof(WalkthroughTests))]
public class WalkthroughTests : BlazorTest
{
    const string RememberKey = "shiny.walkthrough.sample-walkthrough-v1";

    ILocator Callout => Page.Locator(".shiny-walkthrough__callout");
    ILocator Title => Callout.Locator(".shiny-walkthrough__title");
    ILocator Counter => Callout.Locator(".shiny-walkthrough__counter");
    ILocator Status => Page.Locator(".demo-section em").Last;

    /// <summary>
    /// Restart clears the remembered flag and runs from step one, whatever an auto-start (or a parallel
    /// test sharing the browser's localStorage) already did.
    /// </summary>
    async Task Restart()
    {
        await Page.GetByRole(AriaRole.Button, new() { Name = "Restart", Exact = true }).ClickAsync(new() { Force = true });
        await Expect(Title).ToHaveTextAsync("Welcome");
    }

    Task Next() => Callout.GetByRole(AriaRole.Button, new() { Name = "Next", Exact = true }).ClickAsync();

    [Test]
    public async Task Restart_Begins_At_Welcome_And_Next_Moves_To_The_Target_Step()
    {
        await Open("walkthrough");
        await Restart();
        await Expect(Counter).ToHaveTextAsync("1 of 5");
        await Expect(Status).ToHaveTextAsync("Entered step: Welcome");

        await Next();
        await Expect(Title).ToHaveTextAsync("Search");
        await Expect(Counter).ToHaveTextAsync("2 of 5");
        await Expect(Status).ToHaveTextAsync("Entered step: Search");

        // the spotlight travels onto the search box (it animates, so poll until it has arrived)
        await Page.WaitForFunctionAsync(
            """
            () => {
                const s = document.querySelector('.shiny-walkthrough__spot').getBoundingClientRect();
                const t = document.querySelector('#wt-search').getBoundingClientRect();
                const cx = t.left + t.width / 2, cy = t.top + t.height / 2;
                return s.width > 0 && s.left <= cx && s.right >= cx && s.top <= cy && s.bottom >= cy;
            }
            """,
            null,
            new() { Timeout = 5000 }
        );
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Arrow_Keys_Navigate_And_Escape_Skips()
    {
        await Open("walkthrough");
        await Restart();

        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Title).ToHaveTextAsync("Search");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(Title).ToHaveTextAsync("Welcome");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(Callout).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync("Tour skipped. Restart clears the remembered flag.");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Skip_Button_Ends_The_Tour()
    {
        await Open("walkthrough");
        await Restart();

        await Callout.GetByRole(AriaRole.Button, new() { Name = "Skip", Exact = true }).ClickAsync();
        await Expect(Callout).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync("Tour skipped. Restart clears the remembered flag.");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Conditional_Step_Joins_The_Run_When_Ticked()
    {
        await Open("walkthrough");
        await Restart();
        await Expect(Counter).ToHaveTextAsync("1 of 5");
        await Page.Keyboard.PressAsync("Escape");
        await Expect(Callout).ToHaveCountAsync(0);

        await Page.Locator("label", new() { HasTextString = "Include the Advanced step" }).Locator("input").CheckAsync();
        await Restart();

        await Expect(Counter).ToHaveTextAsync("1 of 6");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Timed_Step_Advances_Itself_And_Target_Click_Finishes_The_Tour()
    {
        await Open("walkthrough");
        await Restart();
        await Next();
        await Expect(Title).ToHaveTextAsync("Search");
        await Next();

        // Filter is a 2.5s Tooltip step: no buttons, it moves on by itself
        await Expect(Status).ToHaveTextAsync("Entered step: Filter");
        await Expect(Status).ToHaveTextAsync("Entered step: Avatar", new() { Timeout = 6000 });

        await Next();
        await Expect(Title).ToHaveTextAsync("Try it");
        await Expect(Status).ToHaveTextAsync("Entered step: Save");
        await Page.WaitForTimeoutAsync(800); // let the spotlight settle on the target

        // the Save button is live through the cut-out, and clicking it completes the tour
        await Page.Locator("#wt-save").ClickAsync();
        await Expect(Callout).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("Saved 1 time|Tour completed"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Auto_Starts_When_The_Tour_Has_Not_Run()
    {
        await Page.AddInitScriptAsync($"try {{ localStorage.removeItem('{RememberKey}'); }} catch {{}}");
        await Open("walkthrough");

        await Expect(Title).ToHaveTextAsync("Welcome", new() { Timeout = 10_000 });
        await Expect(Status).ToHaveTextAsync("Entered step: Welcome");
        await AssertNoConsoleErrors();
    }
}
