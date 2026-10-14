namespace Sample.UITests.Features;

/// <summary>
/// ShinyImage's load state for sources that do not depend on the network succeeding: embedded and
/// inline SVG, a URL that can only fail, and the image service's cache commands.
/// </summary>
public class ShinyImageTests : UITest
{
    Task OpenPage() => Open("shinyimage", "ImagesPage");


    [Test]
    public async Task Embedded_And_Inline_Svg_Load()
    {
        await OpenPage();

        // resource:// and data: never touch the network
        await Driver.WaitForProperty("EmbeddedSvgSmall", "State", "Loaded", timeoutSeconds: 20);
        await Driver.WaitForProperty("EmbeddedSvgLarge", "State", "Loaded", timeoutSeconds: 20);
        await Driver.WaitForProperty("InlineSvg", "State", "Loaded", timeoutSeconds: 20);
        await Driver.WaitForProperty("InlineSvg", "IsLoading", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Broken_Url_Ends_Failed_With_An_Error()
    {
        await OpenPage();

        // a 404 online and no route offline both end here
        await Driver.WaitForProperty("BrokenImage", "State", "Failed", timeoutSeconds: 30);
        await Driver.WaitForProperty("BrokenImage", "IsLoading", "False");
        await Assert.That(await Driver.GetProperty("LoadError", automationId: "BrokenImage")).IsNotNull();
    }


    [Test]
    public async Task Missing_Embedded_Resource_Fails()
    {
        await OpenPage();
        await Driver.WaitForProperty("EmbeddedSvgMedium", "State", "Loaded", timeoutSeconds: 20);

        try
        {
            await Driver.SetProperty("Uri", "resource://Sample.Assets.does-not-exist.svg", automationId: "EmbeddedSvgMedium");
            await Driver.WaitForProperty("EmbeddedSvgMedium", "State", "Failed", timeoutSeconds: 20);
        }
        finally
        {
            // Shell keeps the page alive, so put the real artwork back for the next visit
            await Driver.SetProperty("Uri", "resource://Sample.Assets.embedded-icon.svg", automationId: "EmbeddedSvgMedium");
        }
        await Driver.WaitForProperty("EmbeddedSvgMedium", "State", "Loaded", timeoutSeconds: 20);
    }


    [Test]
    public async Task Refresh_Reports_The_Cache_Size()
    {
        await OpenPage();

        await Driver.Tap(automationId: "RefreshCacheButton");

        await Driver.WaitUntil(async () =>
            await Driver.GetProperty("Text", automationId: "CacheSizeLabel") is { } text
                && (text.EndsWith(" KB") || text.EndsWith(" MB")),
            "the cache size to be reported in KB or MB");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Clear_Cache_Reloads_The_Large_Image_From_A_New_Url()
    {
        await OpenPage();
        var before = await Driver.GetProperty("Uri", automationId: "LargeImage");

        await Driver.Tap(automationId: "ClearCacheButton");

        await Driver.WaitUntil(async () =>
            await Driver.GetProperty("Uri", automationId: "LargeImage") is { } uri && uri != before && uri.Contains("?cleared="),
            "the large image to be re-pointed past the cache");
        await Driver.WaitUntil(async () =>
            (await Driver.GetProperty("Text", automationId: "CacheSizeLabel"))?.EndsWith("B") == true,
            "the cleared cache size to be reported");
    }
}
