namespace Sample.Blazor.UITests.Features;

/// <summary>
/// TagEntry on the web, typed for real: Enter and the delimiters commit, a paste splits, Enter-only
/// mode keeps commas, MaxTags and duplicate rules hold, the validator refuses, ✕ and Backspace
/// remove, read-only and disabled block, and the page's clear button empties and refocuses.
/// Each field is found by its placeholder - the one thing every instance on the page has distinct.
/// </summary>
public class TagEntryTests : BlazorTest
{
    ILocator Box(string placeholder)
        => Page.Locator(".shiny-tags", new() { Has = Page.GetByPlaceholder(placeholder, new() { Exact = true }) });

    ILocator Input(string placeholder) => Page.GetByPlaceholder(placeholder, new() { Exact = true });

    ILocator Chips(string placeholder) => Box(placeholder).Locator(".shiny-tags__chip-label");

    ILocator TeamState
        => Page.Locator(".demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Default", Exact = true }) }).Locator(".state");

    [Test]
    public async Task Enter_And_Comma_Commit_Tags()
    {
        await Open("tagentry");
        var input = Input("Add a team...");

        await input.PressSequentiallyAsync("qa");
        await input.PressAsync("Enter");
        await input.PressSequentiallyAsync("ops,");

        await Expect(Chips("Add a team...")).ToHaveTextAsync(["design", "engineering", "qa", "ops"]);
        await Expect(TeamState).ToHaveTextAsync("4 team(s): design, engineering, qa, ops");
        await Expect(input).ToHaveValueAsync("");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Remove_Button_And_Backspace_Remove_Tags()
    {
        await Open("tagentry");
        var input = Input("Add a team...");

        await Box("Add a team...").GetByRole(AriaRole.Button, new() { Name = "Remove design" }).ClickAsync();
        await Expect(TeamState).ToHaveTextAsync("1 team(s): engineering");

        await input.FocusAsync();
        await input.PressAsync("Backspace");
        await Expect(Chips("Add a team...")).ToHaveCountAsync(0);
        await Expect(TeamState).ToHaveTextAsync("No teams yet.");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Backspace_Only_Removes_When_The_Field_Is_Empty()
    {
        await Open("tagentry");
        var input = Input("Add a team...");

        await input.PressSequentiallyAsync("ab");
        await input.PressAsync("Backspace");

        await Expect(input).ToHaveValueAsync("a");
        await Expect(Chips("Add a team...")).ToHaveCountAsync(2);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Pasted_List_Splits_On_Every_Delimiter()
    {
        await Open("tagentry");
        var input = Input("Comma or semicolon commits...");

        await input.FocusAsync();
        await Page.Keyboard.InsertTextAsync("alpha; beta, gamma");

        await Expect(Chips("Comma or semicolon commits...")).ToHaveTextAsync(["alpha", "beta", "gamma"]);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Enter_Only_Mode_Keeps_Commas_In_The_Tag()
    {
        await Open("tagentry");
        var input = Input("Only Enter commits...");

        await input.PressSequentiallyAsync("Smith, John");
        await Expect(Chips("Only Enter commits...")).ToHaveCountAsync(0);
        await input.PressAsync("Enter");

        await Expect(Chips("Only Enter commits...")).ToHaveTextAsync(["Smith, John"]);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task MaxTags_Refuses_Past_The_Cap_And_Keeps_The_Text()
    {
        await Open("tagentry");
        var input = Input("At most three...");

        await input.PressSequentiallyAsync("three");
        await input.PressAsync("Enter");
        await Expect(Chips("At most three...")).ToHaveTextAsync(["one", "two", "three"]);

        await input.PressSequentiallyAsync("four");
        await input.PressAsync("Enter");
        await Expect(Chips("At most three...")).ToHaveCountAsync(3);
        await Expect(input).ToHaveValueAsync("four");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Duplicates_Are_Dropped_Unless_Allowed()
    {
        await Open("tagentry");

        var teams = Input("Add a team...");
        await teams.PressSequentiallyAsync("design");
        await teams.PressAsync("Enter");
        await Page.WaitForTimeoutAsync(300);
        await Expect(Chips("Add a team...")).ToHaveTextAsync(["design", "engineering"]);

        var echo = Input("Duplicates welcome...");
        await echo.PressSequentiallyAsync("echo");
        await echo.PressAsync("Enter");
        await Expect(Chips("Duplicates welcome...")).ToHaveTextAsync(["echo", "echo", "echo"]);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Validator_Refuses_Short_Tags_And_Keeps_The_Text()
    {
        await Open("tagentry");
        var input = Input("Three characters or more...");

        await input.PressSequentiallyAsync("ab");
        await input.PressAsync("Enter");
        await Expect(Chips("Three characters or more...")).ToHaveCountAsync(0);
        await Expect(input).ToHaveValueAsync("ab");

        await input.PressSequentiallyAsync("c");
        await input.PressAsync("Enter");
        await Expect(Chips("Three characters or more...")).ToHaveTextAsync(["abc"]);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task ReadOnly_And_Disabled_Block_Editing()
    {
        await Open("tagentry");

        await Expect(Input("Read-only...")).Not.ToBeEditableAsync();
        await Expect(Box("Read-only...").Locator(".shiny-tags__chip-remove")).ToHaveCountAsync(0);
        await Input("Read-only...").FocusAsync();
        await Page.Keyboard.PressAsync("Backspace");
        await Page.Keyboard.TypeAsync("nope,");
        await Expect(Chips("Read-only...")).ToHaveTextAsync(["look", "no touching"]);

        await Expect(Input("Disabled...")).ToBeDisabledAsync();
        await Expect(Chips("Disabled...")).ToHaveTextAsync(["frozen", "field"]);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clear_Button_Empties_And_Refocuses()
    {
        await Open("tagentry");
        var section = Page.Locator(".demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Programmatic focus" }) });
        var clear = section.GetByRole(AriaRole.Button, new() { Name = "Clear and start over" });

        await Expect(Chips("Add a topic...")).ToHaveTextAsync(["getting-started"]);
        await clear.ClickAsync();

        await Expect(Chips("Add a topic...")).ToHaveCountAsync(0);
        await Expect(Input("Add a topic...")).ToBeFocusedAsync();
        await Expect(clear).ToBeDisabledAsync();

        await Page.Keyboard.TypeAsync("routing");
        await Page.Keyboard.PressAsync("Enter");
        await Expect(Chips("Add a topic...")).ToHaveTextAsync(["routing"]);
        await Expect(clear).ToBeEnabledAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Invalid_Marks_The_Inner_Input()
    {
        await Open("tagentry");

        await Expect(Input("Add a tag...")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(Input("Add a team...")).Not.ToHaveAttributeAsync("aria-invalid", "true");
        await AssertNoConsoleErrors();
    }
}
