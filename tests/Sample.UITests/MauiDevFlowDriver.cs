using System.Text.Json;
using Microsoft.Maui.DevFlow.Driver;

namespace Sample.UITests;

/// <summary>A log line the app wrote at Error or above.</summary>
public sealed record AppLogError(DateTimeOffset Time, string Level, string Category, string Message, string? Exception)
{
    public override string ToString()
        => $"[{Level}] {Category}: {Message}{(Exception == null ? "" : "\n" + Exception)}";
}

/// <summary>
/// Thin, retrying layer over the DevFlow <see cref="AgentClient"/>. Everything that looks an element
/// up re-queries right before acting: element ids are regenerated per tree capture, so an id held
/// across an await can be stale by the time it is used.
/// </summary>
public class MauiDevFlowDriver : IAsyncDisposable
{
    readonly string screenshotDir;
    AgentClient? client;

    public UIPlatform? TargetPlatform { get; init; }
    public int Port { get; set; } = 9223;

    public MauiDevFlowDriver(string? screenshotDir = null)
    {
        this.screenshotDir = screenshotDir ?? Path.Combine(AppContext.BaseDirectory, "screenshots");
        Directory.CreateDirectory(this.screenshotDir);
    }

    public AgentClient Client => client ?? throw new InvalidOperationException("Driver not connected. Call WaitForAgent first.");

    public ValueTask DisposeAsync()
    {
        client?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Distinct Shell routes opened since the agent was last (re)connected. Shell keeps every page it has
    /// shown alive, so this is how much UI the process is holding — on Android each page pins hundreds of
    /// JNI global references, and the head relaunches before the runtime's GREF limit cripples it.
    /// </summary>
    public IReadOnlyCollection<string> RoutesSinceConnect => routesSinceConnect;
    readonly HashSet<string> routesSinceConnect = new(StringComparer.Ordinal);

    public async Task WaitForAgent(int timeoutSeconds = 120)
    {
        routesSinceConnect.Clear();
        client?.Dispose();
        client = new AgentClient("localhost", Port);
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await client.GetStatusAsync();
                return;
            }
            catch (Exception ex)
            {
                lastException = ex;
                await Task.Delay(1000);
            }
        }

