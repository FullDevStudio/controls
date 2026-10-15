using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>
/// BorderBeam and ShineBorder are pure CSS: a ring laid over the content and masked to the border. These
/// check the ring is there, really animates, pauses on demand, and never takes clicks from the content.
/// </summary>
public class BorderAnimationsTests : BlazorTest
{
    ILocator Section(string heading) => Page.Locator("section.demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });
    ILocator Beam => Section("Border Beam").Locator(".shiny-border-beam").First;
    ILocator Shine => Section("Shine Border").Locator(".shiny-shine-border").First;
    ILocator RunningBeam => Section("Running").Locator(".shiny-border-beam");
    ILocator RunningShine => Section("Running").Locator(".shiny-shine-border");

    [Test]
    public async Task Each_Border_Draws_A_Ring_Over_Its_Content()
    {
        await Open("border-animations");

        await Expect(Beam.Locator(".shiny-border-beam__ring .shiny-border-beam__beam")).ToHaveCountAsync(1);
        await Expect(Shine.Locator(".shiny-shine-border__ring")).ToHaveCountAsync(1);

        // the content is still there and on top of nothing that eats it
        await Expect(Beam.GetByRole(AriaRole.Heading, new() { Name = "Login" })).ToBeVisibleAsync();
        await Expect(Shine.GetByRole(AriaRole.Heading, new() { Name = "Pro plan" })).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Rings_Never_Take_Clicks()
    {
        await Open("border-animations");

        await Expect(Beam.Locator(".shiny-border-beam__ring")).ToHaveCSSAsync("pointer-events", "none");
        await Expect(Shine.Locator(".shiny-shine-border__ring")).ToHaveCSSAsync("pointer-events", "none");

        // the reversed overlay beam sits on top of the second card, and the click still lands
        await Beam.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync(new() { Trial = true });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Beam_Actually_Moves()
    {
        await Open("border-animations");
        var beam = Beam.Locator(".shiny-border-beam__beam");

        await Expect(beam).ToHaveCSSAsync("animation-play-state", "running");
        var first = await beam.EvaluateAsync<double>("e => e.getAnimations()[0]?.currentTime ?? -1");
        await Page.WaitForTimeoutAsync(400);
        var second = await beam.EvaluateAsync<double>("e => e.getAnimations()[0]?.currentTime ?? -1");

        await Assert.That(first).IsGreaterThanOrEqualTo(0);
        await Assert.That(second).IsGreaterThan(first);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Animate_Checkbox_Pauses_And_Resumes_Both()
    {
        await Open("border-animations");
        var animate = Section("Running").GetByLabel("Animate");

        await animate.UncheckAsync();
        await Expect(RunningBeam).ToHaveClassAsync(new Regex("shiny-border-beam--paused"));
        await Expect(RunningShine).ToHaveClassAsync(new Regex("shiny-shine-border--paused"));
        await Expect(RunningBeam.Locator(".shiny-border-beam__beam")).ToHaveCSSAsync("animation-play-state", "paused");
        await Expect(RunningShine.Locator(".shiny-shine-border__ring")).ToHaveCSSAsync("animation-play-state", "paused");

        await animate.CheckAsync();
        await Expect(RunningBeam).Not.ToHaveClassAsync(new Regex("--paused"));
        await Expect(RunningShine).Not.ToHaveClassAsync(new Regex("--paused"));
        await AssertNoConsoleErrors();
    }
}
