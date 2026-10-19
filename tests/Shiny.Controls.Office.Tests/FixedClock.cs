namespace Shiny.Controls.Office.Tests;

/// <summary>A clock stopped at <paramref name="now"/>.</summary>
sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
