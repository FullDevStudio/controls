namespace Sample.UITests;

/// <summary>Skips a test or class on the listed heads — for a feature a head does not ship, or a known head bug.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class SkipOnAttribute(UIPlatform platform, string reason)
    : SkipAttribute($"Not on {platform}: {reason}")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context)
        => Task.FromResult(UITestSettings.Platform == platform);
}

/// <summary>Runs a test or class only on the listed heads (camera hardware, a mobile-only gesture, …).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class OnlyOnAttribute(params UIPlatform[] platforms)
    : SkipAttribute($"Only on {String.Join(", ", platforms)}")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context)
        => Task.FromResult(!platforms.Contains(UITestSettings.Platform));
}
