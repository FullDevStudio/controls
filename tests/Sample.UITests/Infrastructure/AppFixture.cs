using TUnit.Core.Interfaces;

namespace Sample.UITests;

/// <summary>
/// The one running app shared by the whole session: built, launched and connected once, then every
/// test navigates inside it. Building per class would cost minutes each; a crash in one test would
/// show as a failed connection in the next, which is what <see cref="UITest"/> checks for.
/// </summary>
public sealed class AppFixture : IAsyncInitializer, IAsyncDisposable
{
    public AppHost Host { get; } = AppHost.Create();
    public MauiDevFlowDriver Driver { get; private set; } = null!;

    /// <summary>Why the head can't run here, or null when it is connected.</summary>
    public string? UnavailableReason { get; private set; }

    public UIPlatform Platform => UITestSettings.Platform;

    public async Task InitializeAsync()
    {
        if (!UITestSettings.Attach && !UITestSettings.CanLaunchOnThisHost)
        {
            UnavailableReason =
                $"The {Platform} head can't be launched from this OS. Start it on its own machine and " +
                "run with SHINY_UITEST_ATTACH=1 and SHINY_UITEST_PORT, or run the suite there.";
            return;
        }

        Directory.CreateDirectory(UITestSettings.ResultsDirectory);

        if (!UITestSettings.Attach)
        {
            if (!UITestSettings.NoBuild)
                await Host.BuildAsync();

            await Host.LaunchAsync();
        }

        var port = await Host.ResolvePortAsync(TimeSpan.FromSeconds(120));
        Driver = new MauiDevFlowDriver(Path.Combine(UITestSettings.ResultsDirectory, "screenshots"))
        {
            Port = port,
            TargetPlatform = Platform
        };
        await Driver.WaitForAgent(timeoutSeconds: 180);
        await Driver.WaitUntilExists("HomePage", timeoutSeconds: 60);
    }

    /// <summary>
    /// Relaunches the app if a previous test crashed it — or left Shell unable to navigate (see
    /// <see cref="RequestRelaunch"/>) — so one bad page fails one test rather than every test after
    /// it. Returns false when it can't be brought back (attach mode).
    /// </summary>
    public async Task<bool> EnsureRunningAsync()
    {
        var alive = await Driver.IsAlive();
        if (alive && !relaunchRequested)
            return true;

        if (UITestSettings.Attach)
            return alive;

        relaunchRequested = false;
        Host.AgentSince = DateTimeOffset.UtcNow.AddSeconds(-2);
        await Host.LaunchAsync();
        Driver.Port = await Host.ResolvePortAsync(TimeSpan.FromSeconds(120));
        await Driver.WaitForAgent(timeoutSeconds: 120);
        await Driver.WaitUntilExists("HomePage", timeoutSeconds: 60);
        Relaunches++;
        return true;
    }

    /// <summary>
    /// Asks for a fresh app before the next test. A page that fails to build can leave Shell stuck
    /// on an empty route (AppKit especially) — the app is alive but no navigation ever lands again.
    /// </summary>
    public void RequestRelaunch() => relaunchRequested = true;

    bool relaunchRequested;

    public int Relaunches { get; private set; }

    public async ValueTask DisposeAsync()
    {
        if (Driver != null)
            await Driver.DisposeAsync();

        await Host.DisposeAsync();
    }
}
