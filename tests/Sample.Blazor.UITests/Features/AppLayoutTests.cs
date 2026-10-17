using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>/applayout — the demo AppLayout (the gallery shell around it is an AppLayout too, so everything is scoped to the page's section).</summary>
// The panels persist to the browser context's shared localStorage, so these tests must not reset it under each other.
[NotInParallel(nameof(AppLayoutTests))]
public class AppLayoutTests : BlazorTest
{
    ILocator Demo => Page.Locator("section.demo-section .shiny-applayout");
    ILocator LeftPanel => Demo.Locator(".shiny-applayout-panel--left");
    ILocator RightPanel => Demo.Locator(".shiny-applayout-panel--right");
    ILocator Controls => Page.Locator("section.demo-section > div").First;
    ILocator LeftButton => Controls.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Left: ") });
    ILocator RightButton => Controls.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Right: ") });
    ILocator Widths => Controls.Locator("span").Last;

    /// <summary>
    /// The panels persist to localStorage, which the shared browser context keeps between tests; start
    /// every test from the page's own defaults (once per tab, so a reload inside a test still restores).
    /// </summary>
    async Task OpenFresh()
    {
        await Page.AddInitScriptAsync(
            """
            if (!window.name.includes('applayout-fresh')) {
                localStorage.removeItem('shiny.applayout.sample-left');
                localStorage.removeItem('shiny.applayout.sample-right');
                window.name += 'applayout-fresh';
            }
            """
        );
        await Open("applayout");
    }

    static readonly Regex Shown = new(@"\bis-shown\b");
    static readonly Regex Toolbar = new(@"\bis-toolbar\b");


    [Test]
    [SkipOnPhone("the demo's panels collapse below 900px by design, so there is no resizer or toolbar state to cycle")]
    public async Task Left_Button_Cycles_Shown_Hidden_Toolbar()
    {
        await OpenFresh();
        await Expect(LeftButton).ToHaveTextAsync("Left: Shown");
        await Expect(LeftPanel).ToHaveClassAsync(Shown);
        await Expect(LeftPanel.GetByText("Explorer")).ToBeVisibleAsync();

        await LeftButton.ClickAsync();
        await Expect(LeftButton).ToHaveTextAsync("Left: Hidden");
        await Expect(LeftPanel).ToHaveAttributeAsync("aria-hidden", "true");
        await Expect(LeftPanel.GetByText("Explorer")).Not.ToBeVisibleAsync();

        await LeftButton.ClickAsync();
        await Expect(LeftButton).ToHaveTextAsync("Left: Toolbar");
        await Expect(LeftPanel).ToHaveClassAsync(Toolbar);
        await Expect(LeftPanel.GetByTitle("Search")).ToBeVisibleAsync();      // the rail
        await Expect(LeftPanel).ToHaveAttributeAsync("style", new Regex(@"width:52px"));

        await LeftButton.ClickAsync();
        await Expect(LeftButton).ToHaveTextAsync("Left: Shown");
        await Expect(LeftPanel).ToHaveAttributeAsync("style", new Regex(@"width:240px"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Header_Button_Drives_The_Same_State()
    {
        await OpenFresh();
        await Expect(RightButton).ToHaveTextAsync("Right: Toolbar");
        await Expect(RightPanel).ToHaveClassAsync(Toolbar);

        await Demo.GetByTitle("Toggle the right panel").ClickAsync();
        await Expect(RightButton).ToHaveTextAsync("Right: Shown");
        await Expect(RightPanel).ToHaveClassAsync(Shown);
        await Expect(RightPanel.GetByText("Property 1", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(RightPanel).ToHaveAttributeAsync("style", new Regex(@"width:280px"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Header_Span_Content_Moves_The_Header_Between_The_Panels()
    {
        await OpenFresh();
        await Expect(Demo).ToHaveAttributeAsync("style", new Regex(@"'header header header'"));

        await Controls.Locator("select").SelectOptionAsync("Content");
        await Expect(Demo).ToHaveAttributeAsync("style", new Regex(@"'left header right'"));

        var header = await Demo.Locator(".shiny-applayout-header").BoundingBoxAsync();
        var left = await LeftPanel.BoundingBoxAsync();
        await Assert.That(header!.X).IsGreaterThanOrEqualTo(left!.X + left.Width - 1);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Borders_Checkbox_Turns_The_Dividers_Off()
    {
        await OpenFresh();
        await Expect(Demo).ToHaveAttributeAsync("style", new Regex(@"--shiny-layout-border-width:1px"));

        await Controls.Locator("input[type=checkbox]").UncheckAsync();
        await Expect(Demo).ToHaveAttributeAsync("style", new Regex(@"--shiny-layout-border-width:0(px)?;"));
        await AssertNoConsoleErrors();
    }


    [Test]
    [SkipOnPhone("the demo's panels collapse below 900px by design, so there is no resizer or toolbar state to cycle")]
    public async Task Dragging_The_Resizer_Changes_The_Width_And_Persists()
    {
        await OpenFresh();
        await Expect(Widths).ToContainTextAsync("left 240px");

        var resizer = LeftPanel.Locator(".shiny-applayout-panel__resizer");
        var box = (await resizer.BoundingBoxAsync())!;
        var x = box.X + box.Width / 2f;
        var y = box.Y + box.Height / 2f;

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= 6; i++)
            await Page.Mouse.MoveAsync(x + i * 10, y);
        await Page.Mouse.UpAsync();

        await Expect(Widths).ToContainTextAsync("left 300px");
        await Expect(LeftPanel).ToHaveAttributeAsync("style", new Regex(@"width:300px"));

        // PersistKey brings the width back after a reload.
        await Page.ReloadAsync();
        await Expect(LeftPanel).ToHaveAttributeAsync("style", new Regex(@"width:300px"), new() { Timeout = 15_000 });
        await AssertNoConsoleErrors();
    }


    [Test]
    [SkipOnPhone("the demo's panels collapse below 900px by design, so there is no resizer or toolbar state to cycle")]
    public async Task Resize_Is_Clamped_To_MaxSize()
    {
        await OpenFresh();
        var resizer = LeftPanel.Locator(".shiny-applayout-panel__resizer");
        var box = (await resizer.BoundingBoxAsync())!;
        var x = box.X + box.Width / 2f;
        var y = box.Y + box.Height / 2f;

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= 10; i++)
            await Page.Mouse.MoveAsync(x + i * 40, y);
        await Page.Mouse.UpAsync();

        await Expect(Widths).ToContainTextAsync("left 420px");               // MaxSize="420"
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Narrow_Window_Compacts_The_Left_Panel_To_Its_Rail()
    {
        await Page.SetViewportSizeAsync(1400, 900);
        await OpenFresh();
        await Expect(LeftPanel).ToHaveClassAsync(Shown);

        // CollapseBelow="900" measures the layout itself, which is narrower than the window.
        await Page.SetViewportSizeAsync(700, 900);
        await Expect(LeftPanel).Not.ToHaveClassAsync(Shown);
        await AssertNoConsoleErrors();
    }
}
