using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class VirtualizedGridTests : BlazorTest
{
    ILocator Section(string heading)
        => Page.Locator("section.demo-section", new() { Has = Page.Locator("h2", new() { HasTextString = heading }) });

    ILocator Basic => Section("Basic Grid");
    ILocator BasicColumns => Basic.Locator("strong").First;
    ILocator BasicGrid => Basic.Locator(".shiny-vgrid-grid").First;


    [Test]
    public async Task Column_Buttons_Change_The_Grid_Within_Limits()
    {
        await Open("virtualizedgrid");
        await Expect(BasicColumns).ToHaveTextAsync("3");
        await Expect(BasicGrid).ToHaveAttributeAsync("style", new Regex(@"repeat\(3,1fr\)"));

        var plus = Basic.GetByRole(AriaRole.Button, new() { Name = "+", Exact = true });
        var minus = Basic.GetByRole(AriaRole.Button, new() { Name = "-", Exact = true });

        for (var i = 0; i < 5; i++)
            await plus.ClickAsync();
        await Expect(BasicColumns).ToHaveTextAsync("6");
        await Expect(BasicGrid).ToHaveAttributeAsync("style", new Regex(@"repeat\(6,1fr\)"));

        for (var i = 0; i < 7; i++)
            await minus.ClickAsync();
        await Expect(BasicColumns).ToHaveTextAsync("1");
        await Expect(BasicGrid).ToHaveAttributeAsync("style", new Regex(@"repeat\(1,1fr\)"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Clicking_A_Cell_Selects_It()
    {
        await Open("virtualizedgrid");
        await Basic.Locator(".shiny-vgrid-cell", new() { HasTextRegex = new Regex(@"Item 5$") }).ClickAsync();
        await Expect(Basic.Locator("p", new() { HasTextString = "Selected:" }).Locator("strong")).ToHaveTextAsync("Item 5");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Large_Dataset_Only_Renders_What_Is_Visible()
    {
        await Open("virtualizedgrid");
        var section = Section("Virtualized (Large Dataset)");
        var cells = section.Locator(".shiny-vgrid-cell");

        // the grid windows against the page scroller - it has no box of its own
        // (its top, not its middle - centring a 500-item section scrolls past the first rows)
        await section.EvaluateAsync(
            "s => { const c = document.querySelector('.shiny-applayout-content') ?? document.scrollingElement; c.scrollBy(0, s.getBoundingClientRect().top - c.getBoundingClientRect().top); }");
        await Expect(cells.First).ToContainTextAsync("#1");
        var rendered = await cells.CountAsync();
        await Assert.That(rendered).IsLessThan(200);           // 500 items, one screen of them
        await Expect(section.GetByText("#500", new() { Exact = false })).ToHaveCountAsync(0);

        // bring the grid's end into view; Virtualize re-measures as rows render, so walk it there
        var last = section.Locator(".shiny-vgrid-cell", new() { HasTextRegex = new Regex(@"#500$") });
        for (var i = 0; i < 20 && await last.CountAsync() == 0; i++)
        {
            await section.EvaluateAsync(
                "s => { const c = document.querySelector('.shiny-applayout-content') ?? document.scrollingElement; c.scrollBy(0, s.getBoundingClientRect().bottom - c.getBoundingClientRect().bottom + 40); }");
            await Page.WaitForTimeoutAsync(150);
        }
        await Expect(section.Locator(".shiny-vgrid-cell", new() { HasTextRegex = new Regex(@"#500$") })).ToBeVisibleAsync();
        await Expect(section.Locator(".shiny-vgrid-cell", new() { HasTextRegex = new Regex(@"#1$") })).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Load_More_Appends_Until_The_Total_Then_Hides_The_Button()
    {
        await Open("virtualizedgrid");
        var section = Section("With Header, Footer & Load More");
        var footer = section.Locator(".shiny-vgrid-footer");
        var button = section.Locator(".shiny-vgrid-loadmore-btn");

        await Expect(section.Locator(".shiny-vgrid-header")).ToContainTextAsync("Featured Collection");
        await Expect(footer).ToContainTextAsync("Showing 9 of 30 items");

        await button.ClickAsync();
        await Expect(footer).ToContainTextAsync("Showing 15 of 30 items", new() { Timeout = 10_000 });
        await Expect(section.Locator(".shiny-vgrid-cell")).ToHaveCountAsync(15);

        // Six per page: 21, 27, then 33 crosses the 30 total and the button goes away.
        foreach (var expected in new[] { 21, 27, 33 })
        {
            await button.ClickAsync();
            await Expect(footer).ToContainTextAsync($"Showing {expected} of 30 items", new() { Timeout = 10_000 });
        }
        await Expect(button).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Group_Header_Sticks_While_Its_Group_Scrolls()
    {
        await Open("virtualizedgrid");
        var section = Section("Grouped Grid with Sticky Headers");
        var scroller = Page.Locator(".shiny-applayout-content").First;
        var fruits = section.Locator(".shiny-vgrid-group-header", new() { HasTextString = "Fruits" });

        await Expect(section.Locator(".shiny-vgrid-group-header")).ToHaveCountAsync(3);
        // The page is the scroller: put the grid's top 200px above the top of the content region,
        // which is 200px into an ~18-item group.
        await section.Locator(".shiny-vgrid").First.EvaluateAsync(
            "g => { const c = document.querySelector('.shiny-applayout-content'); c.scrollBy(0, g.getBoundingClientRect().top - c.getBoundingClientRect().top + 200); }");

        // its header is still pinned at the top of the scroller
        var deadline = DateTime.UtcNow.AddSeconds(5);
        float delta = float.MaxValue;
        while (DateTime.UtcNow < deadline)
        {
            var box = await scroller.BoundingBoxAsync();
            var header = await fruits.BoundingBoxAsync();
            delta = Math.Abs(header!.Y - box!.Y);
            if (delta < 3)
                break;
            await Task.Delay(100);
        }
        await Assert.That(delta).IsLessThan(3f);
        await AssertNoConsoleErrors();
    }
}
