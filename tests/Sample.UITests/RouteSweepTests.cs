namespace Sample.UITests;

/// <summary>
/// Opens every page in the head's Shell, one test per route: the page must build, Shell must land on
/// it, the app must still be alive, and nothing may be logged at Error. This is the net that catches a
/// page that throws in its constructor, a DI registration a head forgot, or a control that crashes on
/// one platform only — before any per-control test even looks at behaviour.
/// </summary>
public class RouteSweepTests : UITest
{
    public static IEnumerable<Func<ShellRoute>> Routes()
        => ShellRoutes.ForCurrentPlatform().Select(r => (Func<ShellRoute>)(() => r));

    [Test]
    [MethodDataSource(nameof(Routes))]
    public async Task Page_Opens_Without_Errors(ShellRoute route)
    {
        if (KnownGaps.ForRoute(route.Route) is string gap)
            Skip.Test("Known gap on " + Platform + ": " + gap);

        await Open(route.Route, route.PageType);

        // give deferred work (handlers attaching, OnAppearing loads, first layout) a moment to fail
        await Task.Delay(750);

        await Assert.That(await Driver.IsAlive()).IsTrue();
        try
        {
            await Screenshot(Path.Combine("routes", route.Route));
        }
        catch (Exception ex)
        {
            // evidence only — a capture hiccup is not a page failure
            Console.WriteLine($"Screenshot of {route.Route} failed: {ex.Message}");
        }
        await AssertNoAppErrors();
    }
}
