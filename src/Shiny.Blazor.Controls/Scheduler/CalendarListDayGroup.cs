using Shiny.Blazor.Controls.Infrastructure;

namespace Shiny.Blazor.Controls.Scheduler;

public class CalendarListDayGroup : List<SchedulerEvent>
{
    public CalendarListDayGroup(DateOnly date, IEnumerable<SchedulerEvent> events, TimeProvider? time = null) : base(events)
    {
        Date = date;
        DateDisplay = date.ToString("dddd, MMMM d, yyyy");
        IsToday = date == DateOnly.FromDateTime((time ?? TimeProvider.System).Today());
        EventCountDisplay = Count == 1 ? "1 event" : $"{Count} events";
    }

    public DateOnly Date { get; }
    public string DateDisplay { get; }
    public bool IsToday { get; }
    public string EventCountDisplay { get; }
}
