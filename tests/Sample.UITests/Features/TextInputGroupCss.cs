using Microsoft.Maui.DevFlow.Driver;

namespace Sample.UITests.Features;

/// <summary>
/// CSS-selector lookups for the text and picker input tests. Shiny's input controls are composites -
/// a TextEntry wraps a BorderlessEntry, a ChipGroup renders one ChipView per item - and their inner
/// parts carry no AutomationId of their own. The DevFlow agent answers descendant selectors
/// (<c>#FirstNameEntry BorderlessEntry</c>, <c>#RangeChips ChipView</c>), which reaches them without
/// leaning on the global index of a type across the whole page. Every action re-queries first, since
/// element ids belong to the capture that produced them.
/// </summary>
static class TextInputGroupCss
{
    public static async Task<List<ElementInfo>> Css(this MauiDevFlowDriver driver, string selector, int minCount = 1, int timeoutSeconds = 15)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        List<ElementInfo> last = [];
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                last = await driver.Client.QueryCssAsync(selector);
                if (last.Count >= minCount)
                    return last;
            }
            catch
            {
                // agent busy laying out; retry
            }
            await Task.Delay(300);
        }
        throw new TimeoutException($"CSS '{selector}' matched {last.Count} element(s); wanted at least {minCount} within {timeoutSeconds}s");
    }

    /// <summary>The current matches, empty when there are none - for asserting something has gone.</summary>
    public static async Task<List<ElementInfo>> CssNow(this MauiDevFlowDriver driver, string selector)
    {
        try
        {
            return await driver.Client.QueryCssAsync(selector);
        }
        catch
        {
            return [];
        }
    }

    public static async Task<ElementInfo> CssAt(this MauiDevFlowDriver driver, string selector, int index = 0, int timeoutSeconds = 15)
        => (await driver.Css(selector, index + 1, timeoutSeconds))[index];

    public static async Task TapCss(this MauiDevFlowDriver driver, string selector, int index = 0)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(750 * attempt);

            var element = await driver.CssAt(selector, index);
            if (await driver.Client.TapAsync(element.Id))
                return;
        }
        throw new InvalidOperationException($"Tap failed on CSS '{selector}'[{index}]");
    }

    public static async Task FillCss(this MauiDevFlowDriver driver, string selector, string text, int index = 0)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(750 * attempt);

            var element = await driver.CssAt(selector, index);
            if (await driver.Client.FillAsync(element.Id, text))
                return;
        }
        throw new InvalidOperationException($"Fill failed on CSS '{selector}'[{index}]");
    }

    public static async Task<string?> GetCss(this MauiDevFlowDriver driver, string selector, string property, int index = 0)
    {
        var element = await driver.CssAt(selector, index);
        return await driver.Client.GetPropertyAsync(element.Id, property);
    }

    public static async Task SetCss(this MauiDevFlowDriver driver, string selector, string property, string value, int index = 0)
    {
        for (var attempt = 0; attempt < 4; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(750 * attempt);

            var element = await driver.CssAt(selector, index);
            if (await driver.Client.SetPropertyAsync(element.Id, property, value))
                return;
        }
        throw new InvalidOperationException($"SetProperty {property}={value} failed on CSS '{selector}'[{index}]");
    }

    /// <summary>Polls a property (dotted paths work: <c>Tags.Count</c>, <c>BindingContext.FirstName</c>) until it reads <paramref name="expected"/>; null and empty are the same.</summary>
    public static Task WaitForValue(this MauiDevFlowDriver driver, string automationId, string property, string? expected, int timeoutSeconds = 10)
        => driver.WaitUntil(
            async () => String.Equals(await driver.GetProperty(property, automationId: automationId) ?? "", expected ?? "", StringComparison.Ordinal),
            $"'{automationId}'.{property} == '{expected}'",
            timeoutSeconds
        );

    public static Task WaitForCssValue(this MauiDevFlowDriver driver, string selector, string property, string? expected, int index = 0, int timeoutSeconds = 10)
        => driver.WaitUntil(
            async () => String.Equals(await driver.GetCss(selector, property, index) ?? "", expected ?? "", StringComparison.Ordinal),
            $"CSS '{selector}'[{index}].{property} == '{expected}'",
            timeoutSeconds
        );
}
