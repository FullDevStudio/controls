namespace Sample.Blazor.UITests.Features;

/// <summary>
/// CameraView without a camera: headless Chromium has no capture device, so these cover what the page
/// does around the stream — the no-camera state, the live CSS/SVG filter chain, and the detector options.
/// </summary>
public class CameraTests : BlazorTest
{
    ILocator Video => Page.Locator(".shiny-camera-video");
    ILocator Status => Page.Locator(".state code");

    ILocator SelectFor(string label) => Page.Locator("label", new() { HasText = label }).Locator("select");

    [Test]
    public async Task No_Camera_Reports_An_Error_Instead_Of_Hanging()
    {
        await Open("/camera");

        // "Starting…" until getUserMedia answers; with no device that answer is an error message
        await Expect(Status).Not.ToHaveTextAsync("Starting…", new() { Timeout = 15_000 });
        await Expect(Status).Not.ToBeEmptyAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Filter_Is_Applied_To_The_Preview_As_Css()
    {
        await Open("/camera");

        await SelectFor("Filter:").SelectOptionAsync("Sepia");
        await Expect(Video).ToHaveCSSAsync("filter", new System.Text.RegularExpressions.Regex("sepia"));

        await SelectFor("Filter:").SelectOptionAsync("None");
        await Expect(Video).ToHaveCSSAsync("filter", "none");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Spatial_Effect_Is_An_Svg_Filter_Reference()
    {
        await Open("/camera");

        await SelectFor("Effect:").SelectOptionAsync("Comic");

        await Expect(Video).ToHaveCSSAsync("filter", new System.Text.RegularExpressions.Regex(@"url\("));
        var ids = await Video.EvaluateAsync<string>("v => getComputedStyle(v).filter");
        var id = System.Text.RegularExpressions.Regex.Match(ids, "#([^\"')]+)").Groups[1].Value;
        await Assert.That(id).IsNotEmpty();
        await Assert.That(await Page.Locator($"filter#{id}").CountAsync()).IsEqualTo(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Document_Detector_Disables_Scan_Area()
    {
        await Open("/camera");
        var scanArea = Page.Locator("label", new() { HasText = "Restrict scan area" }).Locator("input");
        await Expect(scanArea).ToBeEnabledAsync();

        await Page.Locator("label", new() { HasText = "AI Document" }).Locator("input").CheckAsync();
        await Expect(scanArea).ToBeDisabledAsync();

        await Page.Locator("label", new() { HasText = "Barcode / QR" }).Locator("input").CheckAsync();
        await Expect(scanArea).ToBeEnabledAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Start_Stop_Button_Toggles()
    {
        await Open("/camera");
        var toggle = Page.Locator(".demo-section button", new() { HasTextRegex = new("^(Start|Stop)$") });
        await Expect(toggle).ToHaveTextAsync("Stop");

        await toggle.ClickAsync();
        await Expect(toggle).ToHaveTextAsync("Start");
        await AssertNoConsoleErrors();
    }
}
