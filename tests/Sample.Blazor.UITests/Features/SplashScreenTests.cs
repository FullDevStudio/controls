namespace Sample.Blazor.UITests.Features;

/// <summary>Splash screen replay on <c>/splashscreen</c>: shows with the chosen options, steps status/progress through ISplashScreen, hides.</summary>
public class SplashScreenTests : BlazorTest
{
    ILocator Splash => Page.Locator("#shiny-splash");

    [Test]
    public async Task Replay_Shows_Status_And_Progress_Then_Hides()
    {
        await Open("splashscreen");
        await Expect(Splash).ToBeHiddenAsync();

        var replay = Page.GetByRole(AriaRole.Button, new() { Name = "Replay Splash", Exact = true });
        await replay.ClickAsync();

        await Expect(Splash).ToBeVisibleAsync();
        await Expect(Splash).ToHaveAttributeAsync("role", "progressbar");
        await Expect(Splash.Locator(".shiny-splash-title")).ToHaveTextAsync("Shiny Controls");
        await Expect(Splash.Locator(".shiny-splash-subtitle")).ToHaveTextAsync("Blazor Gallery");
        await Expect(replay).ToBeDisabledAsync();

        // the scripted sequence walks the status line and the determinate bar
        await Expect(Splash.Locator(".shiny-splash-status")).ToHaveTextAsync("Loading accounts…", new() { Timeout = 5000 });
        var pct = await Splash.EvaluateAsync<string>("e => e.style.getPropertyValue('--shiny-splash-progress-pct')");
        await Assert.That(pct).IsEqualTo("55%");

        await Expect(Splash).ToBeHiddenAsync(new() { Timeout = 8000 });
        await Expect(replay).ToBeEnabledAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Replay_Uses_The_Chosen_Title_And_Indeterminate_Mode()
    {
        await Open("splashscreen");
        var options = Page.Locator(".splash-options");
        var title = options.GetByRole(AriaRole.Textbox, new() { Name = "Title", Exact = true });
        await title.FillAsync("Custom Title");
        await title.PressAsync("Tab");
        await options.Locator("label", new() { HasTextString = "Determinate progress" }).Locator("input").UncheckAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Replay Splash", Exact = true }).ClickAsync();

        await Expect(Splash.Locator(".shiny-splash-title")).ToHaveTextAsync("Custom Title");
        await Expect(Splash).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bshiny-splash--indeterminate\b"));
        await Expect(Splash).ToBeHiddenAsync(new() { Timeout = 8000 });
        await AssertNoConsoleErrors();
    }
}
