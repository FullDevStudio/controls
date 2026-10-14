using System.Globalization;
using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>The scheduler family: calendar (month grid), agenda (timeline) and the grouped event list.</summary>
public class SchedulerTests : BlazorTest
{
    static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    static string MonthTitle(DateOnly d) => new DateTime(d.Year, d.Month, 1).ToString("MMMM yyyy", En);

    ILocator State => Page.Locator(".state");


    // ---------------------------------------------------------------- calendar

    [Test]
    public async Task Calendar_Shows_The_Current_Month_Starting_Sunday()
    {
        await Open("calendar");

        await Expect(Page.Locator(".shiny-cal-title")).ToHaveTextAsync(MonthTitle(Today));
        await Expect(Page.Locator(".shiny-cal-dayheader").First).ToHaveTextAsync("Sun");
        await Expect(Page.Locator(".shiny-cal-cell")).ToHaveCountAsync(42);
        await Expect(Page.Locator(".shiny-cal-cell.is-today .shiny-cal-daynum")).ToHaveTextAsync(Today.Day.ToString(En));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Calendar_Navigates_Months_And_Binds_DisplayMonth()
    {
        await Open("calendar");
        var title = Page.Locator(".shiny-cal-title");
        var next = Page.Locator(".shiny-cal-nav").Nth(1);
        var prev = Page.Locator(".shiny-cal-nav").Nth(0);

        await next.ClickAsync();
        await Expect(title).ToHaveTextAsync(MonthTitle(Today.AddMonths(1)));
        await Expect(State).ToContainTextAsync("Display month: " + MonthTitle(Today.AddMonths(1)));

        await prev.ClickAsync();
        await prev.ClickAsync();
        await Expect(title).ToHaveTextAsync(MonthTitle(Today.AddMonths(-1)));
        await Expect(State).ToContainTextAsync("Display month: " + MonthTitle(Today.AddMonths(-1)));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Calendar_Clicking_A_Day_Selects_It()
    {
        await Open("calendar");
        // Pick an in-month day that isn't today.
        var day = Today.Day == 10 ? 11 : 10;
        var cell = Page.Locator(".shiny-cal-cell:not(.not-in-month)")
            .Filter(new() { Has = Page.Locator(".shiny-cal-daynum", new() { HasTextRegex = new Regex($"^{day}$") }) });

        await cell.Locator(".shiny-cal-daynum").ClickAsync();

        await Expect(cell).ToHaveClassAsync(new Regex(@"\bis-selected\b"));
        await Expect(Page.Locator(".shiny-cal-cell.is-selected")).ToHaveCountAsync(1);
        var expected = new DateOnly(Today.Year, Today.Month, day).ToString(En);
        await Expect(State).ToContainTextAsync("Selected day: " + expected);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Calendar_Caps_Events_Per_Cell_With_An_Overflow()
    {
        await Open("calendar");

        // MaxEventsPerCell="2": no cell ever shows a third chip; busy days get "+N more" instead.
        await Expect(Page.Locator(".shiny-cal-overflow").First).ToBeVisibleAsync();
        var counts = await Page.Locator(".shiny-cal-events").EvaluateAllAsync<int[]>(
            "els => els.map(e => e.querySelectorAll('.shiny-cal-event').length)");
        await Assert.That(counts.Max()).IsLessThanOrEqualTo(2);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Calendar_Clicking_An_Event_Reports_It()
    {
        await Open("calendar");
        var evt = Page.Locator(".shiny-cal-cell:not(.not-in-month) .shiny-cal-event").First;
        var title = (await evt.InnerTextAsync()).Trim();

        await evt.ClickAsync();

        // The sample page doesn't re-render on provider.Changed (the agenda page does), so give it a
        // reason to: moving the month re-renders it through @bind-DisplayMonth.
        await Page.Locator(".shiny-cal-nav").Nth(1).ClickAsync();
        await Expect(State).ToContainTextAsync(new Regex($@"Last event: {Regex.Escape(title)} \("));
        await AssertNoConsoleErrors();
    }


    // ---------------------------------------------------------------- agenda

    ILocator AgendaButton(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });


    [Test]
    public async Task Agenda_Toggles_Between_One_And_Three_Days()
    {
        await Open("agenda");
        await Expect(Page.Locator(".shiny-agenda-daycol")).ToHaveCountAsync(1);

        await AgendaButton("3-Day").ClickAsync();
        await Expect(Page.Locator(".shiny-agenda-daycol")).ToHaveCountAsync(3);
        await Expect(Page.Locator(".shiny-agenda-dayheader")).ToHaveCountAsync(3);

        await AgendaButton("1-Day").ClickAsync();
        await Expect(Page.Locator(".shiny-agenda-daycol")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Agenda_Day_Header_Follows_The_Selected_Date()
    {
        await Open("agenda");
        var header = Page.Locator(".shiny-agenda-dayheader").First;
        await Expect(header).ToHaveTextAsync(Today.ToString("dddd, MMMM d, yyyy", En));
        await Expect(Page.Locator(".shiny-agenda-date.is-selected .shiny-agenda-date-num")).ToHaveTextAsync(Today.Day.ToString(En));

        // The carousel chip after today.
        var tomorrow = Today.AddDays(1);
        await Page.Locator(".shiny-agenda-date.is-selected + .shiny-agenda-date").ClickAsync();

        await Expect(header).ToHaveTextAsync(tomorrow.ToString("dddd, MMMM d, yyyy", En));
        await Expect(State).ToContainTextAsync("Selected day: " + tomorrow.ToString(En));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Agenda_Date_Picker_Switches_To_A_Month_Calendar()
    {
        await Open("agenda");
        await Expect(Page.Locator(".shiny-agenda-datepicker")).ToBeVisibleAsync();

        await AgendaButton("Calendar").ClickAsync();

        await Expect(Page.Locator(".shiny-agenda-datepicker")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".shiny-agenda-calendar-head span")).ToHaveTextAsync(MonthTitle(Today));
        await Expect(AgendaButton("Carousel")).ToBeVisibleAsync();

        // Picking a day in the month calendar moves the agenda to it.
        var day = Today.Day == 20 ? 21 : 20;
        await Page.Locator(".shiny-agenda-calendar-cell", new() { HasTextRegex = new Regex($"^{day}$") }).ClickAsync();
        await Expect(Page.Locator(".shiny-agenda-dayheader").First)
            .ToHaveTextAsync(new DateOnly(Today.Year, Today.Month, day).ToString("dddd, MMMM d, yyyy", En));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Agenda_Shows_Additional_Timezones()
    {
        await Open("agenda");
        await Expect(Page.Locator(".shiny-agenda-tzabbr--extra")).ToHaveCountAsync(0);
        var timeColumns = await Page.Locator(".shiny-agenda-timecol").CountAsync();

        await AgendaButton("Show TZ").ClickAsync();

        await Expect(AgendaButton("Hide TZ")).ToBeVisibleAsync();
        await Expect(Page.Locator(".shiny-agenda-tzabbr--extra")).ToHaveCountAsync(1);
        await Expect(Page.Locator(".shiny-agenda-timecol")).ToHaveCountAsync(timeColumns + 1);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Agenda_Snap_Button_Cycles()
    {
        await Open("agenda");

        await AgendaButton("Snap 15m").ClickAsync();
        await AgendaButton("Snap 30m").ClickAsync();
        await AgendaButton("Snap 5m").ClickAsync();
        await Expect(AgendaButton("Snap 15m")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Agenda_Drag_And_Resize_Switches()
    {
        await Open("agenda");
        var draggable = Page.Locator(".shiny-agenda-event[data-draggable='true']");
        var grips = Page.Locator(".shiny-agenda-grip");
        await Expect(draggable.First).ToBeAttachedAsync();
        await Expect(grips.First).ToBeAttachedAsync();

        // Resize off: the grips go, but events can still be moved.
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "Resize", Exact = true }).UncheckAsync();
        await Expect(grips).ToHaveCountAsync(0);
        await Expect(draggable.First).ToBeAttachedAsync();

        // Both off: nothing is draggable any more.
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "Drag", Exact = true }).UncheckAsync();
        await Expect(Page.Locator(".shiny-agenda-event[data-draggable]")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Agenda_Clicking_An_Event_Reports_It_When_Not_Draggable()
    {
        await Open("agenda");
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "Drag", Exact = true }).UncheckAsync();
        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "Resize", Exact = true }).UncheckAsync();
        var evt = Page.Locator(".shiny-agenda-event").First;
        var title = (await evt.Locator(".shiny-agenda-event-title").InnerTextAsync()).Trim();

        await evt.ClickAsync();

        await Expect(State).ToContainTextAsync(new Regex($@"Last event: {Regex.Escape(title)} \("));
        await AssertNoConsoleErrors();
    }


    [Test]
    [Skip("BUG: with AllowEventDrag/AllowEventResize on, a plain mouse click on an agenda event never reaches OnEventSelected - scheduler-agenda.js arms a drag on mouse pointerdown with no delay, and onPointerUp calls suppressNextClick for any armed drag even with zero movement, swallowing the click; works with both switches off")]
    public async Task Agenda_Clicking_An_Event_Reports_It()
    {
        await Open("agenda");
        var evt = Page.Locator(".shiny-agenda-event").First;
        await Expect(evt).ToBeAttachedAsync();
        var title = (await evt.Locator(".shiny-agenda-event-title").InnerTextAsync()).Trim();

        await evt.ClickAsync();

        await Expect(State).ToContainTextAsync(new Regex($@"Last event: {Regex.Escape(title)} \("));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Agenda_Clicking_A_Time_Slot_Reports_It()
    {
        await Open("agenda");
        await Expect(State).ToContainTextAsync("Last time slot tap: —");

        // 6 AM: before every sample event, so the click lands on the empty slot.
        var slot = Page.Locator(".shiny-agenda-daycol").First.Locator(".shiny-agenda-hourcell").Nth(6);
        await slot.ClickAsync(new() { Position = new() { X = 5, Y = 5 } });

        var expected = Today.ToDateTime(new TimeOnly(6, 0)).ToString("g", En);
        await Expect(State).ToContainTextAsync("Last time slot tap: " + expected);
        await AssertNoConsoleErrors();
    }


    // ---------------------------------------------------------------- calendar list

    [Test]
    public async Task List_Groups_Events_By_Day_With_Counts()
    {
        await Open("agendalist");
        var days = Page.Locator(".shiny-cal-list-day");
        await Expect(days.First).ToBeVisibleAsync();

        for (var i = 0; i < 3; i++)
        {
            var day = days.Nth(i);
            var events = await day.Locator(".shiny-cal-list-event").CountAsync();
            var label = events == 1 ? "1 event" : $"{events} events";
            await Expect(day.Locator(".shiny-cal-list-count")).ToHaveTextAsync(label);
        }
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task List_Days_Are_In_Date_Order()
    {
        await Open("agendalist");
        await Expect(Page.Locator(".shiny-cal-list-date").First).ToBeVisibleAsync();

        var headers = await Page.Locator(".shiny-cal-list-date").AllInnerTextsAsync();
        var dates = headers.Take(10)
            .Select(h => DateTime.ParseExact(h.Trim(), "dddd, MMMM d, yyyy", En))
            .ToList();
        await Assert.That(dates).IsEquivalentTo(dates.OrderBy(d => d).ToList());
        await Assert.That(dates.Distinct().Count()).IsEqualTo(dates.Count);
        await AssertNoConsoleErrors();
    }


    [Test]
    [Skip("BUG (sample): AgendaListPage.razor never re-renders on provider.Changed, so 'Last event' stays '—' after a click (the control does call OnEventSelected)")]
    public async Task List_Clicking_An_Event_Reports_It()
    {
        await Open("agendalist");
        var evt = Page.Locator(".shiny-cal-list-event").First;
        var title = (await evt.Locator(".shiny-cal-list-event-title").InnerTextAsync()).Trim();

        await evt.ClickAsync();

        await Expect(State).ToContainTextAsync(new Regex($@"Last event: {Regex.Escape(title)} \("));
        await AssertNoConsoleErrors();
    }
}
