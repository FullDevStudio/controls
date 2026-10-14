using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>Tooltip on <c>/tooltip</c>: hover/focus/click triggers, placement, and the state-bound hint.</summary>
public class TooltipTests : BlazorTest
{
    static readonly Regex IsOpen = new(@"\bshiny-tooltip--open\b");

    ILocator Bubble(string text) => Page.Locator(".shiny-tooltip").Filter(new() { HasTextString = text });

    ILocator ShinyButton(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    [Test]
    public async Task Hover_Opens_And_Leaving_Closes()
    {
        await Open("tooltip");
        var bubble = Bubble("Applies the change without closing the sheet.");
        await Expect(bubble).Not.ToHaveClassAsync(IsOpen);

        await ShinyButton("Apply").HoverAsync();
        await Expect(bubble).ToHaveClassAsync(IsOpen);
        await Expect(bubble).ToBeVisibleAsync();

        await Page.Mouse.MoveAsync(5, 5);
        await Expect(bubble).Not.ToHaveClassAsync(IsOpen);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Keyboard_Focus_Opens_The_Tooltip()
    {
        await Open("tooltip");
        var bubble = Bubble("Applies the change without closing the sheet.");

        await ShinyButton("Apply").FocusAsync();
        await Expect(bubble).ToHaveClassAsync(IsOpen);
        await AssertNoConsoleErrors();
    }

    [Test]
    [Arguments("Top", "Above", "top")]
    [Arguments("Bottom", "Below", "bottom")]
    [Arguments("Left", "To the left", "left")]
    [Arguments("Right", "To the right", "right")]
    public async Task Bubble_Sits_On_The_Requested_Side(string button, string text, string side)
    {
        await Open("tooltip");
        var target = ShinyButton(button);
        await target.HoverAsync();

        var bubble = Bubble(text);
        await Expect(bubble).ToHaveClassAsync(IsOpen);
        await Expect(bubble).ToHaveClassAsync(new Regex($@"\bshiny-tooltip--{side}\b"));

        var t = (await target.BoundingBoxAsync())!;
        var b = (await bubble.BoundingBoxAsync())!;
        var ok = side switch
        {
            "top" => b.Y + b.Height <= t.Y + 1,
            "bottom" => b.Y >= t.Y + t.Height - 1,
            "left" => b.X + b.Width <= t.X + 1,
            _ => b.X >= t.X + t.Width - 1
        };
        await Assert.That(ok).IsTrue().Because($"bubble {b.X},{b.Y} {b.Width}x{b.Height} vs target {t.X},{t.Y} {t.Width}x{t.Height}");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Bound_Hint_Toggles_From_State_And_Click_Raises_Clicked()
    {
        await Open("tooltip");
        var hint = Bubble("Why is this disabled?");
        await Expect(hint).Not.ToHaveClassAsync(IsOpen);

        await ShinyButton("Toggle the hint").ClickAsync();
        await Expect(hint).ToHaveClassAsync(IsOpen);

        await hint.ClickAsync();
        await Expect(hint).Not.ToHaveClassAsync(IsOpen);
        await Expect(Page.Locator("em", new() { HasTextString = "bound tooltip was clicked" })).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Click_Trigger_Opens_On_Click_And_Carries_Custom_Colours()
    {
        await Open("tooltip");
        var styled = Bubble("Heads up");

        await ShinyButton("Custom colours").HoverAsync();
        await Page.WaitForTimeoutAsync(400);
        await Expect(styled).Not.ToHaveClassAsync(IsOpen);

        await ShinyButton("Custom colours").ClickAsync();
        await Expect(styled).ToHaveClassAsync(IsOpen);
        await Expect(styled.Locator(".shiny-tooltip__card")).ToHaveCSSAsync("background-color", "rgb(15, 118, 110)"); // #0F766E
        await AssertNoConsoleErrors();
    }
}
