namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The full-screen ImageViewer: opened from a thumbnail, closed by its button or the backdrop, with
/// Opened/Closed raised once each way.
/// </summary>
public class ImageViewerTests : BlazorTest
{
    ILocator Viewer => Page.Locator(".shiny-imgviewer");
    ILocator LastEvent => Page.Locator("p em", new() { HasText = "Last viewer event" });

    async Task OpenFirstThumbnail()
    {
        await Page.Locator("img[alt=thumb]").First.ClickAsync();
        await Expect(Viewer).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-open"));
    }

    [Test]
    public async Task Thumbnail_Opens_The_Viewer_On_That_Image()
    {
        await Open("/imageviewer");
        await Expect(LastEvent).ToHaveTextAsync("Last viewer event: none yet");

        await OpenFirstThumbnail();

        await Expect(Viewer.Locator(".shiny-imgviewer-img")).ToHaveAttributeAsync("src", "https://picsum.photos/seed/blazor1/1200/800");
        await Expect(Viewer.Locator(".shiny-imgviewer-header")).ToContainTextAsync("blazor1");
        await Expect(LastEvent).ToHaveTextAsync("Last viewer event: Opened");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Close_Button_Closes_And_Raises_Closed()
    {
        await Open("/imageviewer");
        await OpenFirstThumbnail();

        var close = Viewer.GetByRole(AriaRole.Button, new() { Name = "Close" });
        await Expect(close).ToHaveTextAsync("Done");
        await close.ClickAsync();

        await Expect(Viewer).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-open"));
        await Expect(LastEvent).ToHaveTextAsync("Last viewer event: Closed");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Backdrop_Click_Closes()
    {
        await Open("/imageviewer");
        await OpenFirstThumbnail();

        // a corner of the backdrop, clear of the picture, header and close button
        var box = await Viewer.Locator(".shiny-imgviewer-backdrop").BoundingBoxAsync();
        await Page.Mouse.ClickAsync(box!.X + 8, box.Y + box.Height - 8);

        await Expect(Viewer).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-open"));
        await Expect(LastEvent).ToHaveTextAsync("Last viewer event: Closed");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Reopening_Shows_The_Newly_Chosen_Image()
    {
        await Open("/imageviewer");
        await OpenFirstThumbnail();
        await Viewer.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await Expect(Viewer).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-open"));

        await Page.Locator("img[alt=thumb]").Nth(2).ClickAsync();

        await Expect(Viewer).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-open"));
        await Expect(Viewer.Locator(".shiny-imgviewer-img")).ToHaveAttributeAsync("src", "https://picsum.photos/seed/blazor3/1200/800");
        await Expect(LastEvent).ToHaveTextAsync("Last viewer event: Opened");
        await AssertNoConsoleErrors();
    }
}
