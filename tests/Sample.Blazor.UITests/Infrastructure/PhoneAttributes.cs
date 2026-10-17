namespace Sample.Blazor.UITests;

/// <summary>
/// Skips a test when the whole session runs as a phone (SHINY_BLAZOR_DEVICE) — for a gesture a phone
/// does not have (a mouse wheel, a hover) or a control that deliberately shows less on a narrow screen.
/// Give the reason: a skip that hides a real mobile bug is worse than a failure.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class SkipOnPhoneAttribute(string reason) : SkipAttribute($"Not on a phone: {reason}")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context)
        => Task.FromResult(BlazorTestSettings.Device != null);
}
