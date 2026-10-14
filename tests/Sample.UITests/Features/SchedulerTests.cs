using System.Globalization;

namespace Sample.UITests.Features;

/// <summary>
/// The scheduler family: month calendar (calendar), agenda timeline (agenda), agenda with the
/// month-calendar date picker (agendacalendarpicker) and the grouped event list (calendarlist).
/// </summary>
/// <remarks>
/// Month and day header text is formatted by the device culture; these expectations assume en-US,
/// which is what the simulators and the desktop heads run under.
/// </remarks>
public class SchedulerTests : UITest
{
    static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");
    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);
    static string MonthTitle(DateOnly d) => new DateTime(d.Year, d.Month, 1).ToString("MMMM yyyy", En);
    static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>The next <paramref name="day"/> strictly after today.</summary>
    static DateOnly Next(DayOfWeek day)
    {
        var d = Today.AddDays(1);
        while (d.DayOfWeek != day)
            d = d.AddDays(1);
        return d;
    }

    async Task<bool> HasText(string exact)
        => (await Driver.Query(text: exact)).Any(e => e.Text == exact);

    async Task<string> FindButton(string exactText)
    {
        string? id = null;
        await Driver.WaitUntil(async () =>
        {
            id = (await Driver.Query(type: "Button", text: exactText)).FirstOrDefault(b => b.Text == exactText)?.Id;
            return id != null;
        }, $"a '{exactText}' button");
        return id!;
    }


    // ---------------------------------------------------------------- calendar

    [Test]
    public async Task Calendar_Next_And_Previous_Change_The_Month()
    {
        await Open("calendar", "CalendarPage");
        await Driver.WaitUntil(() => HasText(MonthTitle(Today)), "the current month title");

        await Driver.Tap(elementId: await FindButton(">"));
        await Driver.WaitUntil(() => HasText(MonthTitle(Today.AddMonths(1))), "next month's title");

        await Driver.Tap(elementId: await FindButton("<"));
        await Driver.Tap(elementId: await FindButton("<"));
        await Driver.WaitUntil(() => HasText(MonthTitle(Today.AddMonths(-1))), "last month's title");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Calendar_DisplayMonth_Drives_The_Grid()
    {
        await Open("calendar", "CalendarPage");

        await Driver.SetProperty("DisplayMonth", "2025-03-01", automationId: "SchedulerCalendar");

        await Driver.WaitUntil(() => HasText("March 2025"), "the March 2025 title");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Calendar_MaxDate_Disables_Next()
    {
        await Open("calendar", "CalendarPage");
        // the calendar's own "next" button - other pages Shell keeps alive have ">" buttons too
        async Task<string?> NextEnabled()
        {
            var next = (await Driver.QuerySelector("#SchedulerCalendar Button[Text=\">\"]")).FirstOrDefault();
            return next == null ? null : await Driver.GetProperty("IsEnabled", elementId: next.Id);
        }

        // show this month, with no upper bound (a MaxDate can't be cleared to null through the agent)
        await Driver.SetProperty("MaxDate", "2100-12-31", automationId: "SchedulerCalendar");
        await Driver.SetProperty("DisplayMonth", Iso(new DateOnly(Today.Year, Today.Month, 1)), automationId: "SchedulerCalendar");
        await Driver.WaitUntil(async () => await NextEnabled() == "True", "next to start enabled");

        var lastOfMonth = new DateOnly(Today.Year, Today.Month, DateTime.DaysInMonth(Today.Year, Today.Month));
        try
        {
            await Driver.SetProperty("MaxDate", Iso(lastOfMonth), automationId: "SchedulerCalendar");
            await Driver.WaitUntil(async () => await NextEnabled() == "False", "next to be disabled at MaxDate");
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.SetProperty("MaxDate", "2100-12-31", automationId: "SchedulerCalendar");
        }
    }


    [Test]
    public async Task Calendar_Loads_Events_Into_Cells()
    {
        await Open("calendar", "CalendarPage");

        // Every month has weekdays, and every weekday has the stand-up.
        await Driver.WaitUntil(() => HasText("Team Standup"), "the provider's events in the month grid", timeoutSeconds: 15);
        await AssertNoAppErrors();
    }


    // ---------------------------------------------------------------- agenda

    [Test]
    public async Task Agenda_Days_Button_Toggles_One_And_Three_Days()
    {
        await Open("agenda", "AgendaPage");
        await Driver.WaitForProperty("AgendaView", "DaysToShow", "1");

        await Driver.Tap("AgendaDaysButton");
        await Driver.WaitForProperty("AgendaView", "DaysToShow", "3");
        await Driver.WaitForProperty("AgendaDaysButton", "Text", "1-Day");

        await Driver.Tap("AgendaDaysButton");
        await Driver.WaitForProperty("AgendaView", "DaysToShow", "1");
        await Driver.WaitForProperty("AgendaDaysButton", "Text", "3-Day");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Agenda_Timezone_And_Picker_Mode_Buttons()
    {
        await Open("agenda", "AgendaPage");

        await Driver.Tap("AgendaTimezoneButton");
        await Driver.WaitForProperty("AgendaView", "ShowAdditionalTimezones", "True");
        await Driver.WaitForProperty("AgendaTimezoneButton", "Text", "Hide TZ");

        await Driver.Tap("AgendaPickerModeButton");
        await Driver.WaitForProperty("AgendaView", "DatePickerMode", "Calendar");
        await Driver.WaitForProperty("AgendaPickerModeButton", "Text", "Carousel");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Agenda_Snap_Cycles_And_Switches_Reach_The_View()
    {
        await Open("agenda", "AgendaPage");
        await Driver.WaitForProperty("AgendaView", "DragSnapMinutes", "15");

        await Driver.Tap("AgendaSnapButton");
        await Driver.WaitForProperty("AgendaView", "DragSnapMinutes", "30");
        await Driver.WaitForProperty("AgendaSnapButton", "Text", "Snap 30m");
        await Driver.Tap("AgendaSnapButton");
        await Driver.WaitForProperty("AgendaView", "DragSnapMinutes", "5");

        await Driver.WaitForProperty("AgendaView", "AllowEventDrag", "True");
        await Driver.Tap("AgendaDragSwitch");
        await Driver.WaitForProperty("AgendaView", "AllowEventDrag", "False");

        await Driver.Tap("AgendaResizeSwitch");
        await Driver.WaitForProperty("AgendaView", "AllowEventResize", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Agenda_Selected_Date_Loads_That_Days_Events()
    {
        await Open("agenda", "AgendaPage");

        // Design Review is a Wednesday-only event: absent on a Thursday, present on a Wednesday.
        await Driver.SetProperty("SelectedDate", Iso(Next(DayOfWeek.Thursday)), automationId: "AgendaView");
        await Driver.WaitUntil(async () => await HasText("Team Standup") && !await HasText("Design Review"),
            "Thursday's events", timeoutSeconds: 15);

        await Driver.SetProperty("SelectedDate", Iso(Next(DayOfWeek.Wednesday)), automationId: "AgendaView");
        await Driver.WaitUntil(() => HasText("Design Review"), "Wednesday's events", timeoutSeconds: 15);
        await AssertNoAppErrors();
    }


    // ---------------------------------------------------------------- agenda + calendar picker

    [Test]
    public async Task Calendar_Picker_Days_Button_Toggles()
    {
        await Open("agendacalendarpicker", "AgendaCalendarPickerPage");
        await Driver.WaitForProperty("PickerAgendaView", "DatePickerMode", "Calendar");

        await Driver.Tap("PickerDaysButton");
        await Driver.WaitForProperty("PickerAgendaView", "DaysToShow", "3");
        await Driver.WaitForProperty("PickerDaysButton", "Text", "1-Day");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Calendar_Picker_Selected_Date_Loads_That_Days_Events()
    {
        await Open("agendacalendarpicker", "AgendaCalendarPickerPage");

        await Driver.SetProperty("SelectedDate", Iso(Next(DayOfWeek.Friday)), automationId: "PickerAgendaView");

        // Sprint Retro is Friday-only.
        await Driver.WaitUntil(() => HasText("Sprint Retro"), "Friday's events", timeoutSeconds: 15);
        await AssertNoAppErrors();
    }


    // ---------------------------------------------------------------- calendar list

    [Test]
    public async Task List_Loads_Grouped_Events()
    {
        await Open("calendarlist", "CalendarListPage");

        await Driver.WaitUntil(() => HasText("Team Standup"), "the provider's events", timeoutSeconds: 15);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task List_Selected_Date_Reloads_Around_It()
    {
        await Open("calendarlist", "CalendarListPage");
        // the list's own labels only - other pages Shell keeps alive mention this year too
        async Task<int> Headers(string year)
            => (await Driver.QuerySelector("#SchedulerCalendarList Label")).Count(l => l.Text?.Contains(year) == true);

        var thisYear = ", " + Today.Year.ToString(CultureInfo.InvariantCulture);
        await Driver.SetProperty("SelectedDate", Iso(Today), automationId: "SchedulerCalendarList");
        await Driver.WaitUntil(async () => await Headers(thisYear) > 0, "this year's day headers", timeoutSeconds: 15);

        // Far enough out that the whole loaded window (±15 days) is in a later year.
        var target = Today.AddDays(400);
        var targetYear = ", " + target.Year.ToString(CultureInfo.InvariantCulture);
        try
        {
            await Driver.SetProperty("SelectedDate", Iso(target), automationId: "SchedulerCalendarList");
            await Driver.WaitUntil(async () => await Headers(targetYear) > 0 && await Headers(thisYear) == 0,
                "the list to reload around the new date", timeoutSeconds: 15);
            await AssertNoAppErrors();
        }
        finally
        {
            await Driver.SetProperty("SelectedDate", Iso(Today), automationId: "SchedulerCalendarList");
        }
    }
}
