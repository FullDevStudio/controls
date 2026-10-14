namespace Sample.Blazor.UITests.Features;

/// <summary>
/// MediaElement draws its own transport bar, so each piece toggles independently, and the page's
/// options map straight onto the underlying &lt;video&gt;. Nothing here waits on the remote media itself.
/// </summary>
public class MediaElementTests : BlazorTest
{
    ILocator Player => Page.Locator(".shiny-media").First;
    ILocator PlayerVideo => Player.Locator("video.shiny-media-video");

    ILocator Option(string label) => Page.Locator("label.demo-check", new() { HasText = label }).Locator("input");

    [Test]
    public async Task Each_Transport_Piece_Toggles_On_Its_Own()
    {
        await Open("/mediaelement");
        await Expect(Player.Locator(".shiny-media-seek")).ToHaveCountAsync(1);
        await Expect(Player.Locator(".shiny-media-time")).ToHaveCountAsync(2);

        await Option("Seek bar").UncheckAsync();
        await Expect(Player.Locator(".shiny-media-seek")).ToHaveCountAsync(0);
        // the rest of the bar stays
        await Expect(Player.Locator(".shiny-media-time")).ToHaveCountAsync(2);

        await Option("Time labels").UncheckAsync();
        await Expect(Player.Locator(".shiny-media-time")).ToHaveCountAsync(0);

        await Option("Seek bar").CheckAsync();
        await Expect(Player.Locator(".shiny-media-seek")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Transport_Bar_Can_Be_Hidden_Entirely()
    {
        await Open("/mediaelement");
        await Expect(Player.Locator(".shiny-media-bar")).ToHaveCountAsync(1);

        await Option("Transport bar").UncheckAsync();
        await Expect(Player.Locator(".shiny-media-bar")).ToHaveCountAsync(0);

        await Option("Transport bar").CheckAsync();
        await Expect(Player.Locator(".shiny-media-bar")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Mute_Method_Mutes_The_Video_And_Flips_The_Bar_Button()
    {
        await Open("/mediaelement");
        await Expect(Player.GetByRole(AriaRole.Button, new() { Name = "Mute", Exact = true })).ToBeVisibleAsync();

        await Page.Locator(".demo-buttons button", new() { HasTextRegex = new("^Mute$") }).ClickAsync();

        await Expect(Player.GetByRole(AriaRole.Button, new() { Name = "Unmute", Exact = true })).ToBeVisibleAsync();
        await Assert.That(await PlayerVideo.EvaluateAsync<bool>("v => v.muted")).IsTrue();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Speed_And_Loop_Reach_The_Video()
    {
        await Open("/mediaelement");

        await Page.Locator("label.demo-check", new() { HasText = "Speed" }).Locator("select").SelectOptionAsync("2");
        await Page.WaitForFunctionAsync("() => document.querySelector('.shiny-media video').playbackRate === 2");

        await Option("Loop").CheckAsync();
        await Page.WaitForFunctionAsync("() => document.querySelector('.shiny-media video').loop === true");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Volume_Slider_Hides_With_Its_Option()
    {
        await Open("/mediaelement");
        await Expect(Player.Locator(".shiny-media-volume")).ToHaveCountAsync(1);

        await Option("Volume control").UncheckAsync();

        await Expect(Player.Locator(".shiny-media-volume")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }
}
