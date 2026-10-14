namespace Sample.Blazor.UITests;

/// <summary>
/// Loads every gallery page: it must render, the framework must not raise its error bar, and the
/// console must stay free of errors and uncaught exceptions. Run it with SHINY_BLAZOR_MODE=publish to
/// catch what only a trimmed Release build breaks.
/// </summary>
public class RouteSweepTests : BlazorTest
{
    public static IEnumerable<Func<string>> Routes()
        => BlazorRoutes.All.Select(r => (Func<string>)(() => r));

    [Test]
    [MethodDataSource(nameof(Routes))]
    public async Task Page_Renders_Without_Errors(string route)
    {
        await Open(route);

        // let OnAfterRenderAsync JS interop (module imports, observers, canvas setup) run and fail
        await Page.WaitForTimeoutAsync(750);

        await Screenshot(Path.Combine("routes", route.Trim('/') is { Length: > 0 } r ? r : "home"));
        await AssertNoConsoleErrors();
    }
}
