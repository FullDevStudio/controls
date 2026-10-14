using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class MotionIconsTests : BlazorTest
{
    static readonly Regex Playing = new("shiny-motion-icon--playing");

    ILocator Section(string heading) => Page.Locator("section.demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });
    ILocator Icon(string label) => Page.Locator($".shiny-motion-icon[aria-label='{label}']").First;
    ILocator Gallery => Page.Locator(".mi-gallery .mi-cell");
    ILocator Caption => Section("The whole set").Locator("p").First;
    ILocator Filter => Page.GetByPlaceholder("Filter icons");

    [Test]
    public async Task Loop_Trigger_Plays_Without_Interaction()
    {
        await Open("motionicons");

        await Expect(Icon("Loop")).ToHaveClassAsync(Playing);
        await Expect(Icon("Loop")).ToHaveAttributeAsync("style", new Regex("--shiny-mi-iterations:infinite"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Hover_Trigger_Plays_On_Pointer_Enter()
    {
        await Open("motionicons");

        var icon = Icon("Hover");
        await Expect(icon).Not.ToHaveClassAsync(Playing);

        await icon.HoverAsync();
        await Expect(icon).ToHaveClassAsync(Playing);

        // leaving lets the cycle finish, then it settles
        await Page.Mouse.MoveAsync(0, 0);
        await Expect(icon).Not.ToHaveClassAsync(Playing, new() { Timeout = 5_000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Press_Trigger_Plays_One_Cycle_On_Click()
    {
        await Open("motionicons");

        var icon = Icon("Press");
        await Expect(icon).Not.ToHaveClassAsync(Playing);

        await icon.ClickAsync();
        await Expect(icon).ToHaveClassAsync(Playing);
        await Expect(icon).Not.ToHaveClassAsync(Playing, new() { Timeout = 5_000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Busy_Flag_Starts_And_Stops_The_Spinner()
    {
        await Open("motionicons");

        var row = Section("Driven from code");
        var spinner = row.Locator(".shiny-motion-icon").First;
        var toggle = row.GetByRole(AriaRole.Button, new() { Name = "Start busy state" });

        await Expect(spinner).Not.ToHaveClassAsync(Playing);
        await toggle.ClickAsync();
        await Expect(spinner).ToHaveClassAsync(Playing);

        // still spinning well past one cycle - it loops for as long as the flag is set
        await Page.WaitForTimeoutAsync(2000);
        await Expect(spinner).ToHaveClassAsync(Playing);

        await row.GetByRole(AriaRole.Button, new() { Name = "Stop busy state" }).ClickAsync();
        await Expect(spinner).Not.ToHaveClassAsync(Playing, new() { Timeout = 5_000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Play_Runs_RepeatCount_Cycles_Then_Stops()
    {
        await Open("motionicons");

        var row = Section("Driven from code");
        var downloader = row.Locator(".shiny-motion-icon").Nth(1);
        await Expect(downloader).ToHaveAttributeAsync("style", new Regex("--shiny-mi-iterations:2"));
        await Expect(downloader).Not.ToHaveClassAsync(Playing);

        await row.GetByRole(AriaRole.Button, new() { Name = "Play twice" }).ClickAsync();
        await Expect(downloader).ToHaveClassAsync(Playing);
        await Expect(downloader).Not.ToHaveClassAsync(Playing, new() { Timeout = 6_000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Choosing_A_Preset_Recompiles_Both_Icons()
    {
        await Open("motionicons");

        var icons = Section("Presets on any icon").Locator(".shiny-motion-icon");
        var starBefore = await icons.Nth(0).GetAttributeAsync("class");
        var pathBefore = await icons.Nth(1).GetAttributeAsync("class");

        await Section("Presets on any icon").Locator("select").SelectOptionAsync("Spin");

        // each motion compiles to its own scoped @keyframes class
        await Expect(icons.Nth(0)).Not.ToHaveAttributeAsync("class", starBefore!);
        await Expect(icons.Nth(1)).Not.ToHaveAttributeAsync("class", pathBefore!);
        await Expect(icons.Nth(0)).ToHaveClassAsync(Playing);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Filter_Narrows_The_Gallery()
    {
        await Open("motionicons");

        var total = await Gallery.CountAsync();
        await Assert.That(total).IsGreaterThan(50);
        await Expect(Caption).ToContainTextAsync($"{total} icons, each with its own motion.");

        await Filter.FillAsync("arrow");
        await Expect(Caption).ToContainTextAsync(new Regex($@"\d+ of {total} icons\."));

        var shown = await Gallery.CountAsync();
        await Assert.That(shown).IsGreaterThan(0);
        await Assert.That(shown).IsLessThan(total);
        await Expect(Caption).ToContainTextAsync($"{shown} of {total} icons.");
        foreach (var label in await Gallery.Locator("span:not(.shiny-motion-icon)").AllInnerTextsAsync())
            await Assert.That(label).Contains("arrow");

        await Filter.FillAsync("no-such-icon-xyz");
        await Expect(Gallery).ToHaveCountAsync(0);

        await Filter.FillAsync("");
        await Expect(Gallery).ToHaveCountAsync(total);
        await AssertNoConsoleErrors();
    }
}
