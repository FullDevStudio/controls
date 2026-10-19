using Microsoft.Extensions.DependencyInjection;

namespace Shiny.Blazor.Controls.Infrastructure;

/// <summary>
/// The clock the components read: the <see cref="TimeProvider"/> registered in DI, or
/// <see cref="TimeProvider.System"/> when there is none.
/// </summary>
/// <remarks>
/// Resolved rather than <c>[Inject]</c>ed, because an injected service that is not registered throws -
/// and almost no app registers a <see cref="TimeProvider"/>. Register one to pin "today" for a demo,
/// a screenshot pass or a test.
/// </remarks>
internal static class ShinyTime
{
    /// <summary>The registered clock, or the system one.</summary>
    public static TimeProvider From(IServiceProvider? services)
        => services?.GetService<TimeProvider>() ?? TimeProvider.System;

    /// <summary>Today's local date, as a midnight <see cref="DateTime"/> (what <c>DateTime.Today</c> was).</summary>
    public static DateTime Today(this TimeProvider time) => time.GetLocalNow().Date;

    /// <summary>The local time, as a <see cref="DateTime"/> (what <c>DateTime.Now</c> was).</summary>
    public static DateTime LocalNow(this TimeProvider time) => time.GetLocalNow().DateTime;
}
