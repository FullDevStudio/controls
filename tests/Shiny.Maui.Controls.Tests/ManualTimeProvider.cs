namespace Shiny.Maui.Controls.Tests;

/// <summary>A clock that only moves when a test moves it.</summary>
sealed class ManualTimeProvider(DateTimeOffset? start = null) : TimeProvider
{
    DateTimeOffset now = start ?? new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => this.now;

    public void Advance(TimeSpan by) => this.now += by;
}
