using System.Globalization;
using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class GanttTests : BlazorTest
{
    ILocator Status => Page.Locator("p.status-message");
    ILocator Rows => Page.Locator(".shiny-gantt-row");
    ILocator Row(string name) => Rows.Filter(new() { Has = Page.Locator(".shiny-gantt-celltext", new() { HasTextRegex = new Regex($"^{Regex.Escape(name)}$") }) });
    ILocator Bar(string name) => Page.Locator($".shiny-gantt-bar[title^='{name}']");
    ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });
    ILocator Toggle(string label) => Page.Locator("label.toggle-row", new() { HasText = label }).Locator("input");


    [Test]
    public async Task Chevron_Collapses_A_Summary_Task()
    {
        await Open("gantt");
        await Expect(Rows).ToHaveCountAsync(12);
        await Expect(Row("User interviews")).ToHaveCountAsync(1);

        var chevron = Page.GetByRole(AriaRole.Button, new() { Name = "Collapse Discovery" });
        await chevron.ClickAsync();

        await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Expand Discovery" })).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(Rows).ToHaveCountAsync(10);
        await Expect(Row("User interviews")).ToHaveCountAsync(0);
        await Expect(Bar("User interviews")).ToHaveCountAsync(0);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Expand Discovery" }).ClickAsync();
        await Expect(Rows).ToHaveCountAsync(12);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Collapse_All_And_Expand_All()
    {
        await Open("gantt");

        await Button("Collapse").ClickAsync();
        await Expect(Rows).ToHaveCountAsync(1);
        await Expect(Row("Mobile app v2")).ToHaveCountAsync(1);

        await Button("Expand").ClickAsync();
        await Expect(Rows).ToHaveCountAsync(12);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Clicking_A_Row_Selects_The_Task()
    {
        await Open("gantt");

        await Row("Wireframes").ClickAsync();

        await Expect(Row("Wireframes")).ToHaveClassAsync(new Regex(@"\bis-selected\b"));
        // The page shows a detail section for the bound SelectedTask.
        await Expect(Page.Locator("section.demo-section h2", new() { HasText = "Wireframes" })).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Critical_Path_Toggle_Clears_Highlighting()
    {
        await Open("gantt");
        await Expect(Page.Locator(".shiny-gantt-bar.is-critical").First).ToBeVisibleAsync();

        await Toggle("Critical path").UncheckAsync();

        await Expect(Page.Locator(".shiny-gantt-bar.is-critical")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".shiny-gantt-link.is-critical")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Work_Week_Toggle_Removes_Weekend_Shading()
    {
        await Open("gantt");
        await Expect(Page.Locator(".shiny-gantt-nonworking").First).ToBeAttachedAsync();

        await Toggle("Work week").UncheckAsync();

        await Expect(Page.Locator(".shiny-gantt-nonworking")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".shiny-gantt-tick.is-nonworking")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Baselines_Toggle()
    {
        await Open("gantt");
        await Expect(Page.Locator(".shiny-gantt-baseline").First).ToBeAttachedAsync();

        await Toggle("Baselines").UncheckAsync();

        await Expect(Page.Locator(".shiny-gantt-baseline")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Zoom_Buttons_Change_The_Timeline_Width()
    {
        await Open("gantt");
        var canvas = Page.Locator(".shiny-gantt-canvas");
        var start = await Width(canvas);

        await Button("+").ClickAsync();
        await WaitForWidth(canvas, w => w > start * 1.2, "zoom in to widen the timeline");
        var zoomed = await Width(canvas);

        await Button("−").ClickAsync();
        await Button("−").ClickAsync();
        await WaitForWidth(canvas, w => w < zoomed * 0.8, "zoom out to narrow the timeline");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Week_Scale_Relabels_The_Header()
    {
        await Open("gantt");
        var lowerTicks = Page.Locator(".shiny-gantt-tier.is-lower .shiny-gantt-tick");
        var dayTicks = await lowerTicks.CountAsync();

        await Page.Locator("label", new() { HasText = "Scale" }).Locator("select").SelectOptionAsync("Week");

        await Page.WaitForFunctionAsync(
            "n => document.querySelectorAll('.shiny-gantt-tier.is-lower .shiny-gantt-tick').length < n",
            dayTicks);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Dragging_A_Bar_Reschedules_And_Undo_Reverts()
    {
        await Open("gantt");
        await Expect(Button("Undo")).ToBeDisabledAsync();

        var bar = Bar("API");
        await bar.ScrollIntoViewIfNeededAsync();
        var box = await bar.BoundingBoxAsync() ?? throw new InvalidOperationException("API bar has no box");
        var x = box.X + box.Width / 2;
        var y = box.Y + box.Height / 2;

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x + 40, y, new() { Steps = 5 });
        await Page.Mouse.MoveAsync(x + 90, y, new() { Steps = 5 });
        await Page.Mouse.UpAsync();

        await Expect(Status).ToHaveTextAsync(new Regex(@"^API: Move"));
        await Expect(Button("Undo")).ToBeEnabledAsync();

        await Button("Undo").ClickAsync();
        await Expect(Status).ToHaveTextAsync("Reverted Move on API");
        await Expect(Button("Undo")).ToBeDisabledAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Reset_Restores_The_Plan()
    {
        await Open("gantt");
        await Button("Collapse").ClickAsync();
        await Row("Mobile app v2").ClickAsync();

        await Button("Reset").ClickAsync();

        await Expect(Status).ToHaveTextAsync("Plan reset");
        await Expect(Rows).ToHaveCountAsync(12);
        await Expect(Page.Locator(".shiny-gantt-row.is-selected")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    static async Task<double> Width(ILocator locator)
        => (await locator.BoundingBoxAsync())?.Width ?? 0;

    async Task WaitForWidth(ILocator locator, Func<double, bool> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        double last = 0;
        while (DateTime.UtcNow < deadline)
        {
            last = await Width(locator);
            if (condition(last))
                return;
            await Page.WaitForTimeoutAsync(200);
        }
        Assert.Fail($"Timed out waiting for {what} (width {last.ToString(CultureInfo.InvariantCulture)})");
    }
}
