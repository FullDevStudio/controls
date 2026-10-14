namespace Sample.Blazor.UITests.Features;

public class SkeletonTests : BlazorTest
{
    ILocator Section(string heading) => Page.Locator("section.demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });
    ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    [Test]
    public async Task Busy_Shows_Placeholder_Lines_Instead_Of_Content()
    {
        await Open("skeleton");

        var section = Section("Default Skeleton");
        // ItemCount="4": four shimmer lines, the last one shorter
        var lines = section.Locator(".shiny-skeleton__line");
        await Expect(lines).ToHaveCountAsync(4);
        await Expect(lines.Last).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex("width: 60%"));
        await Expect(section.GetByText("Loaded Article")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Reload_Reveals_The_Content_When_Done()
    {
        await Open("skeleton");

        var section = Section("Default Skeleton");
        await Button("Reload").ClickAsync();

        // still loading for 2.5s
        await Expect(section.Locator(".shiny-skeleton__line")).ToHaveCountAsync(4);

        await Expect(section.GetByText("Loaded Article")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(section.Locator(".shiny-skeleton__line")).ToHaveCountAsync(0);

        // and a second reload brings the placeholder back
        await Button("Reload").ClickAsync();
        await Expect(section.Locator(".shiny-skeleton__line")).ToHaveCountAsync(4);
        await Expect(section.GetByText("Loaded Article")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Custom_Template_Replaces_The_Default_Lines()
    {
        await Open("skeleton");

        var section = Section("Custom Skeleton (Card)");
        await Expect(section.Locator(".shiny-skeleton__shape")).ToHaveCountAsync(3);
        await Expect(section.Locator(".shiny-skeleton__line")).ToHaveCountAsync(0);

        // the custom shapes get the shimmer
        var animation = await section.Locator(".shiny-skeleton__shape").First.EvaluateAsync<string>("el => getComputedStyle(el).animationName + '|' + getComputedStyle(el, '::after').animationName");
        await Assert.That(animation).IsNotEqualTo("none|none");

        await Button("Reload Card").ClickAsync();
        await Expect(section.GetByText("Allan Ritchie")).ToBeVisibleAsync(new() { Timeout = 10_000 });
        await Expect(section.Locator(".shiny-skeleton__shape")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Each_Skeleton_Reloads_Independently()
    {
        await Open("skeleton");

        await Button("Reload Card").ClickAsync();
        await Expect(Section("Custom Skeleton (Card)").GetByText("Allan Ritchie")).ToBeVisibleAsync(new() { Timeout = 10_000 });

        // the article was never reloaded, so it is still a placeholder
        await Expect(Section("Default Skeleton").Locator(".shiny-skeleton__line")).ToHaveCountAsync(4);
        await AssertNoConsoleErrors();
    }
}
