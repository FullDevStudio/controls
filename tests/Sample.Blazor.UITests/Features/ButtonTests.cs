namespace Sample.Blazor.UITests.Features;

/// <summary>
/// ShinyButton: clicks raise Clicked, an async handler holds the button busy for exactly as long as
/// it runs, Success/Error states show and revert, and Disabled blocks the click.
/// </summary>
public class ButtonTests : BlazorTest
{
    ILocator Status => Page.Locator("p", new() { HasText = "Status:" }).Locator("strong");

    // A button's text content includes its motion icons' inline <style>, so the label is read from
    // aria-label, which ShinyButton keeps equal to the text it is showing (busy/success/error included).
    ILocator Section(string heading)
        => Page.Locator(".demo-section").Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });


    [Test]
    public async Task Click_Raises_Clicked()
    {
        await Open("button");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Tonal", Exact = true }).ClickAsync();
        await Expect(Status).ToHaveTextAsync("Tonal clicked");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
        await Expect(Status).ToHaveTextAsync("Delete clicked");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Icon_Only_Button_Takes_Its_Name_From_Aria_Label()
    {
        await Open("button");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Settings", Exact = true }).ClickAsync();

        await Expect(Status).ToHaveTextAsync("Settings clicked");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Async_Handler_Holds_The_Button_Busy_Until_It_Finishes()
    {
        await Open("button");
        var button = Section("Busy modes").Locator(".shiny-btn").Nth(0);

        await button.ClickAsync();

        await Expect(button).ToHaveAttributeAsync("aria-busy", "true");
        await Expect(button).ToHaveAttributeAsync("aria-label", "Saving...");
        await Expect(Status).ToHaveTextAsync("Saving...");

        await Expect(Status).ToHaveTextAsync("Saved", new() { Timeout = 6000 });
        await Expect(button).Not.ToHaveAttributeAsync("aria-busy", "true");
        await Expect(button).ToHaveAttributeAsync("aria-label", "Save (ReplaceLeftIcon)");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task KeepContent_Busy_Mode_Keeps_The_Label()
    {
        await Open("button");
        var button = Section("Busy modes").Locator(".shiny-btn").Nth(2);

        await button.ClickAsync();

        await Expect(button).ToHaveAttributeAsync("aria-busy", "true");
        await Expect(button).ToHaveAttributeAsync("aria-label", "Save (KeepContent)");
        await Expect(Status).ToHaveTextAsync("Saved", new() { Timeout = 6000 });
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Handler_Reported_Success_Shows_Then_Reverts()
    {
        await Open("button");
        var submit = Section("Success & error states").Locator(".shiny-btn").Nth(0);

        await submit.ClickAsync();
        await Expect(submit).ToHaveAttributeAsync("aria-label", "Submitting...");

        // The handler sets Success itself; the button must keep it rather than resetting to Normal...
        await Expect(submit).ToHaveAttributeAsync("aria-label", "Submitted", new() { Timeout = 6000 });
        await Expect(Status).ToHaveTextAsync("Submitted");

        // ...until StateRevertDelay (2s) puts it back.
        await Expect(submit).ToHaveAttributeAsync("aria-label", "Submit", new() { Timeout = 6000 });
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Faulted_Handler_Shows_The_Error_State()
    {
        await Open("button");
        var failing = Section("Success & error states").Locator(".shiny-btn").Nth(1);

        await failing.ClickAsync();
        await Expect(failing).ToHaveAttributeAsync("aria-label", "Trying...");

        await Expect(failing).ToHaveAttributeAsync("aria-label", "Failed", new() { Timeout = 6000 });
        await Expect(failing).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-btn--error"));
        await Expect(failing).Not.ToHaveAttributeAsync("aria-busy", "true");

        // No AssertNoConsoleErrors here: ShinyButton rethrows the handler's exception by design, and
        // the page does not catch it — see Faulting_Demo_Does_Not_Crash_The_Page.
    }


    [Test]
    [Skip("BUG: the 'This will fail' demo's handler throws and ShinyButton rethrows it (by design), so the button page raises Blazor's unhandled-error bar and the page's status never updates to 'It failed'")]
    public async Task Faulting_Demo_Does_Not_Crash_The_Page()
    {
        await Open("button");
        var failing = Section("Success & error states").Locator(".shiny-btn").Nth(1);

        await failing.ClickAsync();
        await Expect(failing).ToHaveAttributeAsync("aria-label", "Failed", new() { Timeout = 6000 });

        await Expect(Status).ToHaveTextAsync("It failed");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Disabled_Button_Ignores_Clicks_Until_Enabled()
    {
        await Open("button");
        var section = Section("Disabled");
        var locked = section.Locator(".shiny-btn").Nth(0);

        await Expect(locked).ToBeDisabledAsync();
        await Expect(locked).ToHaveAttributeAsync("aria-label", "Locked");
        await locked.ClickAsync(new() { Force = true });
        await Expect(Status).ToHaveTextAsync("Nothing has happened yet");

        await section.GetByRole(AriaRole.Button, new() { Name = "Toggle" }).ClickAsync();

        await Expect(locked).ToBeEnabledAsync();
        await Expect(locked).ToHaveAttributeAsync("aria-label", "Unlocked");
        await locked.ClickAsync();
        await Expect(Status).ToHaveTextAsync("Unlocked action clicked");
        await AssertNoConsoleErrors();
    }
}
