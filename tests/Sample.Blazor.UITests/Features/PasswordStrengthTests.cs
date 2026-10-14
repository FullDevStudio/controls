namespace Sample.Blazor.UITests.Features;

/// <summary>
/// PasswordStrength: typing re-evaluates (debounced), the meter and rule checklist follow, and
/// IsAcceptable — not the score — is what gates the page's submit button.
/// </summary>
public class PasswordStrengthTests : BlazorTest
{
    const string Breached = "Passw0rd!";
    const string Passphrase = "the slow red barn on nine";

    ILocator Section(string heading)
        => Page.Locator(".demo-section").Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });

    ILocator Defaults => Section("Defaults (passphrase-first)");

    static ILocator Rule(ILocator section, string text)
        => section.Locator(".shiny-pws-rule").Filter(new() { HasText = text });


    [Test]
    public async Task Common_Password_Is_Refused_Even_With_Every_Character_Class()
    {
        await Open("passwordstrength");

        await Defaults.Locator("input.shiny-te-input").FillAsync(Breached);

        await Expect(Defaults.Locator(".state")).ToHaveTextAsync("This is one of the most commonly used passwords.");
        await Expect(Rule(Defaults, "Not a commonly used password")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-pws-rule--met"));
        await Expect(Defaults.GetByRole(AriaRole.Button, new() { Name = "Create account" })).ToBeDisabledAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Long_Passphrase_Meets_The_Policy_And_Enables_Submit()
    {
        await Open("passwordstrength");

        await Defaults.Locator("input.shiny-te-input").FillAsync(Passphrase);

        await Expect(Rule(Defaults, "At least 15 characters")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-pws-rule--met"));
        await Expect(Rule(Defaults, "Not a commonly used password")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-pws-rule--met"));

        var submit = Defaults.GetByRole(AriaRole.Button, new() { Name = "Create account" });
        await Expect(submit).ToBeEnabledAsync();

        var meter = Defaults.GetByRole(AriaRole.Progressbar, new() { Name = "Password strength" });
        var score = Int32.Parse((await meter.GetAttributeAsync("aria-valuenow"))!);
        await Assert.That(score).IsGreaterThan(0);

        await submit.ClickAsync();
        await Expect(Defaults.Locator(".state")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^Accepted a \w+ passphrase \(\d+/100\)\.$"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Clearing_The_Field_Disables_Submit_Again()
    {
        await Open("passwordstrength");
        var input = Defaults.Locator("input.shiny-te-input");
        var submit = Defaults.GetByRole(AriaRole.Button, new() { Name = "Create account" });

        await input.FillAsync(Passphrase);
        await Expect(submit).ToBeEnabledAsync();

        await input.FillAsync("");
        await Expect(submit).ToBeDisabledAsync();
        await Expect(Defaults.GetByRole(AriaRole.Progressbar)).ToHaveAttributeAsync("aria-valuenow", "0");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Show_Toggle_Reveals_The_Password()
    {
        await Open("passwordstrength");
        var input = Defaults.Locator("input.shiny-te-input");
        await input.FillAsync("secret words");
        await Expect(input).ToHaveAttributeAsync("type", "password");

        await Defaults.Locator(".shiny-te-tool-btn").ClickAsync();

        await Expect(input).ToHaveAttributeAsync("type", "text");
        await Expect(input).ToHaveValueAsync("secret words");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Show_Toggle_Relabels_Itself_And_Hides_Again()
    {
        await Open("passwordstrength");
        var input = Defaults.Locator("input.shiny-te-input");
        await input.FillAsync("secret words");

        await Defaults.GetByRole(AriaRole.Button, new() { Name = "Show" }).ClickAsync();
        await Expect(input).ToHaveAttributeAsync("type", "text");

        var hide = Defaults.GetByRole(AriaRole.Button, new() { Name = "Hide" });
        await Expect(hide).ToBeVisibleAsync(new() { Timeout = 3000 });
        await hide.ClickAsync();
        await Expect(input).ToHaveAttributeAsync("type", "password");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Legacy_Policy_Ticks_Each_Composition_Rule()
    {
        await Open("passwordstrength");
        var legacy = Section("Legacy composition policy");

        await legacy.Locator("input.shiny-te-input").FillAsync(Breached);

        // Every composition rule is met...
        foreach (var rule in new[] { "At least 8 characters", "An uppercase letter", "A lowercase letter", "A number", "A special character" })
            await Expect(Rule(legacy, rule)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-pws-rule--met"));

        // ...and it is still refused, because it is a breached value.
        await Expect(Rule(legacy, "Not a commonly used password")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-pws-rule--met"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Missing_Composition_Rules_Stay_Unmet()
    {
        await Open("passwordstrength");
        var legacy = Section("Legacy composition policy");

        await legacy.Locator("input.shiny-te-input").FillAsync("lowercaseonly");

        await Expect(Rule(legacy, "A lowercase letter")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-pws-rule--met"));
        await Expect(Rule(legacy, "An uppercase letter")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-pws-rule--met"));
        await Expect(Rule(legacy, "A number")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-pws-rule--met"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Localizer_Rewrites_The_Rules()
    {
        await Open("passwordstrength");
        var localized = Section("Localized wording");

        await Expect(localized.Locator(".shiny-pws-rule-text").First).ToHaveTextAsync("At least 12 characters");

        await localized.GetByRole(AriaRole.Checkbox).CheckAsync();

        await Expect(localized.Locator(".shiny-pws-rule-text").First).ToHaveTextAsync("Au moins 12 caractères");
        await Expect(localized.GetByRole(AriaRole.Button, new() { Name = "Voir" })).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }
}
