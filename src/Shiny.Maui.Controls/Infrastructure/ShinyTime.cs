using Microsoft.Extensions.DependencyInjection;

namespace Shiny.Maui.Controls.Infrastructure;

/// <summary>
/// The clock the controls read: the <see cref="TimeProvider"/> registered in DI, or
/// <see cref="TimeProvider.System"/> when there is none.
/// </summary>
/// <remarks>
/// Controls are built by XAML, so the clock cannot be constructor-injected. It is looked up the way
/// ShinyImage looks up its services: the element's own handler first, then the application's. Register
/// one (<c>builder.Services.AddSingleton&lt;TimeProvider&gt;(...)</c>) to pin "today" for a demo, a
/// screenshot pass or a test.
/// </remarks>
internal static class ShinyTime
{
    /// <summary>The clock for <paramref name="element"/>, falling back to the application's.</summary>
    public static TimeProvider For(Element? element)
    {
        var services = element?.Handler?.MauiContext?.Services
                       ?? Application.Current?.Handler?.MauiContext?.Services;

        return services?.GetService<TimeProvider>() ?? TimeProvider.System;
    }

    /// <summary>The application's clock, for code with no element to hand (a service, a default value).</summary>
    public static TimeProvider Current => For(null);

    /// <summary>Today's local date, as a midnight <see cref="DateTime"/> (what <c>DateTime.Today</c> was).</summary>
    public static DateTime Today(this TimeProvider time) => time.GetLocalNow().Date;

    /// <summary>The local time, as a <see cref="DateTime"/> (what <c>DateTime.Now</c> was).</summary>
    public static DateTime LocalNow(this TimeProvider time) => time.GetLocalNow().DateTime;
}