        throw new TimeoutException($"DevFlow agent on port {Port} did not respond within {timeoutSeconds}s", lastException);
    }

    /// <summary>True while the agent still answers — false means the app died or its UI thread is wedged.</summary>
    public async Task<bool> IsAlive()
    {
        try
        {
            // A wedged UI thread (GTK spinning inside a native call) keeps the process alive and the port
            // open but never answers; without a cap every later test hangs on it instead of relaunching.
            await Client.GetStatusAsync().WaitAsync(TimeSpan.FromSeconds(10));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<AgentStatus> Status() => Client.GetStatusAsync();

    // ── navigation ──────────────────────────────────────────────────────────

    public Task Navigate(string route)
        => Client.NavigateAsync(route);

    /// <summary>
    /// Navigates to an absolute Shell route and waits until the Shell reports it as current and the
    /// page's own type is in the tree. A Shell that silently refuses (an unresolvable page wedges it
    /// on AppKit) fails here instead of letting the next assertion read the previous page.
    /// </summary>
    public async Task NavigateTo(string route, string? pageType = null, int? timeoutSeconds = null)
    {
        var timeout = timeoutSeconds ?? DefaultNavigationTimeoutSeconds;
        var target = route.TrimStart('/');
        routesSinceConnect.Add(target);
        await RideOutLease(() => Client.NavigateAsync("//" + target));

        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        string? current = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                current = (await Client.GetStatusAsync()).Route;
                if (current != null && RouteMatches(current, target))
                    break;
            }
            catch
            {
                // agent busy laying the page out; retry
            }
            await Task.Delay(300);
        }

        if (current == null || !RouteMatches(current, target))
            throw new TimeoutException($"{NavigationTimeoutMessage} '//{target}' within {timeout}s (current route: '{current}')");

        if (pageType != null)
            await WaitUntilTypeExists(pageType, timeout);
    }

    /// <summary>
    /// Another DevFlow session (the CLI, a coding agent) holding the app's mutation lease makes a
    /// mutation throw rather than return false. The lease is short-lived, so wait it out.
    /// </summary>
    static async Task RideOutLease(Func<Task> action, int timeoutSeconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (true)
        {
            try
            {
                await action();
                return;
            }
            catch (Exception ex) when (ex.GetType().Name == "MutationLeaseException" && DateTime.UtcNow < deadline)
            {
                await Task.Delay(1000);
            }
        }
    }

    public const string NavigationTimeoutMessage = "Shell did not reach";

    /// <summary>
    /// The Android emulator builds the heavy pages slowly: after the other Office pages, //documenteditor's
    /// navigate call blocks ~23s and the page shows ~27s in (a 13s main-thread stall, "Skipped 766 frames"),
    /// right at the 30s every other head gets. A real wedge still fails, just later.
    /// </summary>
    int DefaultNavigationTimeoutSeconds => TargetPlatform == UIPlatform.Android ? 60 : 30;

    static bool RouteMatches(string current, string target)
        => current.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() == target.Split('/').Last()
            || current.Contains("/" + target, StringComparison.Ordinal);

    public async Task Back()
        => await Client.BackAsync();

    // ── querying ────────────────────────────────────────────────────────────

    public async Task<List<ElementInfo>> Tree(int depth = 15)
        => await Client.GetTreeAsync(maxDepth: depth);

    public async Task<List<ElementInfo>> Query(string? automationId = null, string? type = null, string? text = null)
    {
        try
        {
            return await Client.QueryAsync(type: type, automationId: automationId, text: text);
        }
        catch
        {
            return [];
        }
    }

    async Task<ElementInfo> FindElementAsync(string? automationId = null, string? type = null, string? text = null, int timeoutSeconds = 30, int index = 0)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        Exception? lastException = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var results = await Client.QueryAsync(type: type, automationId: automationId, text: text);
                if (results.Count > index)
                    return results[index];
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
            await Task.Delay(400);
        }

        throw new TimeoutException(
            $"Element (automationId '{automationId}', type '{type}', text '{text}', index {index}) not found within {timeoutSeconds}s",
            lastException
        );
    }

    /// <summary>
    /// Elements matching a CSS-style selector — descendant (<c>DialogView Button</c>), child (<c>&gt;</c>),
    /// attribute (<c>Label[Text="Hi"]</c>) and <c>:visible</c>. Scoping to an ancestor is how you find an
    /// element that has no AutomationId of its own (a dialog's buttons, a toast's label). Empty on error.
    /// </summary>
    public async Task<List<ElementInfo>> QuerySelector(string selector)
    {
        try
        {
            return await Client.QueryCssAsync(selector);
        }
        catch
        {
            return [];
        }
    }

    /// <summary>Waits for the <paramref name="index"/>th element matching a CSS-style selector.</summary>
    public async Task<ElementInfo> FindBySelector(string selector, int index = 0, int timeoutSeconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var results = await QuerySelector(selector);
            if (results.Count > index)
                return results[index];

            await Task.Delay(400);
        }
        throw new TimeoutException($"Element matching selector '{selector}' (index {index}) not found within {timeoutSeconds}s");
    }

    /// <summary>A selector attribute value, quoted and escaped: <c>Label[Text={Quote(text)}]</c>.</summary>
    public static string Quote(string value)
        => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    public Task<ElementInfo> Find(string automationId, int timeoutSeconds = 30)
        => FindElementAsync(automationId: automationId, timeoutSeconds: timeoutSeconds);

    public Task<ElementInfo> FindByType(string type, int index = 0, int timeoutSeconds = 30)
        => FindElementAsync(type: type, index: index, timeoutSeconds: timeoutSeconds);

    public Task<ElementInfo> FindByText(string text, int timeoutSeconds = 30)
        => FindElementAsync(text: text, timeoutSeconds: timeoutSeconds);

    public async Task<List<ElementInfo>> WaitUntilExists(string automationId, int timeoutSeconds = 30)
        => [await FindElementAsync(automationId: automationId, timeoutSeconds: timeoutSeconds)];

    public Task<ElementInfo> WaitUntilTypeExists(string type, int timeoutSeconds = 30)
        => FindElementAsync(type: type, timeoutSeconds: timeoutSeconds);

    public Task<ElementInfo> WaitUntilTextExists(string text, int timeoutSeconds = 30)
        => FindElementAsync(text: text, timeoutSeconds: timeoutSeconds);

    public async Task WaitUntilGone(string automationId, int timeoutSeconds = 30)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            var results = await Query(automationId: automationId);
            if (results.Count == 0 || results.All(r => !r.IsVisible))
                return;

            await Task.Delay(400);
        }
        throw new TimeoutException($"Element with AutomationId '{automationId}' still present after {timeoutSeconds}s");
    }

    public async Task<bool> IsElementVisible(string automationId)
    {
        var results = await Query(automationId: automationId);
        return results.Any(r => r.IsVisible);
    }

    static readonly HashSet<string> ShellStructure = ["Window", "FlyoutItem", "ShellItem", "ShellSection", "ShellContent", "Tab", "TabBar"];

    /// <summary>
    /// True when an element with this AutomationId is actually on screen: it and every ancestor are
    /// visible. <see cref="IsElementVisible"/> reads the element's own IsVisible only, so a child of a
    /// hidden container (a hidden bar's buttons) still reads visible there. Shell's structural nodes
    /// (FlyoutItem, ShellContent, …) always report invisible and are skipped.
    /// </summary>
    public async Task<bool> IsDisplayed(string automationId)
    {
        List<ElementInfo> roots;
        try
        {
            roots = await Client.GetTreeAsync(maxDepth: 80);
        }
        catch
        {
            return false;
        }

        bool Walk(IEnumerable<ElementInfo>? items, bool ancestorsShown)
        {
            foreach (var e in items ?? [])
            {
                var shown = ancestorsShown && (e.IsVisible || ShellStructure.Contains(e.Type ?? ""));
                if (e.AutomationId == automationId && shown)
                    return true;
                if (Walk(e.Children, shown))
                    return true;
            }
            return false;
        }
        return Walk(roots, true);
    }

    public async Task<bool> Exists(string automationId)
        => (await Query(automationId: automationId)).Count > 0;

    /// <summary>Polls until a property reads <paramref name="expected"/> — state changes land on the UI thread asynchronously.</summary>
    public async Task<string?> WaitForProperty(string automationId, string property, string expected, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        string? value = null;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                value = await GetProperty(property, automationId: automationId);
                if (String.Equals(value, expected, StringComparison.OrdinalIgnoreCase))
                    return value;
            }
            catch
            {
            }
            await Task.Delay(300);
        }
        throw new TimeoutException($"'{automationId}'.{property} was '{value}', expected '{expected}' within {timeoutSeconds}s");
    }

    // ── acting ──────────────────────────────────────────────────────────────

    async Task<string> ResolveId(string? automationId, string? elementId, string? type = null, int? index = null, string? text = null)
    {
        if (elementId != null)
            return elementId;

        if (automationId == null && type == null && text == null)
            throw new InvalidOperationException("No element identifier provided");

        return (await FindElementAsync(automationId, type, text, timeoutSeconds: 10, index: index ?? 0)).Id;
    }

    /// <summary>Taps, retrying against a fresh id if the first went stale or the mutation lease was briefly held elsewhere.</summary>
    public async Task Tap(string? automationId = null, string? elementId = null, string? type = null, int? index = null, string? text = null)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(750 * attempt);

            var id = await ResolveId(automationId, elementId, type, index, text);
            var tapped = false;
            await RideOutLease(async () => tapped = await Client.TapAsync(id));
            if (tapped)
                return;
        }
        throw new InvalidOperationException($"Tap failed on '{automationId ?? type ?? text ?? elementId}'");
    }

    /// <summary>Polls until <paramref name="condition"/> holds — for state that isn't a single property read.</summary>
    public async Task WaitUntil(Func<Task<bool>> condition, string description, int timeoutSeconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                if (await condition())
                    return;
            }
            catch
            {
            }
            await Task.Delay(300);
        }
        throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for: {description}");
    }

    /// <summary>A numeric property, parsed invariantly (the agent returns ToString() of the value).</summary>
    public async Task<double?> GetDouble(string property, string automationId)
    {
        var raw = await GetProperty(property, automationId: automationId);
        return Double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    public async Task Fill(string text, string? automationId = null, string? elementId = null)
    {
        var id = await ResolveId(automationId, elementId);
        var filled = false;
        await RideOutLease(async () => filled = await Client.FillAsync(id, text));
        if (!filled)
            throw new InvalidOperationException($"Fill failed on '{automationId ?? elementId}'");
    }

    public async Task Clear(string? automationId = null, string? elementId = null)
    {
        var id = await ResolveId(automationId, elementId);
        await Client.ClearAsync(id);
    }

    public async Task<string?> GetProperty(string property, string? automationId = null, string? elementId = null)
    {
        var id = await ResolveId(automationId, elementId);
        return await Client.GetPropertyAsync(id, property);
    }

    /// <summary>
    /// Sets a property through the agent — the reliable seam for custom controls that handle input
    /// themselves and give a synthetic tap nothing to invoke.
    /// </summary>
    public async Task SetProperty(string property, string value, string? automationId = null, string? elementId = null)
    {
        // A refusal is usually another DevFlow session (the CLI, an agent) briefly holding the
        // mutation lease, or a stale id — retry with a fresh lookup before calling it a failure.
        for (var attempt = 0; attempt < 4; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(750 * attempt);

            var id = await ResolveId(automationId, elementId);
            var set = false;
            await RideOutLease(async () => set = await Client.SetPropertyAsync(id, property, value));
            if (set)
                return;
        }
        throw new InvalidOperationException($"SetProperty {property}={value} failed on '{automationId ?? elementId}'");
    }

    public async Task Scroll(string? automationId = null, string? elementId = null, int? dy = null, int? itemIndex = null)
    {
        string? id = elementId;
        if (id == null && automationId != null)
            id = (await FindElementAsync(automationId, timeoutSeconds: 10)).Id;

        await Client.ScrollAsync(elementId: id, deltaX: 0, deltaY: dy ?? 0, animated: true, itemIndex: itemIndex);
    }

    public async Task Resize(int width, int height)
        => await Client.ResizeAsync(width, height);

    public async Task Gesture(string gestureType, string? automationId = null, string? direction = null, double? distance = null, int? durationMs = null)
    {
        string? elId = null;
        if (automationId != null)
            elId = (await FindElementAsync(automationId, timeoutSeconds: 10)).Id;

        await Client.GestureAsync(gestureType, elementId: elId, direction: direction, distance: distance, durationMs: durationMs);
    }

    // ── evidence ────────────────────────────────────────────────────────────

    public async Task<string> Screenshot(string filename, string? elementAutomationId = null)
    {
        string? elId = null;
        if (elementAutomationId != null)
            elId = (await FindElementAsync(elementAutomationId, timeoutSeconds: 10)).Id;

        // Capture fails transiently while a page is mid-animation (pulses, shimmers, marquees), so retry.
        byte[]? bytes = null;
        string? reason = null;
        for (var attempt = 0; attempt < 4 && bytes == null; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(500 * attempt);

            try
            {
                var result = await Client.ScreenshotResultAsync(elementId: elId);
                if (result.Success && result.Data is { Length: > 0 })
                    bytes = result.Data;
                else
                    reason = result.Reason ?? result.Error;
            }
            catch (Exception ex)
            {
                reason = ex.Message;
            }
        }
        if (bytes == null)
            throw new InvalidOperationException("Screenshot returned no data: " + reason);

        var path = Path.Combine(screenshotDir, filename);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
    }

    /// <summary>
    /// Error-level log lines written after <paramref name="since"/>.
    /// The agent's log store outlives the process, so the time filter is what scopes it to one test.
    /// </summary>
    public async Task<List<AppLogError>> GetErrors(DateTimeOffset since, int limit = 500)
    {
        string json;
        try
        {
            json = await Client.GetLogsAsync(limit, 0, "all");
        }
        catch
        {
            return [];
        }

        var errors = new List<AppLogError>();
        using var doc = JsonDocument.Parse(json);
        var entries = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : doc.RootElement.TryGetProperty("entries", out var e) ? e : default;

        if (entries.ValueKind != JsonValueKind.Array)
            return errors;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("t", out var t) || !DateTimeOffset.TryParse(t.GetString(), out var time) || time < since)
                continue;

            var level = entry.TryGetProperty("l", out var l) ? l.GetString() ?? "" : "";
            var exception = entry.TryGetProperty("e", out var ex) && ex.ValueKind == JsonValueKind.String ? ex.GetString() : null;
            // Warnings carrying exceptions are mostly the network (remote demo images) — not the app's fault.
            if (level is not ("Error" or "Critical"))
                continue;

            errors.Add(new AppLogError(
                time,
                level,
                entry.TryGetProperty("c", out var c) ? c.GetString() ?? "" : "",
                entry.TryGetProperty("m", out var m) ? m.GetString() ?? "" : "",
                exception
            ));
        }
        return errors;
    }
}
