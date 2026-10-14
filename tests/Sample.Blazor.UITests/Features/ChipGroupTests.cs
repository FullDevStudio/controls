namespace Sample.Blazor.UITests.Features;

/// <summary>
/// ChipGroup on the web: single selection (aria-pressed + the bound item), re-click and
/// AllowDeselect, multiple selection into a bound list, MaxSelectionCount, SelectionMode.None
/// reporting clicks, DisplaySelector objects, AllowRemove with a refusing ChipRemoving, keyboard
/// activation, and read-only / disabled.
/// </summary>
public class ChipGroupTests : BlazorTest
{
    ILocator Section(string heading)
        => Page.Locator(".demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });

    ILocator Chip(ILocator scope, string text)
        => scope.Locator(".shiny-chips__body", new() { Has = Page.Locator(".shiny-chips__label", new() { HasTextRegex = new System.Text.RegularExpressions.Regex($"^\\s*{System.Text.RegularExpressions.Regex.Escape(text)}\\b") }) });

    ILocator Status => Page.Locator("h1 + .state");

    [Test]
    public async Task Single_Selection_Picks_One_Chip()
    {
        await Open("chipgroup");
        var section = Section("Single (the default)");

        await Chip(section, "Week").ClickAsync();
        await Expect(section.Locator(".state")).ToHaveTextAsync("Range: Week");
        await Expect(Chip(section, "Week")).ToHaveAttributeAsync("aria-pressed", "true");

        await Chip(section, "Quarter").ClickAsync();
        await Expect(section.Locator(".state")).ToHaveTextAsync("Range: Quarter");
        await Expect(Chip(section, "Week")).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(section.Locator(".shiny-chips__chip.is-selected")).ToHaveCountAsync(1);

        // re-clicking the answer is a no-op without AllowDeselect
        await Chip(section, "Quarter").ClickAsync();
        await Expect(section.Locator(".state")).ToHaveTextAsync("Range: Quarter");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task AllowDeselect_Clears_On_Second_Click()
    {
        await Open("chipgroup");
        var section = Section("Deselectable");

        await Chip(section, "Month").ClickAsync();
        await Expect(Chip(section, "Month")).ToHaveAttributeAsync("aria-pressed", "true");

        await Chip(section, "Month").ClickAsync();
        await Expect(Chip(section, "Month")).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(section.Locator(".shiny-chips__chip.is-selected")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Multiple_Selection_Toggles_Into_The_Bound_List()
    {
        await Open("chipgroup");
        var section = Section("Multiple");

        await Chip(section, "Open").ClickAsync();
        await Chip(section, "Blocked").ClickAsync();
        await Expect(section.Locator(".state")).ToHaveTextAsync("Open, Blocked");

        await Chip(section, "Open").ClickAsync();
        await Expect(section.Locator(".state")).ToHaveTextAsync("Blocked");
        await Expect(Chip(section, "Open")).ToHaveAttributeAsync("aria-pressed", "false");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task MaxSelectionCount_Refuses_A_Fourth_Pick()
    {
        await Open("chipgroup");
        var section = Section("Capped");

        foreach (var topping in new[] { "Cheese", "Mushroom", "Olive", "Pepper" })
            await Chip(section, topping).ClickAsync();

        await Expect(section.Locator(".shiny-chips__chip.is-selected")).ToHaveCountAsync(3);
        await Expect(Chip(section, "Pepper")).ToHaveAttributeAsync("aria-pressed", "false");

        // freeing a slot lets the refused chip in
        await Chip(section, "Cheese").ClickAsync();
        await Chip(section, "Pepper").ClickAsync();
        await Expect(Chip(section, "Pepper")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(section.Locator(".shiny-chips__chip.is-selected")).ToHaveCountAsync(3);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Action_Chips_Report_Clicks_Without_Selecting()
    {
        await Open("chipgroup");
        var section = Section("Actions");

        await Chip(section, "Archive").ClickAsync();
        await Expect(Status).ToHaveTextAsync("Action: Archive");
        await Chip(section, "Share").ClickAsync();
        await Expect(Status).ToHaveTextAsync("Action: Share");

        await Expect(section.Locator(".shiny-chips__chip.is-selected")).ToHaveCountAsync(0);
        await Expect(Chip(section, "Share")).Not.ToHaveAttributeAsync("aria-pressed", new System.Text.RegularExpressions.Regex(".*"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Object_Items_Show_Their_Display_And_Select_The_Object()
    {
        await Open("chipgroup");
        var section = Section("Objects");

        await Expect(section.Locator(".shiny-chips__label")).ToHaveTextAsync(["Design", "Engineering", "Sales"]);
        await Chip(section, "Engineering").ClickAsync();

        await Expect(section.Locator(".state")).ToHaveTextAsync("Team: Engineering (24)");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Remove_Takes_Items_Out_But_Never_The_Last()
    {
        await Open("chipgroup");
        var section = Section("Removable");
        var labels = section.Locator(".shiny-chips__label");

        await section.GetByRole(AriaRole.Button, new() { Name = "Remove design" }).ClickAsync();
        await Expect(labels).ToHaveTextAsync(["engineering", "sales"]);
        await Expect(Status).ToHaveTextAsync("Removed design");

        await section.GetByRole(AriaRole.Button, new() { Name = "Remove sales" }).ClickAsync();
        await Expect(labels).ToHaveTextAsync(["engineering"]);

        await section.GetByRole(AriaRole.Button, new() { Name = "Remove engineering" }).ClickAsync();
        await Page.WaitForTimeoutAsync(300);
        await Expect(labels).ToHaveTextAsync(["engineering"]);

        await section.GetByRole(AriaRole.Button, new() { Name = "Reset recipients" }).ClickAsync();
        await Expect(labels).ToHaveTextAsync(["design", "engineering", "sales"]);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Keyboard_Activates_A_Chip()
    {
        await Open("chipgroup");
        var section = Section("Single (the default)");

        await Chip(section, "Year").FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(section.Locator(".state")).ToHaveTextAsync("Range: Year");

        await Chip(section, "Day").FocusAsync();
        await Page.Keyboard.PressAsync("Space");
        await Expect(section.Locator(".state")).ToHaveTextAsync("Range: Day");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task ReadOnly_And_Disabled_Cannot_Change()
    {
        await Open("chipgroup");
        var groups = Section("Read-only and disabled").Locator(".shiny-chips");

        foreach (var index in new[] { 0, 1 })
        {
            var bodies = groups.Nth(index).Locator(".shiny-chips__body");
            await Expect(bodies).ToHaveCountAsync(2);
            await Expect(bodies.First).ToBeDisabledAsync();
            await Expect(groups.Nth(index).Locator(".shiny-chips__remove")).ToHaveCountAsync(0);
            await bodies.First.ClickAsync(new() { Force = true });
            await Expect(groups.Nth(index).Locator(".shiny-chips__chip.is-selected")).ToHaveCountAsync(0);
        }
        await AssertNoConsoleErrors();
    }
}
