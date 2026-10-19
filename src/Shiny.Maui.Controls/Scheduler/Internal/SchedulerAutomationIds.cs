using System.Globalization;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.Scheduler.Internal;

/// <summary>
/// Keys the scheduler's generated parts by what they show rather than where they sit: days by date,
/// events by <see cref="SchedulerEvent.Identifier"/>. A day's grid position shifts with the month and
/// <c>FirstDayOfWeek</c>, and an event's with whatever else was loaded, so an index would not survive.
/// </summary>
static class SchedulerAutomationIds
{
    /// <summary><c>yyyyMMdd</c>, invariant - a culture with another calendar (th-TH) would change the year.</summary>
    public static string DateKey(DateOnly date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    /// <summary><c>&lt;owner id&gt;&lt;suffix&gt;_&lt;key&gt;</c>, or null when the owner has no id.</summary>
    public static string? For(Element owner, string suffix, string key)
        => AutomationIds.For(owner, suffix) is { } prefix ? $"{prefix}_{key}" : null;

    /// <summary>Names each event view after its owner and the event's identifier.</summary>
    public static void NameEvents(Element owner, IEnumerable<(View View, SchedulerEvent Event)> views)
    {
        foreach (var (view, evt) in views)
            AutomationIds.Set(view, For(owner, SchedulerCalendarView.EventAutomationIdSuffix, evt.Identifier));
    }
}
