namespace Sample.Blazor.UITests.Features;

/// <summary>/animated-backgrounds: AnimatedBackground.</summary>
public class AnimatedBackgroundTests : BlazorTest
{
    ILocator Section(string heading)
        => Page.Locator("section.demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });

    ILocator Playground => Section("Playground");
    ILocator Hero => Playground.Locator(".shiny-animated-background");
    ILocator HeroCanvas => Hero.Locator("canvas");

    /// <summary>The canvas's pixels, so two reads tell whether anything was painted between them.</summary>
    static Task<string> Pixels(ILocator canvas) => canvas.EvaluateAsync<string>("c => c.toDataURL()");

    /// <summary>
    /// Whether the canvas changes within <paramref name="withinMs"/>. Sparse effects can sit still for a
    /// moment - Meteors leave an empty sky for up to MaxDelay (1.5s on the demo) between streaks.
    /// </summary>
    async Task<bool> IsMoving(ILocator canvas, int withinMs = 3000)
    {
        var before = await Pixels(canvas);
        for (var waited = 0; waited < withinMs; waited += 250)
        {
            await Page.WaitForTimeoutAsync(250);
            if (before != await Pixels(canvas))
                return true;
        }
        return false;
    }


    [Test]
    public async Task Hero_Effect_Animates()
    {
        await Open("animated-backgrounds");
        await Expect(HeroCanvas).ToBeVisibleAsync();

        await Assert.That(await IsMoving(HeroCanvas)).IsTrue();
        // the script sizes the canvas to the box
        await Assert.That(await HeroCanvas.EvaluateAsync<int>("c => c.width")).IsGreaterThan(300);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Content_On_Top_Gets_Real_Clicks()
    {
        await Open("animated-backgrounds");
        var button = Playground.GetByRole(AriaRole.Button, new() { Name = "A real button (0)" });

        await button.ClickAsync();
        await Expect(Playground.GetByRole(AriaRole.Button, new() { Name = "A real button (1)" })).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Content_Is_Readable_On_The_Dark_Effect()
    {
        await Open("animated-backgrounds");

        // the root's colour must reach the heading over the page's own h2 rule
        var color = await Hero.GetByRole(AriaRole.Heading).EvaluateAsync<string>("h => getComputedStyle(h).color");
        await Assert.That(color).IsEqualTo("rgb(255, 255, 255)");

        // and the subtitle, which the page's p rule turned dark the same way
        var subtitle = await Hero.Locator("p").EvaluateAsync<string>("p => getComputedStyle(p).color");
        await Assert.That(subtitle).IsEqualTo("rgb(255, 255, 255)");
    }


    [Test]
    public async Task Choosing_An_Effect_Swaps_It()
    {
        await Open("animated-backgrounds");

        await Playground.Locator("select").SelectOptionAsync(new SelectOptionValue { Label = "Matrix" });
        await Expect(Hero.GetByRole(AriaRole.Heading)).ToHaveTextAsync("Matrix");
        await Assert.That(await IsMoving(HeroCanvas)).IsTrue();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Unticking_Animate_Holds_A_Still_Frame()
    {
        await Open("animated-backgrounds");

        await Playground.GetByLabel("Animate").UncheckAsync();
        await Page.WaitForTimeoutAsync(500);
        await Assert.That(await IsMoving(HeroCanvas)).IsFalse();

        // still painted, not blank
        var blank = await HeroCanvas.EvaluateAsync<string>(
            "c => { const b = document.createElement('canvas'); b.width = c.width; b.height = c.height; return b.toDataURL(); }");
        await Assert.That(await Pixels(HeroCanvas)).IsNotEqualTo(blank);

        await Playground.GetByLabel("Animate").CheckAsync();
        await Assert.That(await IsMoving(HeroCanvas)).IsTrue();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Fixed_Pins_The_Canvas_To_The_Viewport()
    {
        await Open("animated-backgrounds");

        await Playground.GetByLabel("Fixed (full-page background)").CheckAsync();
        await Expect(HeroCanvas).ToHaveCSSAsync("position", "fixed");

        var size = Page.ViewportSize!;
        await Page.WaitForFunctionAsync(
            "([c, w, h]) => { const r = c.getBoundingClientRect(); return r.x === 0 && r.y === 0 && r.width === w && r.height === h; }",
            new object[] { await HeroCanvas.ElementHandleAsync(), size.Width, size.Height }
        );

        await Playground.GetByLabel("Fixed (full-page background)").UncheckAsync();
        await Expect(HeroCanvas).ToHaveCSSAsync("position", "absolute");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Every_Gallery_Effect_Paints_And_Moves()
    {
        await Open("animated-backgrounds");
        var canvases = Section("Gallery").Locator("canvas");
        await Expect(canvases).ToHaveCountAsync(12);

        for (var i = 0; i < 12; i++)
        {
            var canvas = canvases.Nth(i);
            await canvas.ScrollIntoViewIfNeededAsync();
            await Page.WaitForTimeoutAsync(300);
            await Assert.That(await IsMoving(canvas)).IsTrue().Because($"gallery effect {i} should be animating");
        }
        await AssertNoConsoleErrors();
    }
}
