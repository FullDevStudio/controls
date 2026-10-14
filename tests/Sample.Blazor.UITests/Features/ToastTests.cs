using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>IToastService on <c>/toast</c>: show, auto-dismiss, manual dismiss, tap-to-dismiss and the configuration form.</summary>
public class ToastTests : BlazorTest
{
    ILocator Toasts => Page.Locator(".shiny-toast-host .shiny-toast");
    ILocator Status => Page.Locator("p.status-message");
    ILocator Form => Page.Locator(".toast-form").First;

    Task Click(string button) => Page.GetByRole(AriaRole.Button, new() { Name = button, Exact = true }).ClickAsync();

    [Test]
    public async Task Themed_Toast_Shows_Then_Auto_Dismisses()
    {
        await Open("toast");
        await Click("Info");

        await Expect(Toasts).ToHaveCountAsync(1);
        await Expect(Toasts.First).ToHaveAttributeAsync("role", "alert");
        await Expect(Toasts.First.Locator(".shiny-toast-text")).ToContainTextAsync("This is a really long toast message");
        await Expect(Status).ToHaveTextAsync("Info toast shown");

        // default duration is 3s
        await Expect(Toasts).ToHaveCountAsync(0, new() { Timeout = 8000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Themed_Types_Get_Distinct_Colours()
    {
        await Open("toast");
        await Click("Success");
        await Expect(Toasts).ToHaveCountAsync(1);
        var success = await Toasts.First.EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor");
        await Expect(Toasts).ToHaveCountAsync(0, new() { Timeout = 8000 });

        await Click("Danger");
        await Expect(Toasts).ToHaveCountAsync(1);
        var danger = await Toasts.First.EvaluateAsync<string>("e => getComputedStyle(e).backgroundColor");

        await Assert.That(success).IsNotEqualTo(danger);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Persistent_Toast_Stays_Until_Dismissed()
    {
        await Open("toast");
        await Click("Show Persistent Toast");

        var toast = Toasts.Filter(new() { HasTextString = "Uploading file..." });
        await Expect(toast).ToHaveCountAsync(1);
        await Expect(toast.Locator(".shiny-toast-spinner")).ToHaveCountAsync(1);

        // Duration zero: well past the default 3s it is still up, and a tap does not dismiss it
        await Page.WaitForTimeoutAsync(4000);
        await toast.ClickAsync();
        await Page.WaitForTimeoutAsync(500);
        await Expect(toast).ToHaveCountAsync(1);

        await Click("Dismiss");
        await Expect(toast).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Expect(Status).ToHaveTextAsync("Manual toast dismissed");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Tap_Dismisses_A_Toast_Configured_To_Allow_It()
    {
        await Open("toast");
        await Form.Locator("input[type=text]").FillAsync("Tap me away");
        await Click("Show Toast");

        var toast = Toasts.Filter(new() { HasTextString = "Tap me away" });
        await Expect(toast).ToHaveCountAsync(1);
        await toast.ClickAsync();
        await Expect(toast).ToHaveCountAsync(0, new() { Timeout = 2500 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Configured_Position_Mode_And_Progress_Bar_Are_Applied()
    {
        await Open("toast");
        await Form.Locator("input[type=text]").FillAsync("Configured toast");
        await Form.Locator("select").Nth(0).SelectOptionAsync("Top");
        await Form.Locator("select").Nth(1).SelectOptionAsync("FillHorizontal");
        await Page.Locator("label.toggle-row", new() { HasTextString = "Show Progress Bar" }).Locator("input").CheckAsync();
        await Click("Show Toast");

        var toast = Toasts.Filter(new() { HasTextString = "Configured toast" });
        await Expect(toast).ToHaveCountAsync(1);
        await Expect(toast).ToHaveClassAsync(new Regex(@"\bshiny-toast-top\b"));
        await Expect(toast).ToHaveClassAsync(new Regex(@"\bshiny-toast-fill\b"));
        await Expect(toast.Locator(".shiny-toast-progress")).ToHaveCountAsync(1);
        await Expect(Status).ToHaveTextAsync("Configured toast shown");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Marquee_Overflow_Scrolls_Instead_Of_Truncating()
    {
        await Open("toast");
        await Form.Locator("select").Nth(4).SelectOptionAsync("Marquee");
        await Click("Show Toast");

        await Expect(Toasts).ToHaveCountAsync(1);
        await Expect(Toasts.First).ToHaveClassAsync(new Regex(@"\bshiny-toast-has-marquee\b"));
        await Expect(Toasts.First.Locator(".shiny-toast-marquee-inner")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }
}
