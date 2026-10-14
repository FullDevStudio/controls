using System.Globalization;
using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class RangePickersTests : BlazorTest
{
    static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    ILocator Section(string heading) => Page.Locator("section.demo-section", new() { Has = Page.Locator("h2", new() { HasTextRegex = new Regex($"^{Regex.Escape(heading)}$") }) });
    ILocator Inline => Section("Inline calendar");
    ILocator InlineDay(DateOnly d) => Inline.Locator($".shiny-rangecal-day[data-date='{Iso(d)}']");
    ILocator InlineDescription => Inline.Locator("p strong");

    /// <summary>The open popover of a range field - only one is open at a time.</summary>
    ILocator Popover => Page.Locator(".shiny-rangefield-popover[role=dialog]");
    ILocator PopoverDay(DateOnly d) => Popover.Locator($".shiny-rangecal-day[data-date='{Iso(d)}']");
    ILocator PopoverButton(string name) => Popover.Locator(".shiny-rangefield-button", new() { HasTextRegex = new Regex($"^{name}$") });


    // ---------------------------------------------------------------- inline calendar

    [Test]
    public async Task Inline_Shows_The_Bound_Range()
    {
        await Open("rangepickers");

        await Expect(InlineDay(Today.AddDays(-3))).ToHaveClassAsync(new Regex(@"\bis-start\b"));
        await Expect(InlineDay(Today.AddDays(3))).ToHaveClassAsync(new Regex(@"\bis-end\b"));
        await Expect(InlineDay(Today)).ToHaveClassAsync(new Regex(@"\bis-in-range\b"));
        await Expect(InlineDescription).ToHaveTextAsync(new Regex(@"\(7 days\)$"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Inline_First_Tap_Starts_And_Second_Completes_A_Range()
    {
        await Open("rangepickers");
        var start = Today.AddDays(1);
        var end = Today.AddDays(5);

        await InlineDay(start).ClickAsync();

        // Half a range: the start is set and the end is still open.
        await Expect(InlineDay(start)).ToHaveClassAsync(new Regex(@"\bis-start\b"));
        await Expect(InlineDescription).ToHaveTextAsync(start.ToString("d", En) + " – …");

        await InlineDay(end).ClickAsync();

        await Expect(InlineDay(end)).ToHaveClassAsync(new Regex(@"\bis-end\b"));
        await Expect(InlineDay(Today.AddDays(3))).ToHaveClassAsync(new Regex(@"\bis-in-range\b"));
        await Expect(InlineDescription).ToHaveTextAsync(new Regex(@"\(5 days\)$"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Inline_A_Third_Tap_Starts_Over()
    {
        await Open("rangepickers");

        // The page starts with a complete range, so the next tap is a fresh start.
        await InlineDay(Today.AddDays(2)).ClickAsync();

        await Expect(InlineDescription).ToHaveTextAsync(Today.AddDays(2).ToString("d", En) + " – …");
        await Expect(Inline.Locator(".shiny-rangecal-day.is-end")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Inline_Preset_Applies_A_Range()
    {
        await Open("rangepickers");
        var preset = Inline.Locator(".shiny-rangecal-preset", new() { HasTextRegex = new Regex("^Last 7 days$") });

        await preset.ClickAsync();

        await Expect(preset).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(InlineDescription).ToHaveTextAsync(new Regex(@"\(7 days\)$"));
        await Expect(InlineDay(Today)).ToHaveClassAsync(new Regex(@"\bis-end\b"));
        await AssertNoConsoleErrors();
    }


    static string Title(DateOnly d) => new DateTime(d.Year, d.Month, 1).ToString("MMMM yyyy", En);


    [Test]
    public async Task Inline_Previous_Month_Pages_Back()
    {
        await Open("rangepickers");
        var titles = Inline.Locator(".shiny-rangecal-title");
        await Expect(titles).ToHaveTextAsync(new[] { Title(Today), Title(Today.AddMonths(1)) });

        await Inline.GetByRole(AriaRole.Button, new() { Name = "Previous month" }).ClickAsync();
        await Expect(titles).ToHaveTextAsync(new[] { Title(Today.AddMonths(-1)), Title(Today) });

        await Inline.GetByRole(AriaRole.Button, new() { Name = "Previous month" }).ClickAsync();
        await Expect(titles).ToHaveTextAsync(new[] { Title(Today.AddMonths(-2)), Title(Today.AddMonths(-1)) });
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Inline_Next_Month_Pages_Forward()
    {
        await Open("rangepickers");
        var titles = Inline.Locator(".shiny-rangecal-title");

        await Inline.GetByRole(AriaRole.Button, new() { Name = "Next month" }).ClickAsync();

        await Expect(titles).ToHaveTextAsync(new[] { Title(Today.AddMonths(1)), Title(Today.AddMonths(2)) });
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Inline_Arrow_Keys_Move_Focus_Between_Days()
    {
        await Open("rangepickers");
        var start = InlineDay(Today.AddDays(-3));
        await start.FocusAsync();

        await start.PressAsync("ArrowRight");

        await Expect(InlineDay(Today.AddDays(-2))).ToBeFocusedAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        await Expect(InlineDay(Today.AddDays(5))).ToBeFocusedAsync();
        await AssertNoConsoleErrors();
    }


    // ---------------------------------------------------------------- date range picker (popover)

    [Test]
    public async Task Picker_Edits_A_Draft_And_Apply_Commits_It()
    {
        await Open("rangepickers");
        var section = Section("Date range picker");
        var trigger = section.Locator(".shiny-rangefield-trigger");

        await trigger.ClickAsync();
        await Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Popover).ToHaveAttributeAsync("aria-label", "Report period");
        await Expect(PopoverButton("Apply")).ToBeDisabledAsync();

        await PopoverDay(Today.AddDays(1)).ClickAsync();
        await PopoverDay(Today.AddDays(4)).ClickAsync();
        await Expect(PopoverButton("Apply")).ToBeEnabledAsync();
        // Still only a draft - the bound value hasn't moved.
        await Expect(section.Locator("p strong")).ToHaveTextAsync("—");

        await PopoverButton("Apply").ClickAsync();

        await Expect(Popover).ToHaveCountAsync(0);
        await Expect(trigger).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(trigger).Not.ToHaveClassAsync(new Regex(@"\bis-placeholder\b"));
        await Expect(section.Locator("p strong")).ToHaveTextAsync(new Regex(@"\(4 days\)$"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Picker_Cancel_Discards_The_Draft()
    {
        await Open("rangepickers");
        var section = Section("Date range picker");
        var trigger = section.Locator(".shiny-rangefield-trigger");

        await trigger.ClickAsync();
        await PopoverDay(Today.AddDays(1)).ClickAsync();
        await PopoverDay(Today.AddDays(4)).ClickAsync();
        await PopoverButton("Cancel").ClickAsync();

        await Expect(Popover).ToHaveCountAsync(0);
        await Expect(trigger).ToHaveClassAsync(new Regex(@"\bis-placeholder\b"));
        await Expect(trigger.Locator(".shiny-rangefield-text")).ToHaveTextAsync("Select dates");
        await Expect(section.Locator("p strong")).ToHaveTextAsync("—");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Picker_Preset_Fills_The_Draft()
    {
        await Open("rangepickers");
        var section = Section("Date range picker");

        await section.Locator(".shiny-rangefield-trigger").ClickAsync();
        await Popover.Locator(".shiny-rangecal-preset", new() { HasTextRegex = new Regex("^Today$") }).ClickAsync();
        await Expect(Popover.Locator(".shiny-rangefield-summary")).Not.ToBeEmptyAsync();
        await PopoverButton("Apply").ClickAsync();

        await Expect(section.Locator("p strong")).ToHaveTextAsync(new Regex(@"\(1 days\)$"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Booking_Blocks_Past_And_Booked_Days()
    {
        await Open("rangepickers");
        var section = Section("Booking");

        await section.Locator(".shiny-rangefield-trigger").ClickAsync();

        // MinDate = today: yesterday is out of reach (when it is on screen at all).
        if (await PopoverDay(Today.AddDays(-1)).CountAsync() > 0)
            await Expect(PopoverDay(Today.AddDays(-1))).ToHaveAttributeAsync("aria-disabled", "true");
        await Expect(PopoverDay(Today)).Not.ToHaveAttributeAsync("aria-disabled", "true");
        // Booked nights (today+5, today+6) can't be picked.
        await Expect(PopoverDay(Today.AddDays(5))).ToHaveAttributeAsync("aria-disabled", "true");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Booking_Refuses_A_Single_Day_Stay()
    {
        await Open("rangepickers");
        var section = Section("Booking");
        await section.Locator(".shiny-rangefield-trigger").ClickAsync();

        // AllowSingleDay=false: tapping the start again does not complete a one-day range.
        await PopoverDay(Today.AddDays(1)).ClickAsync();
        await PopoverDay(Today.AddDays(1)).ClickAsync();
        await Expect(Popover.Locator(".shiny-rangecal-day.is-end")).ToHaveCountAsync(0);

        await PopoverDay(Today.AddDays(3)).ClickAsync();
        await Expect(PopoverDay(Today.AddDays(3))).ToHaveClassAsync(new Regex(@"\bis-end\b"));
        await AssertNoConsoleErrors();
    }


    // ---------------------------------------------------------------- time range picker

    [Test]
    public async Task Time_Picker_Moving_The_Start_Keeps_The_Length()
    {
        await Open("rangepickers");
        var section = Section("Time range picker");
        var trigger = section.Locator(".shiny-rangefield-trigger").First;
        await Expect(trigger.Locator(".shiny-rangefield-text")).ToHaveTextAsync("9:30 AM – 10:30 AM");

        await trigger.ClickAsync();
        var start = Popover.Locator(".shiny-timerange-list[aria-label='Start']");
        await Expect(start.Locator(".shiny-timerange-slot").First).ToHaveTextAsync("8:00 AM");   // MinTime
        await start.Locator(".shiny-timerange-slot", new() { HasTextRegex = new Regex("^11:00 AM$") }).ClickAsync();

        await Expect(Popover.Locator(".shiny-rangefield-summary")).ToHaveTextAsync("11:00 AM – 12:00 PM · 1 hr");
        await PopoverButton("Apply").ClickAsync();

        await Expect(trigger.Locator(".shiny-rangefield-text")).ToHaveTextAsync("11:00 AM – 12:00 PM");
        await Expect(section.Locator("p strong").First).ToHaveTextAsync("11:00 AM – 12:00 PM");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Time_Picker_End_Choices_Show_Their_Length()
    {
        await Open("rangepickers");
        var section = Section("Time range picker");

        await section.Locator(".shiny-rangefield-trigger").First.ClickAsync();
        var end = Popover.Locator(".shiny-timerange-list[aria-label='End']");
        await Expect(end.Locator(".shiny-timerange-slot").First).ToContainTextAsync("9:45 AM");
        await Expect(end.Locator(".shiny-timerange-slot").First.Locator(".shiny-timerange-duration")).ToHaveTextAsync("15 min");

        await end.Locator(".shiny-timerange-slot", new() { HasText = "10:00 AM" }).ClickAsync();
        await Expect(Popover.Locator(".shiny-rangefield-summary")).ToHaveTextAsync("9:30 AM – 10:00 AM · 30 min");
        await AssertNoConsoleErrors();
    }
}
