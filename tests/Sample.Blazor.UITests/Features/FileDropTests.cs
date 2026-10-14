namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The window-level file drop service, driven with real DragEvents carrying a DataTransfer of File
/// objects — the same events an OS drag delivers to the page.
/// </summary>
public class FileDropTests : BlazorTest
{
    ILocator Status => Page.Locator("p.status-message");
    ILocator Overlay => Page.Locator(".drop-overlay");
    ILocator DroppedFiles => Page.Locator("section.demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Dropped files" }) }).Locator("li");
    ILocator Events => Page.Locator("section.demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Events" }) }).Locator("li");

    ILocator Option(string label) => Page.Locator("label", new() { HasText = label }).Locator("input");

    /// <summary>Dispatches a drag event on the window carrying one in-memory file.</summary>
    Task Drag(string type, string name = "notes.txt", string mime = "text/plain", string body = "hello shiny")
        => Page.EvaluateAsync(
            """
            ([type, name, mime, body]) => {
                window.__shinyDt ??= new DataTransfer();
                if (window.__shinyDt.items.length === 0)
                    window.__shinyDt.items.add(new File([body], name, { type: mime }));
                window.dispatchEvent(new DragEvent(type, { dataTransfer: window.__shinyDt, bubbles: true, cancelable: true, clientX: 200, clientY: 150 }));
                if (type === 'drop' || type === 'dragleave')
                    window.__shinyDt = undefined;
            }
            """,
            new[] { type, name, mime, body }
        );

    [Test]
    public async Task Drag_Enter_And_Leave_Toggle_The_Overlay()
    {
        await Open("/filedrop");
        await Expect(Overlay).ToHaveCountAsync(0);

        await Drag("dragenter");
        await Expect(Overlay).ToBeVisibleAsync();
        await Expect(Overlay).ToHaveTextAsync("Drop to accept 1 file(s).");

        await Drag("dragleave");
        await Expect(Overlay).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync("Drag files anywhere over this page.");
        await Expect(Events.First).ToContainTextAsync("leave");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Drop_Lists_The_File_And_Its_Bytes_Can_Be_Read()
    {
        await Open("/filedrop");

        await Drag("dragenter");
        await Drag("dragover");
        await Drag("drop", "notes.txt", "text/plain", "hello shiny");

        await Expect(Status).ToHaveTextAsync("Dropped 1 file(s).");
        await Expect(Overlay).ToHaveCountAsync(0);
        await Expect(DroppedFiles).ToHaveCountAsync(1);
        await Expect(DroppedFiles.First.Locator("strong")).ToHaveTextAsync("notes.txt");
        await Expect(DroppedFiles.First).ToContainTextAsync("11 bytes");

        await DroppedFiles.First.GetByRole(AriaRole.Button, new() { Name = "Read first 32 bytes" }).ClickAsync();
        // "hello shiny" as hex
        await Expect(DroppedFiles.First.Locator("code")).ToHaveTextAsync("68656C6C6F207368696E79");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Images_Only_Filter_Rejects_A_Text_File_On_Drop()
    {
        await Open("/filedrop");
        await Option("Images only").CheckAsync();
        await Expect(Events.First).ToContainTextAsync("filter — images only");

        // The browser hides names until the drop, so a hover cannot be judged on its extension and
        // is accepted provisionally (by design - FileDropOptions.Accepts); the drop is where it counts.
        await Drag("dragenter", "notes.txt", "text/plain");
        await Expect(Overlay).ToBeVisibleAsync();
        await Drag("drop", "notes.txt", "text/plain");

        // a refused drop ends the drag like a leave: overlay down, nothing listed
        await Expect(Overlay).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync("Drag files anywhere over this page.");
        await Expect(Events.First).ToContainTextAsync("leave");
        await Expect(DroppedFiles).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Images_Only_Filter_Accepts_A_Png()
    {
        await Open("/filedrop");
        await Option("Images only").CheckAsync();

        await Drag("dragenter", "photo.png", "image/png");
        await Expect(Overlay).ToHaveTextAsync("Drop to accept 1 file(s).");

        await Drag("drop", "photo.png", "image/png");
        await Expect(DroppedFiles).ToHaveCountAsync(1);
        await Expect(DroppedFiles.First.Locator("strong")).ToHaveTextAsync("photo.png");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Disabled_Service_Ignores_Drops()
    {
        await Open("/filedrop");
        await Option("Accept drops").UncheckAsync();

        await Drag("dragenter");
        await Drag("drop");

        await Page.WaitForTimeoutAsync(500);
        await Expect(Overlay).ToHaveCountAsync(0);
        await Expect(DroppedFiles).ToHaveCountAsync(0);
        await Expect(Page.GetByText("Nothing yet.")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clear_Empties_Files_And_Events()
    {
        await Open("/filedrop");
        await Drag("dragenter");
        await Drag("drop");
        await Expect(DroppedFiles).ToHaveCountAsync(1);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Clear", Exact = true }).ClickAsync();

        await Expect(DroppedFiles).ToHaveCountAsync(0);
        await Expect(Events).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }
}
