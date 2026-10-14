namespace Sample.Blazor.UITests.Features;

/// <summary>
/// IMediaService opens its own modal camera page from the layout's MediaHost. With no capture device
/// in the test browser, these cover the modal's lifecycle and the log the page keeps of each call.
/// </summary>
public class MediaServiceTests : BlazorTest
{
    ILocator Log => Page.Locator("ul.media-log li");
    ILocator Dialog => Page.Locator(".shiny-media-host[role=dialog]");

    ILocator PageButton(string text)
        => Page.Locator("section.demo-section button", new() { HasTextRegex = new($"^{System.Text.RegularExpressions.Regex.Escape(text)}$") });

    [Test]
    public async Task List_Cameras_Logs_A_Result_And_Clear_Empties_The_Log()
    {
        await Open("/mediaservice");
        await Expect(Log).ToHaveCountAsync(0);

        await PageButton("List Cameras").ClickAsync();
        await Expect(Log).ToHaveCountAsync(1);
        await Expect(Log.First).ToContainTextAsync(new System.Text.RegularExpressions.Regex("camera", System.Text.RegularExpressions.RegexOptions.IgnoreCase));

        await PageButton("Clear").ClickAsync();
        await Expect(Log).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Take_Photo_Opens_The_Modal_And_Close_Cancels()
    {
        await Open("/mediaservice");

        await PageButton("Take Photo").ClickAsync();
        await Expect(Dialog).ToBeVisibleAsync();
        await Expect(Dialog).ToHaveAttributeAsync("aria-label", "Take a photo");

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();

        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Log.First).ToContainTextAsync("Cancelled");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Escape_Cancels_The_Modal()
    {
        await Open("/mediaservice");

        await PageButton("Record Video").ClickAsync();
        await Expect(Dialog).ToBeVisibleAsync();
        await Dialog.FocusAsync();
        await Page.Keyboard.PressAsync("Escape");

        await Expect(Dialog).ToHaveCountAsync(0);
        await Expect(Log.First).ToContainTextAsync("Cancelled");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Effect_Picker_Selects_A_Look()
    {
        await Open("/mediaservice");

        await PageButton("Photo + Effects").ClickAsync();
        var looks = Dialog.GetByRole(AriaRole.Listbox, new() { Name = "Looks" });
        await Expect(looks).ToBeVisibleAsync();

        var chips = looks.Locator(".shiny-media-chip");
        await Assert.That(await chips.CountAsync()).IsGreaterThan(1);
        var second = chips.Nth(1);
        await Expect(second).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-selected"));

        await second.ClickAsync();
        await Expect(second).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-selected"));
        await Expect(looks.Locator(".shiny-media-chip.is-selected")).ToHaveCountAsync(1);

        await Dialog.GetByRole(AriaRole.Button, new() { Name = "Close" }).ClickAsync();
        await Expect(Dialog).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }
}
