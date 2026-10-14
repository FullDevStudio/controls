namespace Sample.UITests;

/// <summary>
/// Base for every UI test. All tests share one running app (<see cref="AppFixture"/>) and run one at
/// a time — there is a single UI to drive. Each test starts by navigating to its own page, so order
/// never matters; on failure a screenshot and the app's error log are saved next to the results.
/// </summary>
public abstract class UITest
{
    [ClassDataSource<AppFixture>(Shared = SharedType.PerTestSession)]
    public required AppFixture App { get; init; }

    protected MauiDevFlowDriver Driver => App.Driver;
    protected UIPlatform Platform => App.Platform;

    protected bool IsMobile => Platform is UIPlatform.iOS or UIPlatform.Android;
    protected bool IsDesktop => !IsMobile;

    /// <summary>
    /// When the current test started, for scoping the app's log to it. Seeded at construction because
    /// <see cref="PrepareApp"/> can throw before it assigns (a relaunch that never comes back): left at
    /// default, the <c>AddSeconds(-1)</c> below overflows DateTimeOffset.MinValue and the After(Test)
    /// hook reports that instead of the failure that actually mattered.
    /// </summary>
    protected DateTimeOffset TestStarted { get; private set; } = DateTimeOffset.UtcNow;

    [Before(Test)]
    public async Task PrepareApp()
    {
        if (App.UnavailableReason != null)
            Skip.Test(App.UnavailableReason);

        if (!await App.EnsureRunningAsync())
            throw new InvalidOperationException("The app is no longer running (it crashed in an earlier test) and attach mode can't relaunch it.");

        TestStarted = DateTimeOffset.UtcNow;
        App.Host.TakeOutputErrors(); // drop output from before this test
        Timeline($"start {TestContext.Current?.Metadata.TestDetails.ClassType.Name}.{TestContext.Current?.Metadata.DisplayName}");
    }

    /// <summary>
    /// One line per test start/end in uitest-results/&lt;platform&gt;/timeline.log. Successful tests print nothing
    /// to the console, so this is how a device-side event (a GC storm in logcat, a crash) is matched to the
    /// test that was running at that moment.
    /// </summary>
    internal static void Timeline(string line)
    {
        try
        {
            Directory.CreateDirectory(UITestSettings.ResultsDirectory);
            File.AppendAllText(Path.Combine(UITestSettings.ResultsDirectory, "timeline.log"), $"{DateTimeOffset.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
    }

    [After(Test)]
    public async Task CollectEvidence(TestContext context)
    {
        Timeline($"end   {context.Metadata.TestDetails.ClassType.Name}.{context.Metadata.DisplayName} {context.Execution.Result?.State}");

        // Driver is null when the fixture itself failed (build/launch) — that error is already the test's.
        if (App.UnavailableReason != null || App.Driver == null || context.Execution.Result?.State != TestState.Failed)
            return;

        var name = Sanitize(context.Metadata.DisplayName);
        var dir = Path.Combine(UITestSettings.ResultsDirectory, "failures");
        Directory.CreateDirectory(dir);

        if (!await Driver.IsAlive())
        {
            await File.WriteAllTextAsync(Path.Combine(dir, name + ".txt"), "The app was not responding after this test — it most likely crashed or hung here.");
            return;
        }

        // A navigation that never landed usually means Shell is wedged (a page that failed to build
        // leaves it on an empty route), and every later test would fail the same way. A page that is
        // gone while its route is current wedges just that page the same way — on Windows one dead
        // ButtonsPage failed the six tests after it, none of which relaunched.
        var failure = context.Execution.Result?.Exception?.ToString();
        if (failure != null
            && (failure.Contains(MauiDevFlowDriver.NavigationTimeoutMessage)
                || failure.Contains(MauiDevFlowDriver.PageMissingMessage)))
            App.RequestRelaunch();

        try { await Driver.Screenshot(Path.Combine("..", "failures", name + ".png")); } catch { }

        var errors = await Driver.GetErrors(TestStarted.AddSeconds(-1));
        if (errors.Count > 0)
            await File.WriteAllLinesAsync(Path.Combine(dir, name + ".log"), errors.Select(e => e.ToString()));
    }

    /// <summary>Navigates to a Shell route and waits for its page type (or a marker AutomationId) to be on screen.</summary>
    protected async Task Open(string route, string? pageType = null)
    {
        // Android: the Office editors need ~20k JNI global refs just to open (every ribbon tab built up front,
        // hundreds of GraphicsView icons), so a first visit on top of ~20 retained pages crosses monodroid's
        // GREF threshold and the app stalls in back-to-back full GCs. Give them a fresh process.
        if (Platform == UIPlatform.Android
            && HeavyAndroidRoutes.Contains(route)
            && !Driver.RoutesSinceConnect.Contains(route)
            && Driver.RoutesSinceConnect.Count > 3)
        {
            App.RequestRelaunch();
            await App.EnsureRunningAsync();
        }

        await Driver.NavigateTo(route, pageType);
    }

    static readonly HashSet<string> HeavyAndroidRoutes =
        ["documenteditor", "slideeditor", "spreadsheet", "notebook", "documentviewer", "slideviewer"];

    /// <summary>Fails the test with the app's own error log lines written since the test started.</summary>
    protected async Task AssertNoAppErrors(Func<AppLogError, bool>? ignore = null)
    {
        var output = App.Host.TakeOutputErrors();

        // a page that failed to build can leave Shell unable to navigate anywhere else
        if (output.Any(line => line.Contains("Failed to create page view")))
            App.RequestRelaunch();

        var errors = (await Driver.GetErrors(TestStarted.AddSeconds(-1)))
            .Where(e => !IsWinUiFontResolutionFailure(e))
            .Where(e => ignore == null || !ignore(e))
            .Select(e => e.ToString())
            .Concat(output.Select(line => "[stdout] " + line))
            .ToList();

        if (errors.Count > 0)
            Assert.Fail("The app logged errors:\n" + String.Join("\n", errors.Take(10)));
    }

    /// <summary>
    /// MAUI's WinUI font services resolve a font *family name* as though it were an embedded asset path:
    /// <c>FontManager.FindFontFamilyName</c> and <c>FontImageSourceService.GetFontSource</c> call
    /// <c>Uri.LocalPath</c> on it and throw "This operation is not supported for a relative URI". It happens
    /// for every system face the Office editors and the font picker legitimately offer — Calibri, Calibri
    /// Light, Consolas, Courier, monospace — and for glyph image sources on several other pages.
    ///
    /// Nothing in this repo causes it and nothing here can fix it; it is logged at Error, so without this
    /// <see cref="AssertNoAppErrors"/> fails eleven otherwise-passing tests on Windows alone. Scoped to
    /// Windows and to those two categories, so every other head stays strict and a real font failure
    /// elsewhere still fails the test.
    /// </summary>
    static bool IsWinUiFontResolutionFailure(AppLogError error)
        => UITestSettings.Platform == UIPlatform.Windows
            && error.Category.StartsWith("Microsoft.Maui.Font", StringComparison.Ordinal)
            && error.Exception?.Contains("not supported for a relative URI", StringComparison.Ordinal) == true;

    /// <summary>Saves a screenshot under the run's results folder, named per platform for side-by-side review.</summary>
    protected Task<string> Screenshot(string name)
        => Driver.Screenshot(name.EndsWith(".png") ? name : name + ".png");

    static string Sanitize(string value)
    {
        foreach (var c in Path.GetInvalidFileNameChars().Append(' '))
            value = value.Replace(c, '_');

        return value.Length > 120 ? value[..120] : value;
    }
}
