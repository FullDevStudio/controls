using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>
/// Captcha (Blazor-only) with the built-in local provider. The math challenge is real text, so a
/// test can read and solve it; the drawn-text challenge is a canvas, so only its wrong-answer and
/// refresh behaviour is driven. Covers solve → token, wrong answer → error, refresh, and the
/// ValidChanged-gated submit button.
/// </summary>
public class CaptchaTests : BlazorTest
{
    ILocator Section(string heading)
        => Page.Locator(".demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });

    ILocator MathSection => Section("Math mode, dark, case-sensitive off");

    static string Solve(string question)
    {
        var m = Regex.Match(question, @"(\d+)\s*([+−-])\s*(\d+)");
        if (!m.Success)
            throw new InvalidOperationException($"Unreadable math challenge '{question}'");

        var a = Int32.Parse(m.Groups[1].Value);
        var b = Int32.Parse(m.Groups[3].Value);
        return (m.Groups[2].Value == "+" ? a + b : a - b).ToString();
    }

    [Test]
    public async Task Math_Challenge_Solves_To_A_Token()
    {
        await Open("captcha");
        var question = await MathSection.Locator(".shiny-captcha-math").InnerTextAsync();
        var input = MathSection.Locator(".shiny-captcha-input");

        await input.FillAsync(Solve(question));

        await Expect(MathSection.Locator(".shiny-captcha-local")).ToHaveClassAsync(new Regex("is-solved"));
        await Expect(MathSection.Locator(".state code")).ToHaveTextAsync(new Regex(@"^local\.[0-9a-f]{32}$"));
        await Expect(input).Not.ToBeEditableAsync();
        await Expect(MathSection.Locator(".shiny-captcha-refresh")).ToBeDisabledAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Wrong_Answer_Shows_The_Error_And_Stays_Unsolved()
    {
        await Open("captcha");
        var answer = Solve(await MathSection.Locator(".shiny-captcha-math").InnerTextAsync());
        var input = MathSection.Locator(".shiny-captcha-input");

        await input.FillAsync(answer == "99" ? "98" : "99");
        await input.PressAsync("Enter");

        await Expect(input).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(MathSection.Locator(".shiny-captcha-error")).ToHaveTextAsync("That didn't match — try again");
        await Expect(MathSection.Locator(".state code")).ToHaveTextAsync("—");

        // editing the answer clears the error state (an empty field can't be wrong yet)
        await input.FillAsync("");
        await Expect(input).ToHaveAttributeAsync("aria-invalid", "false");
        await Expect(MathSection.Locator(".shiny-captcha-error")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Refresh_Draws_A_New_Math_Question()
    {
        await Open("captcha");
        var math = MathSection.Locator(".shiny-captcha-math");
        var first = await math.InnerTextAsync();

        // a new pair is random, so allow a few refreshes for it to differ
        for (var i = 0; i < 8 && await math.InnerTextAsync() == first; i++)
            await MathSection.Locator(".shiny-captcha-refresh").ClickAsync();

        await Expect(math).Not.ToHaveTextAsync(first);
        await Expect(math).ToHaveAttributeAsync("aria-label", new Regex(@"^What is \d+ [+−] \d+\?$"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Text_Challenge_Rejects_A_Wrong_Answer()
    {
        await Open("captcha");
        var section = Section("Default (local challenge)");
        var input = section.Locator(".shiny-captcha-input");

        // the drawn alphabet has no I, L, O, 0 or 1, so this can never be the answer
        await input.FillAsync("IIIIIIII");
        await input.PressAsync("Enter");

        await Expect(input).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(section.Locator(".shiny-captcha-error")).ToBeVisibleAsync();
        await Expect(section.Locator(".state")).ToContainTextAsync("Valid: False");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Submit_Stays_Disabled_Until_Solved()
    {
        await Open("captcha");
        var section = Section("Gating a submit button");
        var submit = section.GetByRole(AriaRole.Button, new() { Name = "Submit" });

        await Expect(submit).ToBeDisabledAsync();

        await section.Locator(".shiny-captcha-input").FillAsync("IIIIIIII");
        await section.Locator(".shiny-captcha-input").PressAsync("Enter");
        await Expect(section.Locator(".shiny-captcha-input")).ToHaveAttributeAsync("aria-invalid", "true");
        await Expect(submit).ToBeDisabledAsync();
        await AssertNoConsoleErrors();
    }
}
