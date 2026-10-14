namespace Sample.Blazor.UITests.Features;

/// <summary>
/// AutoCompleteEntry on the web: local filtering into the dropdown, picking by mouse (the
/// control has no arrow-key navigation), typing again clearing the selection, TextMemberPath objects, and the SearchCommand
/// path with its busy spinner.
/// </summary>
public class AutoCompleteTests : BlazorTest
{
    ILocator Section(string heading)
        => Page.Locator(".demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });

    static readonly string[] Berries = ["Blackberry", "Blueberry", "Cranberry", "Raspberry", "Strawberry"];

    [Test]
    public async Task Typing_Filters_Local_Items()
    {
        await Open("autocomplete");
        var section = Section("Fruits (local filter, no SearchCommand)");

        await section.GetByPlaceholder("Type a fruit...").PressSequentiallyAsync("berry");

        await Expect(section.Locator(".shiny-ac-item")).ToHaveTextAsync(Berries);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clicking_A_Suggestion_Selects_It()
    {
        await Open("autocomplete");
        var section = Section("Fruits (local filter, no SearchCommand)");
        var input = section.GetByPlaceholder("Type a fruit...");

        await input.PressSequentiallyAsync("berry");
        await section.Locator(".shiny-ac-item", new() { HasText = "Blueberry" }).ClickAsync();

        await Expect(section.Locator(".state")).ToHaveTextAsync("Selected: Blueberry");
        await Expect(input).ToHaveValueAsync("Blueberry");
        await Expect(section.Locator(".shiny-ac-item")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Typing_Again_Clears_The_Selection()
    {
        await Open("autocomplete");
        var section = Section("Fruits (local filter, no SearchCommand)");
        var input = section.GetByPlaceholder("Type a fruit...");
        await input.PressSequentiallyAsync("kiw");
        await section.Locator(".shiny-ac-item", new() { HasText = "Kiwi" }).ClickAsync();
        await Expect(section.Locator(".state")).ToHaveTextAsync("Selected: Kiwi");

        await input.PressAsync("Backspace");

        await Expect(section.Locator(".state")).ToHaveTextAsync("Selected: (none)");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task TextMemberPath_Shows_And_Selects_Objects()
    {
        await Open("autocomplete");
        var section = Section("Programming Languages (TextMemberPath)");
        var input = section.GetByPlaceholder("Search languages...");

        await input.PressSequentiallyAsync("script");
        await Expect(section.Locator(".shiny-ac-item")).ToHaveTextAsync(["JavaScript", "TypeScript"]);
        await section.Locator(".shiny-ac-item", new() { HasText = "TypeScript" }).ClickAsync();

        await Expect(input).ToHaveValueAsync("TypeScript");
        await Expect(section.Locator(".state")).ToHaveTextAsync("TypeScript (2012) — Multi-paradigm");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task SearchCommand_Shows_Spinner_Then_Results()
    {
        await Open("autocomplete");
        var section = Section("Simulated Async Search (with spinner)");
        var input = section.GetByPlaceholder("Search cities...");

        await input.PressSequentiallyAsync("to");

        await Expect(section.Locator(".shiny-ac-spinner")).ToBeVisibleAsync();
        await Expect(section.Locator(".shiny-ac-item")).ToHaveTextAsync(["Tokyo", "Toronto", "Stockholm"]);
        await Expect(section.Locator(".shiny-ac-spinner")).ToHaveCountAsync(0);

        await section.Locator(".shiny-ac-item", new() { HasText = "Toronto" }).ClickAsync();
        await Expect(section.Locator(".state")).ToHaveTextAsync("Toronto (founded 1793) — Americas");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Below_Threshold_Does_Not_Search()
    {
        await Open("autocomplete");
        var section = Section("Simulated Async Search (with spinner)");

        await section.GetByPlaceholder("Search cities...").PressSequentiallyAsync("t");
        await Page.WaitForTimeoutAsync(1500);

        await Expect(section.Locator(".shiny-ac-spinner")).ToHaveCountAsync(0);
        await Expect(section.Locator(".shiny-ac-item")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }
}
