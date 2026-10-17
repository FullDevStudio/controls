namespace Sample.Blazor.UITests;

/// <summary>
/// Real touch input, sent through Chromium's input pipeline (CDP Input.dispatchTouchEvent). Unlike
/// Page.Mouse on a phone context, this is what a finger does: the browser hit-tests touch-action,
/// may claim the gesture for scrolling and fire pointercancel, and reports pointerType "touch". A
/// control that only works with a mouse fails here. Chromium only — WebKit and Firefox have no CDP.
/// </summary>
public sealed class TouchInput(IPage page)
{
    ICDPSession? cdp;

    public static bool IsAvailable => BlazorTestSettings.Browser == "chromium";

    async Task Send(string type, params (float X, float Y)[] points)
    {
        cdp ??= await page.Context.NewCDPSessionAsync(page);
        await cdp.SendAsync("Input.dispatchTouchEvent", new Dictionary<string, object>
        {
            ["type"] = type,
            ["touchPoints"] = points.Select(p => new Dictionary<string, object> { ["x"] = p.X, ["y"] = p.Y }).ToArray()
        });
    }

    public Task DownAsync(float x, float y) => Send("touchStart", (x, y));

    public async Task MoveAsync(float fromX, float fromY, float toX, float toY, int steps = 12, int stepDelayMs = 16)
    {
        for (var i = 1; i <= steps; i++)
        {
            var t = i / (float)steps;
            await Send("touchMove", (fromX + (toX - fromX) * t, fromY + (toY - fromY) * t));
            if (stepDelayMs > 0)
                await page.WaitForTimeoutAsync(stepDelayMs);
        }
    }

    public Task UpAsync() => Send("touchEnd");

    public async Task TapAsync(float x, float y)
    {
        await DownAsync(x, y);
        await page.WaitForTimeoutAsync(40);
        await UpAsync();
    }

    /// <summary>Press, optionally hold (a long-press), slide to a point, and lift.</summary>
    public async Task DragAsync(float fromX, float fromY, float toX, float toY, int holdMs = 0, int steps = 12)
    {
        await DownAsync(fromX, fromY);
        if (holdMs > 0)
            await page.WaitForTimeoutAsync(holdMs);
        await MoveAsync(fromX, fromY, toX, toY, steps);
        await page.WaitForTimeoutAsync(60);
        await UpAsync();
    }

    /// <summary>The centre of an element, scrolled into view first — a finger can only touch what is on screen.</summary>
    public static async Task<(float X, float Y)> CentreOf(ILocator locator)
    {
        var box = await BoxOf(locator);
        return (box.X + box.Width / 2, box.Y + box.Height / 2);
    }

    /// <summary>
    /// The centre of the part of an element a finger can actually reach: clipped by every scrolling or
    /// clipping ancestor and the viewport, and checked to be the topmost thing there. On a phone a
    /// wide element (a Gantt bar) often has its geometric centre under another pane.
    /// </summary>
    public static async Task<(float X, float Y)> VisibleCentreOf(ILocator locator)
    {
        await locator.ScrollIntoViewIfNeededAsync();
        var point = await locator.EvaluateAsync<float[]?>(
            """
            el => {
                const r = el.getBoundingClientRect();
                let left = Math.max(r.left, 0), top = Math.max(r.top, 0);
                let right = Math.min(r.right, innerWidth), bottom = Math.min(r.bottom, innerHeight);
                for (let a = el.parentElement; a; a = a.parentElement) {
                    if (getComputedStyle(a).overflow === 'visible') continue;
                    const c = a.getBoundingClientRect();
                    left = Math.max(left, c.left); top = Math.max(top, c.top);
                    right = Math.min(right, c.right); bottom = Math.min(bottom, c.bottom);
                }
                // walk across the visible strip for a point where the element itself is on top
                const y = (top + bottom) / 2;
                for (let i = 1; i < 10; i++) {
                    const x = left + (right - left) * i / 10;
                    const hit = document.elementFromPoint(x, y);
                    if (hit && (hit === el || el.contains(hit))) return [x, y];
                }
                return null;
            }
            """);
        if (point is null)
            throw new InvalidOperationException("No part of the element is reachable on screen");
        return (point[0], point[1]);
    }

    public static async Task<LocatorBoundingBoxResult> BoxOf(ILocator locator)
    {
        await locator.ScrollIntoViewIfNeededAsync();
        return await locator.BoundingBoxAsync() ?? throw new InvalidOperationException("The element has no box");
    }
}

/// <summary>Skips a test where CDP touch input is unavailable (anything but Chromium).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequiresTouchInputAttribute() : SkipAttribute("Real touch input needs Chromium (CDP)")
{
    public override Task<bool> ShouldSkip(TestRegisteredContext context)
        => Task.FromResult(!TouchInput.IsAvailable);
}
