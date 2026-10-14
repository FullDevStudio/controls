using System.Globalization;

namespace Sample.UITests.Features;

public class RangePickersTests : UITest
{
    /// <summary>
    /// Opens the page. Shell keeps it for the session, so an earlier test can leave the inline
    /// calendar holding just a start date - the next tap would then complete that range instead of
    /// starting one. Complete it, so every test starts from "a range, or nothing".
    /// </summary>
    async Task OpenPage()
    {
        await Open("rangepickers", "RangePickersPage");
        if (await Driver.GetProperty("IsOpen", automationId: "ReportPicker") == "True")
            await Driver.SetProperty("IsOpen", "False", automationId: "ReportPicker");

        for (var day = 26; day <= 28; day++)
        {
            if (await Driver.GetProperty("StartDate", automationId: "InlineCalendar") == null
                || await Driver.GetProperty("EndDate", automationId: "InlineCalendar") != null)
                break;
            await TapDay(Day(day));
            await Task.Delay(300);
        }
    }

    static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>A day in the month the inline calendar opens on (it opens on the current month).</summary>
    static DateOnly Day(int day) => new(Today.Year, Today.Month, day);


    [Test]
    public async Task Inline_Two_Taps_Select_A_Range()
    {
        await OpenPage();

        await TapDay(Day(10));
        await TapDay(Day(14));

        await Driver.WaitUntil(async () => (await Driver.GetProperty("Text", automationId: "InlineStatus"))?.EndsWith("(5 days)") == true,
            "the inline status to report a five-day range");
        await Assert.That(await Driver.GetProperty("StartDate", automationId: "InlineCalendar")).IsNotNull();
        await Assert.That(await Driver.GetProperty("EndDate", automationId: "InlineCalendar")).IsNotNull();
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Inline_One_Tap_Only_Starts_A_Range()
    {
        await OpenPage();

        var status = await Driver.GetProperty("Text", automationId: "InlineStatus");

        await TapDay(Day(12));

        await Driver.WaitUntil(async () => TryParseDate(await Driver.GetProperty("StartDate", automationId: "InlineCalendar"), out var d) && d == Day(12),
            "the start date to be set");
        await Assert.That(await Driver.GetProperty("EndDate", automationId: "InlineCalendar")).IsNull();
        // RangeSelected only fires on a completed range, so the status keeps whatever it said before.
        await Task.Delay(500);
        await Assert.That(await Driver.GetProperty("Text", automationId: "InlineStatus")).IsEqualTo(status);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Inline_A_Third_Tap_Starts_Over()
    {
        await OpenPage();
        await TapDay(Day(10));
        await TapDay(Day(14));
        await Driver.WaitUntil(async () => await Driver.GetProperty("EndDate", automationId: "InlineCalendar") != null,
            "the range to complete");

        await TapDay(Day(20));

        await Driver.WaitUntil(async () => await Driver.GetProperty("EndDate", automationId: "InlineCalendar") == null,
            "the third tap to clear the end and start a new range");
        await Assert.That(await Driver.GetProperty("StartDate", automationId: "InlineCalendar")).IsNotNull();
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Picker_Opens_And_Cancels_Without_Committing()
    {
        await OpenPage();
        await Driver.WaitForProperty("ReportPicker", "IsOpen", "False");

        await Driver.SetProperty("IsOpen", "True", automationId: "ReportPicker");

        await Driver.WaitForProperty("ReportPicker", "IsOpen", "True");
        await Driver.WaitUntilTextExists("Report period");
        var apply = await FindButton("Apply");
        // An empty draft may be applied (it clears the value); half a range may not.
        await Assert.That(await Driver.GetProperty("IsEnabled", elementId: apply)).IsEqualTo("True");
        await TapPopupDay(Day(12));
        await Driver.WaitUntil(async () => await Driver.GetProperty("IsEnabled", elementId: await FindButton("Apply")) == "False",
            "Apply to disable while only a start is drafted");

        // Closing without Apply drops the draft.
        await Driver.SetProperty("IsOpen", "False", automationId: "ReportPicker");

        await Driver.WaitForProperty("ReportPicker", "IsOpen", "False");
        await Driver.WaitUntil(async () => (await Driver.Query(type: "Button", text: "Apply")).All(b => b.Text != "Apply" || !b.IsVisible),
            "the popup to close");
        await Assert.That(await Driver.GetProperty("StartDate", automationId: "ReportPicker")).IsNull();
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Time_Picker_Shows_A_Bound_Range_Instead_Of_The_Placeholder()
    {
        await OpenPage();
        // Shell keeps the page, so an earlier run may already have bound a range (a TimeSpan? can't be
        // set back to null through the agent): bind one range, then another, and follow the text.
        async Task<string> PickerText()
            => String.Join(" ", (await Driver.QuerySelector("#MeetingPicker Label")).Select(l => l.Text));

        await Driver.SetProperty("StartTime", "13:00:00", automationId: "MeetingPicker");
        await Driver.SetProperty("EndTime", "14:45:00", automationId: "MeetingPicker");
        await Driver.WaitUntil(async () => (await PickerText()).Contains("14:45") || (await PickerText()).Contains("2:45"), "the first range text");

        await Driver.SetProperty("StartTime", "09:00:00", automationId: "MeetingPicker");
        await Driver.SetProperty("EndTime", "10:30:00", automationId: "MeetingPicker");

        await Driver.WaitUntil(async () => (await PickerText()) is var t && t.Contains("10:30") && !t.Contains("Meeting time") && !t.Contains("45"),
            "the picker to show the bound range instead of the placeholder");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Booking_Picker_Carries_Its_Constraints()
    {
        await OpenPage();

        await Driver.WaitForProperty("BookingPicker", "MinDays", "2");
        await Driver.WaitForProperty("BookingPicker", "MaxDays", "14");
        await Driver.WaitForProperty("BookingPicker", "AllowSingleDay", "False");
        await Driver.WaitForProperty("BookingPicker", "AutoApply", "True");
        await Assert.That(await Driver.GetProperty("MinDate", automationId: "BookingPicker")).IsNotNull();
    }


    // ---------------------------------------------------------------- helpers

    async Task<string> FindButton(string text)
    {
        string? id = null;
        await Driver.WaitUntil(async () =>
        {
            id = (await Driver.Query(type: "Button", text: text)).FirstOrDefault(b => b.Text == text && b.IsVisible)?.Id;
            return id != null;
        }, $"a '{text}' button");
        return id!;
    }

    /// <summary>
    /// Taps the inline calendar's cell for a date. Each RangeDayCell is a Grid with a
    /// TapGestureRecognizer and a public Date; only the inline calendar has cells while no picker is open.
    /// </summary>
    async Task TapDay(DateOnly date)
    {
        string? id = null;
        await Driver.WaitUntil(async () =>
        {
            foreach (var cell in await Driver.QuerySelector("#InlineCalendar RangeDayCell"))
            {
                if (TryParseDate(await Driver.GetProperty("Date", elementId: cell.Id), out var d) && d == date)
                {
                    id = cell.Id;
                    return true;
                }
            }
            return false;
        }, $"the day cell for {date:yyyy-MM-dd}");

        await Driver.Tap(elementId: id);
    }

    /// <summary>Taps a day in the open picker's popup: the RangeDayCells that are not the inline calendar's.</summary>
    async Task TapPopupDay(DateOnly date)
    {
        string? id = null;
        await Driver.WaitUntil(async () =>
        {
            var inline = (await Driver.QuerySelector("#InlineCalendar RangeDayCell")).Select(c => c.Id).ToHashSet();
            foreach (var cell in await Driver.Query(type: "RangeDayCell"))
            {
                if (!inline.Contains(cell.Id) && TryParseDate(await Driver.GetProperty("Date", elementId: cell.Id), out var d) && d == date)
                {
                    id = cell.Id;
                    return true;
                }
            }
            return false;
        }, $"the popup's day cell for {date:yyyy-MM-dd}");

        await Driver.Tap(elementId: id);
    }

    /// <summary>The agent formats DateOnly with the device culture's short date pattern.</summary>
    static bool TryParseDate(string? raw, out DateOnly date)
    {
        date = default;
        if (String.IsNullOrWhiteSpace(raw))
            return false;

        foreach (var culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("en-US"), CultureInfo.CurrentCulture })
        {
            if (DateOnly.TryParse(raw, culture, DateTimeStyles.None, out date))
                return true;
        }
        return DateOnly.TryParseExact(raw, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }
}
