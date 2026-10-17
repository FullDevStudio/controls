namespace Sample.Blazor.UITests.Features;

/// <summary>/coverflow: CoverFlowView driven with real mouse, wheel and keyboard input.</summary>
public class CoverFlowTests : BlazorTest
{
    ILocator Flow => Page.Locator(".shiny-coverflow").First;
    ILocator Position => Page.Locator(".cf-controls strong").First;
    ILocator Selected => Page.Locator(".cf-selected strong");
    ILocator Caption => Flow.Locator(".shiny-coverflow-caption .cf-caption-title");

    async Task OpenPage()
    {
        await Open("coverflow");
        await Expect(Flow).ToBeVisibleAsync();
        // the engine shows the slides once it has positioned them
        await Expect(Flow.Locator(".shiny-coverflow-slide").First).ToBeVisibleAsync();
        await Expect(Position).ToHaveTextAsync("4");
        await Expect(Caption).ToHaveTextAsync("Summit");
    }

    async Task<(float X, float Y)> CoverBand()
    {
        var box = (await Flow.Locator(".shiny-coverflow-stage").BoundingBoxAsync())!;
        return (box.X + box.Width / 2, box.Y + box.Height * 0.35f);
    }

    async Task<int> CurrentPosition() => Int32.Parse(await Position.InnerTextAsync());


    [Test]
    public async Task Renders_Only_A_Window_Of_Covers()
    {
        await OpenPage();

        // VisibleItems (4) each side plus a small buffer - never all twelve albums
        var count = await Flow.Locator(".shiny-coverflow-slide").CountAsync();
        await Assert.That(count).IsGreaterThanOrEqualTo(9);
        await Assert.That(count).IsLessThan(12);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Dragging_Left_Moves_Forward()
    {
        await OpenPage();
        var (x, y) = await CoverBand();

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= 13; i++)
        {
            await Page.Mouse.MoveAsync(x - i * 20, y);
            await Page.WaitForTimeoutAsync(16);
        }
        await Page.WaitForTimeoutAsync(150); // hold, so it settles by distance rather than flick
        await Page.Mouse.UpAsync();

        // 260px at 130px per cover
        await Expect(Position).ToHaveTextAsync("6");
        await Expect(Caption).ToHaveTextAsync("Ember");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task A_Flick_Carries_On_Past_Where_The_Pointer_Stopped()
    {
        await OpenPage();
        var (x, y) = await CoverBand();

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= 5; i++)
            await Page.Mouse.MoveAsync(x + i * 30, y);
        await Page.Mouse.UpAsync();

        // 150px is just over one cover; the momentum takes it further back than that
        await Page.WaitForTimeoutAsync(1500);
        await Assert.That(await CurrentPosition()).IsLessThan(3);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Clicking_A_Side_Cover_Centres_It_And_Clicking_The_Centre_Selects_It()
    {
        await OpenPage();
        var (x, y) = await CoverBand();

        await Page.Mouse.ClickAsync(x + 130, y); // the first cover right of centre
        await Expect(Position).ToHaveTextAsync("5");
        await Expect(Caption).ToHaveTextAsync("City Lights");
        await Expect(Selected).ToHaveTextAsync("— click the centre album —");

        await Page.Mouse.ClickAsync(x, y);
        await Expect(Selected).ToHaveTextAsync("City Lights");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task A_Drag_Is_Not_A_Click()
    {
        await OpenPage();
        var (x, y) = await CoverBand();

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x - 140, y, new() { Steps = 8 });
        await Page.WaitForTimeoutAsync(150);
        await Page.Mouse.UpAsync();

        await Expect(Position).ToHaveTextAsync("5");
        await Page.WaitForTimeoutAsync(300);
        await Expect(Selected).ToHaveTextAsync("— click the centre album —");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Keyboard_Moves_And_Selects()
    {
        await OpenPage();
        await Flow.FocusAsync();

        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Position).ToHaveTextAsync("5");
        await Page.Keyboard.PressAsync("ArrowLeft");
        await Expect(Position).ToHaveTextAsync("4");
        await Page.Keyboard.PressAsync("End");
        await Expect(Position).ToHaveTextAsync("11");
        await Page.Keyboard.PressAsync("Home");
        await Expect(Position).ToHaveTextAsync("0");

        await Page.Keyboard.PressAsync("Enter");
        await Expect(Selected).ToHaveTextAsync("Midnight Drive");
        await AssertNoConsoleErrors();
    }


    [Test]
    [SkipOnPhone("a wheel gesture; drag and keyboard are covered by the other tests")]
    public async Task Horizontal_Wheel_Scrolls()
    {
        await OpenPage();
        var (x, y) = await CoverBand();

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.WheelAsync(300, 0);

        await Page.WaitForTimeoutAsync(1200);
        await Assert.That(await CurrentPosition()).IsGreaterThan(4);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Bound_Position_Drives_The_Control()
    {
        await OpenPage();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Last" }).ClickAsync();
        await Expect(Caption).ToHaveTextAsync("Harvest");
        await Expect(Flow.Locator(".shiny-coverflow-slide[data-index='11']")).ToBeVisibleAsync();

        await Page.GetByRole(AriaRole.Button, new() { Name = "‹ Previous" }).ClickAsync();
        await Expect(Caption).ToHaveTextAsync("Orbit");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Loop_Wraps_From_The_Last_Cover_To_The_First()
    {
        await OpenPage();
        await Page.GetByLabel("Loop").CheckAsync();
        await Page.GetByRole(AriaRole.Button, new() { Name = "Last" }).ClickAsync();
        await Expect(Position).ToHaveTextAsync("11");

        await Flow.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowRight");
        await Expect(Position).ToHaveTextAsync("0");
        await Expect(Caption).ToHaveTextAsync("Midnight Drive");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Reflection_Toggle_Adds_And_Removes_Reflections()
    {
        await OpenPage();
        await Expect(Flow.Locator(".shiny-coverflow-reflection").First).ToBeAttachedAsync();

        await Page.GetByLabel("Reflection").UncheckAsync();
        await Expect(Flow.Locator(".shiny-coverflow-reflection")).ToHaveCountAsync(0);

        await Page.GetByLabel("Reflection").CheckAsync();
        await Expect(Flow.Locator(".shiny-coverflow-reflection").First).ToBeAttachedAsync();
        await AssertNoConsoleErrors();
    }
}
