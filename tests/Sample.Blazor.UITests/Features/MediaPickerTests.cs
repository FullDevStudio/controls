namespace Sample.Blazor.UITests.Features;

/// <summary>
/// MediaPickerButton driven through its real file chooser: a small PNG is handed to the browser's
/// picker, compressed in the page, and counted, previewed, removed and read back.
/// </summary>
public class MediaPickerTests : BlazorTest
{
    // 8x6 solid red PNG
    static readonly byte[] SamplePng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAGCAIAAABxZ0isAAAAEklEQVR4nGP4z8CAFWEXpZMEADMoL9HswQX5AAAAAElFTkSuQmCC");

    ILocator Picker => Page.Locator(".shiny-mediapicker").First;
    ILocator AddButton => Picker.Locator(".shiny-mediapicker-add");
    ILocator Count => Page.Locator("p", new() { HasText = "Photos added:" });

    ILocator Option(string label) => Page.Locator(".demo-controls label", new() { HasText = label }).Locator("input");

    /// <summary>
    /// iPhone and iPad: the control skips its own chooser and goes straight to the browser's file
    /// input, because WebKit there answers it with its own library/camera/files sheet. Same test as
    /// media-picker.js makes.
    /// </summary>
    Task<bool> IsAppleTouch() => Page.EvaluateAsync<bool>(
        "() => /iPhone|iPad|iPod/.test(navigator.userAgent) || (/Macintosh/.test(navigator.userAgent) && navigator.maxTouchPoints > 1)");

    async Task AddPhotoThroughChooser()
    {
        IFileChooser chooser;
        if (await IsAppleTouch())
        {
            chooser = await Page.RunAndWaitForFileChooserAsync(() => AddButton.ClickAsync());
        }
        else
        {
            await AddButton.ClickAsync();
            var gallery = Picker.GetByRole(AriaRole.Button, new() { Name = "Choose from Gallery" });
            chooser = await Page.RunAndWaitForFileChooserAsync(() => gallery.ClickAsync());
        }
        await chooser.SetFilesAsync(new FilePayload { Name = "red.png", MimeType = "image/png", Buffer = SamplePng });
    }

    [Test]
    public async Task Picking_A_Photo_Adds_A_Thumbnail()
    {
        await Open("/mediapicker");
        await Expect(Count).ToHaveTextAsync("Photos added: 0");
        await Expect(Picker.Locator(".shiny-mediapicker-empty")).ToBeVisibleAsync();

        await AddPhotoThroughChooser();

        await Expect(Count).ToHaveTextAsync("Photos added: 1");
        await Expect(Picker.Locator(".shiny-mediapicker-thumb")).ToHaveCountAsync(1);
        await Expect(Picker.Locator(".shiny-mediapicker-empty")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Removing_A_Photo_Returns_To_Empty()
    {
        await Open("/mediapicker");
        await AddPhotoThroughChooser();
        await Expect(Count).ToHaveTextAsync("Photos added: 1");

        await Picker.Locator(".shiny-mediapicker-remove").First.ClickAsync();

        await Expect(Count).ToHaveTextAsync("Photos added: 0");
        await Expect(Picker.Locator(".shiny-mediapicker-empty")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Chooser_Cancel_Closes_It()
    {
        await Open("/mediapicker");
        var sheet = Picker.Locator(".shiny-mediapicker-sheet");
        if (await IsAppleTouch())
        {
            // no sheet of ours to cancel: the browser's own sheet opens instead
            await Page.RunAndWaitForFileChooserAsync(() => AddButton.ClickAsync());
            await Expect(sheet).ToHaveCountAsync(0);
            await Expect(Count).ToHaveTextAsync("Photos added: 0");
            await AssertNoConsoleErrors();
            return;
        }

        await AddButton.ClickAsync();
        await Expect(sheet).ToBeVisibleAsync();

        await sheet.GetByRole(AriaRole.Button, new() { Name = "Cancel" }).ClickAsync();

        await Expect(sheet).ToHaveCountAsync(0);
        await Expect(Count).ToHaveTextAsync("Photos added: 0");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Gallery_Only_Skips_The_Chooser()
    {
        await Open("/mediapicker");
        await Option("Allow camera").UncheckAsync();

        var chooser = await Page.RunAndWaitForFileChooserAsync(() => AddButton.ClickAsync());
        await Assert.That(chooser.IsMultiple).IsFalse();
        await Expect(Picker.Locator(".shiny-mediapicker-sheet")).ToHaveCountAsync(0);

        await chooser.SetFilesAsync(new FilePayload { Name = "red.png", MimeType = "image/png", Buffer = SamplePng });
        await Expect(Count).ToHaveTextAsync("Photos added: 1");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Max_Photos_Hides_The_Add_Button()
    {
        await Open("/mediapicker");
        await Option("Max:").FillAsync("1");
        await Option("Max:").BlurAsync();

        await AddPhotoThroughChooser();

        await Expect(Count).ToHaveTextAsync("Photos added: 1");
        await Expect(AddButton).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Output_Format_Decides_The_Stored_Type()
    {
        await Open("/mediapicker");
        await Option("PNG output").CheckAsync();

        await AddPhotoThroughChooser();
        await Expect(Count).ToHaveTextAsync("Photos added: 1");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Read the first photo's bytes" }).ClickAsync();
        await Expect(Page.Locator("span", new() { HasText = " bytes, " })).ToContainTextAsync("image/png");
        await AssertNoConsoleErrors();
    }
}
