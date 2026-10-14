using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class MarqueeTests : BlazorTest
{
    ILocator Section(string heading) => Page.Locator("section.demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });
    ILocator Reviews => Section("Reviews, both directions").Locator(".shiny-marquee").First;
    ILocator ReviewsReversed => Section("Reviews, both directions").Locator(".shiny-marquee").Nth(1);
    ILocator Angled => Section("Any angle").Locator(".shiny-marquee");
    ILocator Ticker => Section("Stopped and started").Locator(".shiny-marquee");

    static Task<string> PlayState(ILocator marquee)
        => marquee.Locator(".shiny-marquee__group").First.EvaluateAsync<string>("el => getComputedStyle(el).animationPlayState");

    static Task<double> GroupOffset(ILocator marquee)
        => marquee.Locator(".shiny-marquee__group").First.EvaluateAsync<double>("el => el.getBoundingClientRect().left");

    [Test]
    public async Task Content_Moves_While_Running()
    {
        await Open("marquee");
        await Ticker.ScrollIntoViewIfNeededAsync();

        var before = await GroupOffset(Ticker);
        await Page.WaitForTimeoutAsync(600);
        var after = await GroupOffset(Ticker);
        await Assert.That(Math.Abs(after - before)).IsGreaterThan(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Unticking_Running_Pauses_And_Holds_Position()
    {
        await Open("marquee");

        var running = Section("Stopped and started").GetByRole(AriaRole.Checkbox);
        await Expect(running).ToBeCheckedAsync();
        await Expect(Ticker).Not.ToHaveClassAsync(new Regex("shiny-marquee--paused"));

        await running.UncheckAsync();
        await Expect(Ticker).ToHaveClassAsync(new Regex("shiny-marquee--paused"));
        await Assert.That(await PlayState(Ticker)).IsEqualTo("paused");

        var before = await GroupOffset(Ticker);
        await Page.WaitForTimeoutAsync(600);
        await Assert.That(await GroupOffset(Ticker)).IsEqualTo(before);

        await running.CheckAsync();
        await Expect(Ticker).Not.ToHaveClassAsync(new Regex("shiny-marquee--paused"));
        await Assert.That(await PlayState(Ticker)).IsEqualTo("running");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Hover_Pauses_When_PauseOnHover()
    {
        await Open("marquee");
        await Expect(Reviews).ToHaveClassAsync(new Regex("shiny-marquee--pause-hover"));

        await Reviews.HoverAsync();
        await Assert.That(await PlayState(Reviews)).IsEqualTo("paused");

        await Page.Mouse.MoveAsync(0, 0);
        await Assert.That(await PlayState(Reviews)).IsEqualTo("running");

        // the ticker has no PauseOnHover: hovering it changes nothing
        await Ticker.HoverAsync();
        await Assert.That(await PlayState(Ticker)).IsEqualTo("running");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Reverse_Runs_The_Other_Way()
    {
        await Open("marquee");

        var forward = await Reviews.Locator(".shiny-marquee__group").First.EvaluateAsync<string>("el => getComputedStyle(el).animationDirection");
        var reverse = await ReviewsReversed.Locator(".shiny-marquee__group").First.EvaluateAsync<string>("el => getComputedStyle(el).animationDirection");
        await Assert.That(forward).IsEqualTo("normal");
        await Assert.That(reverse).IsEqualTo("reverse");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Only_The_First_Copy_Is_Exposed_To_Assistive_Tech()
    {
        await Open("marquee");

        var groups = Reviews.Locator(".shiny-marquee__group");
        await Assert.That(await groups.CountAsync()).IsGreaterThanOrEqualTo(2);
        await Expect(groups.First).Not.ToHaveAttributeAsync("aria-hidden", "true");
        await Expect(groups.Nth(1)).ToHaveAttributeAsync("aria-hidden", "true");

        // every copy carries the full item set
        await Expect(groups.First.Locator("figure")).ToHaveCountAsync(6);
        await Expect(groups.Nth(1).Locator("figure")).ToHaveCountAsync(6);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Keep_Upright_Toggles_The_Upright_Mode()
    {
        await Open("marquee");

        var upright = Section("Any angle").GetByRole(AriaRole.Checkbox);
        await Expect(Angled).ToHaveClassAsync(new Regex("shiny-marquee--angled"));
        await Expect(Angled).ToHaveClassAsync(new Regex("shiny-marquee--upright"));

        await upright.UncheckAsync();
        await Expect(Angled).Not.ToHaveClassAsync(new Regex("shiny-marquee--upright"));
        await Expect(Angled).ToHaveClassAsync(new Regex("shiny-marquee--angled"));

        await upright.CheckAsync();
        await Expect(Angled).ToHaveClassAsync(new Regex("shiny-marquee--upright"));
        await AssertNoConsoleErrors();
    }
}
