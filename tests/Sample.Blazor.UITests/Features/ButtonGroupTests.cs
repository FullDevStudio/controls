namespace Sample.Blazor.UITests.Features;

/// <summary>
/// ButtonGroup: plain action segments keep their own handlers; Single mode is a segmented picker
/// with a two-way SelectedIndex; Multiple mode toggles any number of segments.
/// </summary>
public class ButtonGroupTests : BlazorTest
{
    ILocator Status => Page.Locator("p", new() { HasText = "Status:" }).Locator("strong");

    ILocator Section(string heading)
        => Page.Locator(".demo-section").Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });


    [Test]
    public async Task Action_Segments_Run_Their_Own_Handlers()
    {
        await Open("buttongroup");
        var actions = Section("Actions");

        await actions.GetByRole(AriaRole.Button, new() { Name = "Report" }).ClickAsync();
        await Expect(Status).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^Report at "));

        await actions.GetByRole(AriaRole.Button, new() { Name = "Snooze" }).ClickAsync();
        await Expect(Status).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^Snooze at "));

        // An action group is not a picker: nothing is pressed.
        await Expect(actions.Locator("[aria-pressed]")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Single_Selection_Moves_The_Pressed_Segment()
    {
        await Open("buttongroup");
        var picker = Section("Single selection");
        var week = picker.GetByRole(AriaRole.Button, new() { Name = "Week" });
        var month = picker.GetByRole(AriaRole.Button, new() { Name = "Month" });

        await Expect(week).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(picker.Locator(".state code")).ToHaveTextAsync("Week");

        await month.ClickAsync();

        await Expect(month).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(month).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-btn--filled"));
        await Expect(week).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(week).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-btn--outlined"));
        await Expect(picker.Locator(".state code")).ToHaveTextAsync("Month");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Single_Selection_Cannot_Be_Emptied_By_Retapping()
    {
        await Open("buttongroup");
        var picker = Section("Single selection");
        var week = picker.GetByRole(AriaRole.Button, new() { Name = "Week" });

        await week.ClickAsync();

        await Expect(week).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(picker.Locator(".state code")).ToHaveTextAsync("Week");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Multiple_Selection_Toggles_Each_Segment()
    {
        await Open("buttongroup");
        var formats = Section("Multiple selection");
        var bold = formats.GetByRole(AriaRole.Button, new() { Name = "B", Exact = true });
        var underline = formats.GetByRole(AriaRole.Button, new() { Name = "U", Exact = true });

        await underline.ClickAsync();
        await bold.ClickAsync();

        await Expect(bold).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(underline).ToHaveAttributeAsync("aria-pressed", "true");
        // Reported in index order, not click order.
        await Expect(formats.Locator(".state")).ToHaveTextAsync("Bold, Underline");

        await bold.ClickAsync();
        await Expect(bold).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(formats.Locator(".state")).ToHaveTextAsync("Underline");

        await underline.ClickAsync();
        await Expect(formats.Locator(".state")).ToHaveTextAsync("No formatting");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Text_Segment_Group_Adjusts_The_Amount()
    {
        await Open("buttongroup");
        var amount = Section("Text segment");

        await Expect(amount.Locator(".shiny-btn-group__text")).ToHaveTextAsync("USD");

        await amount.GetByRole(AriaRole.Button, new() { Name = "Increase" }).ClickAsync();
        await amount.GetByRole(AriaRole.Button, new() { Name = "Increase" }).ClickAsync();
        await amount.GetByRole(AriaRole.Button, new() { Name = "Decrease" }).ClickAsync();

        await Expect(amount.Locator(".state code")).ToHaveTextAsync("1");
        await Expect(Status).ToHaveTextAsync("Amount decreased");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Icon_Only_Segments_Are_Named_And_Clickable()
    {
        await Open("buttongroup");

        await Section("Vertical").GetByRole(AriaRole.Button, new() { Name = "Zoom out" }).ClickAsync();
        await Expect(Status).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^Zoom out at "));

        await Section("Split button").GetByRole(AriaRole.Button, new() { Name = "More follow options" }).ClickAsync();
        await Expect(Status).ToHaveTextAsync(new System.Text.RegularExpressions.Regex("^More follow options at "));
        await AssertNoConsoleErrors();
    }
}
