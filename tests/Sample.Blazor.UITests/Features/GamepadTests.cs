namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The on-screen GamepadView: pressing a face button reaches the game loop through GetState() and
/// raises ButtonChanged; presets and face styles rebuild the pad; misses pass through to the field.
/// </summary>
public class GamepadTests : BlazorTest
{
    ILocator Pad => Page.Locator(".shiny-gamepad");
    ILocator Hud => Page.Locator(".gp-hud");
    ILocator LastEvent => Page.Locator(".state code");

    ILocator Element(string id) => Pad.Locator($".shiny-gamepad__element[data-id='{id}']");

    async Task OpenPad()
    {
        await Open("gamepad");
        await Expect(Element("a")).ToBeVisibleAsync();
    }

    async Task<(float X, float Y)> CenterOf(ILocator locator)
    {
        var box = (await locator.BoundingBoxAsync())!;
        return (box.X + box.Width / 2, box.Y + box.Height / 2);
    }


    [Test]
    public async Task Holding_A_Face_Button_Reaches_The_Game_Loop()
    {
        await OpenPad();
        var (x, y) = await CenterOf(Element("a").Locator(".shiny-gamepad__shape"));

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();

        await Expect(Element("a")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-pressed"));
        await Expect(Hud.Locator("code").First).ToHaveTextAsync("A");
        await Expect(LastEvent).ToHaveTextAsync("A pressed");
        // Holding A grows the player.
        await Expect(Page.Locator(".gp-player")).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex("width:48px"));

        await Page.Mouse.UpAsync();

        await Expect(LastEvent).ToHaveTextAsync("A released");
        await Expect(Hud.Locator("code").First).ToHaveTextAsync("-");
        await Expect(Element("a")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-pressed"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Dragging_The_Left_Stick_Moves_The_Player()
    {
        await OpenPad();
        var player = Page.Locator(".gp-player");
        var before = await player.GetAttributeAsync("style");
        var (x, y) = await CenterOf(Element("ls").Locator(".shiny-gamepad__shape"));

        await Page.Mouse.MoveAsync(x, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync(x + 60, y, new() { Steps = 6 });

        // The stick reports a deflection to the right while it is held...
        await Expect(Hud.Locator("code").Nth(1)).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^0\.[1-9]\d*, |^1\.00, "));
        // ...and the loop integrates it into the player's position.
        await Expect(player).Not.ToHaveAttributeAsync("style", before!);

        await Page.Mouse.UpAsync();
        await Expect(Hud.Locator("code").Nth(1)).ToHaveTextAsync("0.00, 0.00");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Preset_Rebuilds_The_Pad()
    {
        await OpenPad();
        await Expect(Element("ls")).ToHaveCountAsync(1);
        await Expect(Element("lt")).ToHaveCountAsync(1);

        await Page.Locator(".gp-options select").Nth(0).SelectOptionAsync("Nes");

        // The NES pad: d-pad, B and A, Select and Start — no sticks, no shoulders.
        await Expect(Element("ls")).ToHaveCountAsync(0);
        await Expect(Element("lt")).ToHaveCountAsync(0);
        await Expect(Element("dpad")).ToHaveCountAsync(1);
        await Expect(Element("a")).ToHaveCountAsync(1);
        await Expect(Element("b")).ToHaveCountAsync(1);
        await Expect(Element("x")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Face_Style_Relabels_The_Buttons()
    {
        await OpenPad();
        await Expect(Element("a").Locator("text")).ToHaveTextAsync("A");

        await Page.Locator(".gp-options select").Nth(1).SelectOptionAsync("Nintendo");

        // Nintendo swaps the A/B positions' letters relative to Xbox: the bottom button reads B.
        await Expect(Element("a").Locator("text")).ToHaveTextAsync("B");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Misses_Pass_Through_To_The_Playfield()
    {
        await OpenPad();
        var taps = Hud.Locator("div", new() { HasText = "Field taps" }).Locator("code");
        await Expect(taps).ToHaveTextAsync("0");

        // The middle of the stage, well clear of every control.
        var stage = (await Page.Locator(".gp-stage").BoundingBoxAsync())!;
        await Page.Mouse.ClickAsync(stage.X + stage.Width / 2, stage.Y + stage.Height * 0.3f);

        await Expect(taps).ToHaveTextAsync("1");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Option_Toggles_Reach_The_View()
    {
        await OpenPad();

        await Page.GetByLabel("Fade when idle").CheckAsync();
        await Expect(Pad).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex(@"--shiny-gamepad-idle-opacity:0\.35"));

        await Page.GetByLabel("Edit layout (drag / pinch)").CheckAsync();
        await Expect(Pad).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-editing"));

        await Page.GetByLabel("Inline controller").CheckAsync();
        await Expect(Page.Locator(".gp-stage")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-inline"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task The_Pad_Is_Listed_As_A_Virtual_Gamepad()
    {
        await OpenPad();

        await Expect(Page.Locator(".demo-section li").First).ToContainTextAsync("Virtual On-Screen Gamepad");
        await AssertNoConsoleErrors();
    }
}
