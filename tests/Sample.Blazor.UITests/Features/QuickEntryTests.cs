namespace Sample.Blazor.UITests.Features;

/// <summary>
/// Quick Entry: the inline PromptView (type, Enter, busy, answer; arrow keys through suggestions),
/// the popup's show/hide/toggle lifecycle, and the screen glow.
/// </summary>
public class QuickEntryTests : BlazorTest
{
    ILocator InlinePrompt => Page.Locator("section.demo-card .shiny-prompt").First;
    ILocator Log => Page.Locator("ul.log li");
    ILocator Popup => Page.Locator(".shiny-quick-entry");

    ILocator CardButton(string card, string name)
        => Page.Locator("section.demo-card", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = card }) })
            .GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    [Test]
    public async Task Inline_Prompt_Submits_On_Enter_And_Shows_The_Answer()
    {
        await Open("/quickentry");
        var field = InlinePrompt.Locator("input.shiny-prompt-field");

        await field.FillAsync("what is shiny");
        await field.PressAsync("Enter");

        await Expect(Log.First).ToContainTextAsync("Inline submitted: \"what is shiny\"");
        await Expect(InlinePrompt.Locator(".shiny-prompt-response"))
            .ToContainTextAsync("You asked \"what is shiny\"", new() { Timeout = 10_000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Arrow_Keys_Move_Through_Suggestions()
    {
        await Open("/quickentry");
        var field = InlinePrompt.Locator("input.shiny-prompt-field");
        var suggestions = InlinePrompt.Locator(".shiny-prompt-suggestion");
        await field.FocusAsync();

        await field.PressAsync("ArrowDown");
        await Expect(suggestions.Nth(0)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-highlighted"));

        await field.PressAsync("ArrowDown");
        await Expect(suggestions.Nth(1)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-highlighted"));
        await Expect(InlinePrompt.Locator(".shiny-prompt-suggestion.is-highlighted")).ToHaveCountAsync(1);

        await field.PressAsync("ArrowUp");
        await Expect(suggestions.Nth(0)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-highlighted"));
        await AssertNoConsoleErrors();
    }

    /// <summary>The popup is modal: its scrim covers the page, so a click on it is how a user dismisses it.</summary>
    async Task ClickScrim()
    {
        var box = await Popup.Locator(".shiny-quick-entry-scrim").BoundingBoxAsync();
        await Page.Mouse.ClickAsync(box!.X + 10, box.Y + box.Height - 10);
    }


    [Test]
    public async Task Popup_Show_And_Scrim_Raise_Opened_And_Closed()
    {
        await Open("/quickentry");
        await Expect(Popup).ToHaveCountAsync(0);

        await CardButton("Popup", "Show").ClickAsync();
        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Popup.Locator(".shiny-prompt")).ToBeVisibleAsync();
        await Expect(Log.First).ToContainTextAsync("Popup opened");

        await ClickScrim();
        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Log.First).ToContainTextAsync("Popup closed");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Toggle_Opens_The_Popup()
    {
        await Open("/quickentry");

        await CardButton("Popup", "Toggle").ClickAsync();
        await Expect(Popup).ToBeVisibleAsync();
        await Expect(Log.First).ToContainTextAsync("Popup opened");

        await ClickScrim();
        await Expect(Popup).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Popup_Prompt_Submits_And_Answers()
    {
        await Open("/quickentry");
        await CardButton("Popup", "Show").ClickAsync();
        var field = Popup.Locator("input.shiny-prompt-field");

        await field.FillAsync("from the popup");
        await field.PressAsync("Enter");

        await Expect(Log.First).ToContainTextAsync("Submitted: \"from the popup\"");
        await Expect(Popup.Locator(".shiny-prompt-response"))
            .ToContainTextAsync("You asked \"from the popup\"", new() { Timeout = 10_000 });
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Escape_Backs_Out_One_Step_Then_Dismisses()
    {
        await Open("/quickentry");
        await CardButton("Popup", "Show").ClickAsync();
        await Expect(Popup).ToBeVisibleAsync();
        var field = Popup.Locator("input.shiny-prompt-field");
        await field.FocusAsync();

        // first Escape only clears the highlighted suggestion
        await field.PressAsync("ArrowDown");
        await Expect(Popup.Locator(".shiny-prompt-suggestion.is-highlighted")).ToHaveCountAsync(1);
        await field.PressAsync("Escape");
        await Expect(Popup.Locator(".shiny-prompt-suggestion.is-highlighted")).ToHaveCountAsync(0);
        await Expect(Popup).ToBeVisibleAsync();

        // with nothing left to back out of, the next one closes the popup
        await field.PressAsync("Escape");
        await Expect(Popup).ToHaveCountAsync(0);
        await Expect(Log.First).ToContainTextAsync("Popup closed");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Placement_Option_Moves_The_Popup()
    {
        await Open("/quickentry");
        await Page.Locator("section.demo-card select").First.SelectOptionAsync("BottomCenter");

        await CardButton("Popup", "Show").ClickAsync();
        await Expect(Popup).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-bottom"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Screen_Glow_Shows_And_Hides()
    {
        await Open("/quickentry");
        var glow = Page.Locator(".shiny-glow");

        await CardButton("Screen glow", "Show").ClickAsync();
        await Expect(glow).ToBeVisibleAsync();

        await CardButton("Screen glow", "Hide").ClickAsync();
        await Expect(glow).ToBeHiddenAsync();
        await AssertNoConsoleErrors();
    }
}
