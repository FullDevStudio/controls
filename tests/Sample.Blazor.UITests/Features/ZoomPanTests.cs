namespace Sample.Blazor.UITests.Features;

/// <summary>
/// ZoomPanView: zooming the live control tree, and the content staying interactive at every scale.
/// </summary>
public class ZoomPanTests : BlazorTest
{
    ILocator State => Page.Locator("span.state").First;
    ILocator FirstView => Page.Locator(".shiny-zoompan").First;
    ILocator SecondView => Page.Locator(".shiny-zoompan").Nth(1);

    static Task<string> Transform(ILocator view)
        => view.Locator(".shiny-zoompan-surface").EvaluateAsync<string>("s => getComputedStyle(s).transform");

    [Test]
    public async Task Zoom_And_Reset_Report_Back()
    {
        await Open("/zoompan");
        await Expect(State).ToHaveTextAsync("1.00× — at rest");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Zoom to 2×" }).ClickAsync();
        await Expect(State).ToHaveTextAsync("2.00× — panning");
        await Assert.That(await Transform(FirstView)).IsNotEqualTo("none");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset", Exact = true }).ClickAsync();
        await Expect(State).ToHaveTextAsync("1.00× — at rest");
        await AssertNoConsoleErrors();
    }

    /// <summary>
    /// Zooms with Ctrl+wheel over <paramref name="target"/>, which keeps the point under the cursor
    /// fixed — so the control is still under the pointer afterwards, the way a user would zoom into it.
    /// </summary>
    async Task<(float X, float Y)> ZoomOnto(ILocator target)
    {
        await target.ScrollIntoViewIfNeededAsync();
        var box = await target.BoundingBoxAsync();
        var x = box!.X + box.Width / 2;
        var y = box.Y + box.Height / 2;
        await Page.Mouse.MoveAsync(x, y);
        await Page.Keyboard.DownAsync("Control");
        await Page.Mouse.WheelAsync(0, -400);
        await Page.Keyboard.UpAsync("Control");
        await Expect(State).ToContainTextAsync("panning");
        await Page.WaitForTimeoutAsync(400); // let the zoom animation settle
        return (x, y);
    }

    [Test]
    [SkipOnPhone("zooms with Ctrl+wheel; a phone pinches, and mobile WebKit has no wheel to synthesise")]
    public async Task Content_Button_Still_Clicks_While_Zoomed()
    {
        await Open("/zoompan");
        var inner = FirstView.GetByRole(AriaRole.Button, new() { NameRegex = new("^Clicked \\d+ times$") });
        var (x, y) = await ZoomOnto(inner);

        await Page.Mouse.ClickAsync(x, y);

        await Expect(inner).ToHaveTextAsync("Clicked 1 times");
        await Expect(State).ToContainTextAsync("panning");
        await AssertNoConsoleErrors();
    }

    [Test]
    [SkipOnPhone("zooms with Ctrl+wheel; a phone pinches, and mobile WebKit has no wheel to synthesise")]
    public async Task Content_Input_Takes_Text_While_Zoomed()
    {
        await Open("/zoompan");
        var input = FirstView.GetByPlaceholder("type in here");
        var (x, y) = await ZoomOnto(input);

        await Page.Mouse.ClickAsync(x, y);
        await Page.Keyboard.TypeAsync("hello");

        await Expect(FirstView.Locator("p", new() { HasText = "You typed:" })).ToHaveTextAsync("You typed: hello");
        await AssertNoConsoleErrors();
    }

    [Test]
    [SkipOnPhone("zooms with Ctrl+wheel; a phone pinches, and mobile WebKit has no wheel to synthesise")]
    public async Task Ctrl_Wheel_Zooms_In()
    {
        await Open("/zoompan");
        var box = await FirstView.BoundingBoxAsync();
        await Page.Mouse.MoveAsync(box!.X + box.Width / 2, box.Y + box.Height - 20);

        await Page.Keyboard.DownAsync("Control");
        await Page.Mouse.WheelAsync(0, -400);
        await Page.Keyboard.UpAsync("Control");

        await Expect(State).Not.ToHaveTextAsync("1.00× — at rest");
        await Expect(State).ToContainTextAsync("panning");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Double_Click_Toggles_Zoom_And_The_Switch_Turns_It_Off()
    {
        await Open("/zoompan");
        await SecondView.Locator("svg").DblClickAsync();
        await Page.WaitForFunctionAsync(
            "el => getComputedStyle(el.querySelector('.shiny-zoompan-surface')).transform !== 'none' && getComputedStyle(el.querySelector('.shiny-zoompan-surface')).transform !== 'matrix(1, 0, 0, 1, 0, 0)'",
            await SecondView.ElementHandleAsync());

        // IsZoomEnabled off drops the zoom already applied
        await Page.Locator("label", new() { HasText = "IsZoomEnabled" }).Locator("input").UncheckAsync();
        await Page.WaitForFunctionAsync(
            "el => { const t = getComputedStyle(el.querySelector('.shiny-zoompan-surface')).transform; return t === 'none' || t === 'matrix(1, 0, 0, 1, 0, 0)'; }",
            await SecondView.ElementHandleAsync());

        // and while it is off, a double click does nothing
        await SecondView.Locator("svg").DblClickAsync();
        await Page.WaitForTimeoutAsync(500);
        var t = await Transform(SecondView);
        await Assert.That(t is "none" or "matrix(1, 0, 0, 1, 0, 0)").IsTrue();
        await AssertNoConsoleErrors();
    }
}
