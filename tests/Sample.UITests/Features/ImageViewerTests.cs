namespace Sample.UITests.Features;

/// <summary>
/// ImageViewer opening and closing - through IsOpen, its close chip and a footer command - with
/// OpenedCommand/ClosedCommand reported on the page, plus the gallery's per-cell viewers.
/// </summary>
public class ImageViewerTests : UITest
{
    /// <summary>Shell keeps the page alive between visits, so the viewer is closed on the way in.</summary>
    async Task OpenPage()
    {
        await Open("imageviewer", "ImageViewerPage");
        await Driver.SetProperty("IsOpen", "False", automationId: "LocalImageViewer");
        await Driver.WaitForProperty("LocalImageViewer", "IsOpen", "False");
    }

    async Task OpenLocalViewer()
    {
        await Driver.SetProperty("IsOpen", "True", automationId: "LocalImageViewer");
        await Driver.WaitForProperty("LocalImageViewer", "IsOpen", "True");
        await Driver.WaitForProperty("ViewerEventLabel", "Text", "Last viewer event: Opened");
    }


    [Test]
    public async Task IsOpen_Raises_Opened_And_Closed()
    {
        await OpenPage();

        await OpenLocalViewer();

        await Driver.SetProperty("IsOpen", "False", automationId: "LocalImageViewer");
        await Driver.WaitForProperty("ViewerEventLabel", "Text", "Last viewer event: Closed");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Close_Chip_Uses_CloseButtonText_And_Closes()
    {
        await OpenPage();
        await OpenLocalViewer();

        // CloseButtonText="Done" on the sample: the chip is a plain Button with that text
        await Driver.Tap(elementId: (await FindButton("Done")).Id);

        await Driver.WaitForProperty("LocalImageViewer", "IsOpen", "False");
        await Driver.WaitForProperty("ViewerEventLabel", "Text", "Last viewer event: Closed");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Footer_Command_Closes_Through_The_Binding()
    {
        await OpenPage();
        await OpenLocalViewer();

        // the footer's Close runs the view model's CloseViewerCommand, which writes IsViewerOpen back
        await Driver.Tap(automationId: "ViewerFooterCloseButton");

        await Driver.WaitForProperty("LocalImageViewer", "IsOpen", "False");
        await Driver.WaitForProperty("ViewerEventLabel", "Text", "Last viewer event: Closed");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Broken_Url_Ends_Failed()
    {
        await OpenPage();

        // a 404 online and no route offline both end here
        await Driver.WaitForProperty("BrokenImageViewer", "State", "Failed", timeoutSeconds: 30);
        await Driver.WaitForProperty("BrokenImageViewer", "IsLoading", "False");
    }


    [Test]
    public async Task Gallery_Cell_Opens_And_Its_Close_Chip_Closes()
    {
        await Open("imagegallery", "ImageGalleryPage");
        await Driver.WaitUntilTextExists("Harbour");

        // element ids are regenerated per capture, so every step looks the first viewer up afresh
        await Driver.SetProperty("IsOpen", "True", elementId: (await Driver.FindByType("ImageViewer")).Id);
        await Driver.WaitUntil(async () => await FirstViewerIsOpen() == "True", "the first gallery viewer to open");

        // the default chip is a ✕ Button
        await Driver.Tap(elementId: (await FindButton("✕")).Id);

        await Driver.WaitUntil(async () => await FirstViewerIsOpen() == "False", "the gallery viewer to close");
        await AssertNoAppErrors();
    }


    async Task<string?> FirstViewerIsOpen()
        => await Driver.GetProperty("IsOpen", elementId: (await Driver.FindByType("ImageViewer")).Id);


    /// <summary>The viewer's close chip is injected into the overlay host only once it opens.</summary>
    async Task<Microsoft.Maui.DevFlow.Driver.ElementInfo> FindButton(string text)
    {
        Microsoft.Maui.DevFlow.Driver.ElementInfo? found = null;
        await Driver.WaitUntil(async () =>
        {
            found = (await Driver.Query(type: "Button", text: text)).FirstOrDefault();
            return found != null;
        }, $"a '{text}' button", timeoutSeconds: 10);
        return found!;
    }
}
