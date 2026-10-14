using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>
/// Theming: the header's pack picker and Dark toggle (IShinyThemeService) and the Theme Composer, whose
/// knobs write authoring tokens and round-trip the whole design through ?preset=.
/// </summary>
public class ThemeTests : BlazorTest
{
    ILocator PackPicker => Page.GetByRole(AriaRole.Combobox, new() { Name = "Theme" });
    ILocator DarkToggle => Page.Locator(".theme-switcher-dark input");
    ILocator Html => Page.Locator("html");

    Task<string> RootToken(string name)
        => Page.EvaluateAsync<string>($"() => getComputedStyle(document.documentElement).getPropertyValue('--shiny-{name}').trim()");

    ILocator Knob(string name) => Page.Locator("label.knob", new() { Has = Page.Locator(".knob-name", new() { HasTextString = name }) }).Locator("select");
    ILocator Code => Page.Locator("code.dock-code");

    [Test]
    public async Task Pack_Picker_Swaps_The_Stylesheet_And_Tokens()
    {
        await Open("/theming");
        var basicPrimary = await RootToken("color-primary");

        await PackPicker.SelectOptionAsync("ocean");

        await Expect(Page.Locator("link[href*='shiny-theme-ocean.css']")).ToHaveCountAsync(1);
        await Page.WaitForFunctionAsync(
            "before => getComputedStyle(document.documentElement).getPropertyValue('--shiny-color-primary').trim() !== before",
            basicPrimary);

        await PackPicker.SelectOptionAsync("");
        await Expect(Page.Locator("link[href*='shiny-theme-ocean.css']")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Dark_Toggle_Switches_The_Scheme_Class_And_Surface()
    {
        await Open("/theming");
        await Expect(DarkToggle).Not.ToBeCheckedAsync();
        var lightSurface = await RootToken("color-surface");

        await DarkToggle.CheckAsync();
        await Expect(Html).ToHaveClassAsync(new Regex("shiny-theme-dark"));
        await Assert.That(await RootToken("color-surface")).IsNotEqualTo(lightSurface);

        await DarkToggle.UncheckAsync();
        await Expect(Html).ToHaveClassAsync(new Regex("shiny-theme-light"));
        await Expect(Html).Not.ToHaveClassAsync(new Regex("shiny-theme-dark"));
        await Assert.That(await RootToken("color-surface")).IsEqualTo(lightSurface);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Composer_Knob_Writes_Tokens_And_The_Preset_Url()
    {
        await Open("/themes");
        var startCode = await Code.InnerTextAsync();

        await Knob("Accent").SelectOptionAsync(new SelectOptionValue { Label = "Rose" });

        await Expect(Code).Not.ToHaveTextAsync(startCode);
        var code = await Code.InnerTextAsync();
        await Page.WaitForURLAsync(new Regex($"preset={Regex.Escape(code)}"));
        // the override lands inline on <html>
        await Assert.That(await Html.GetAttributeAsync("style") ?? "").Contains("--shiny-");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Composer_Mode_Switches_To_Dark()
    {
        await Open("/themes");

        await Knob("Mode").SelectOptionAsync("dark");

        await Expect(Html).ToHaveClassAsync(new Regex("shiny-theme-dark"));
        await Page.WaitForURLAsync(new Regex("preset="));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Preset_In_The_Url_Restores_The_Design()
    {
        await Open("/themes");
        await Knob("Pack").SelectOptionAsync(new SelectOptionValue { Label = "Ocean" });
        await Knob("Radius").SelectOptionAsync(new SelectOptionValue { Label = "Pill" });
        await Knob("Mode").SelectOptionAsync("dark");
        var code = await Code.InnerTextAsync();

        await Open($"/themes?preset={code}");

        await Expect(Code).ToHaveTextAsync(code);
        await Expect(Knob("Pack")).ToHaveValueAsync("1");
        await Expect(Knob("Radius")).ToHaveValueAsync("5");
        await Expect(Knob("Mode")).ToHaveValueAsync("dark");
        await Expect(Page.Locator("link[href*='shiny-theme-ocean.css']")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Get_Code_Shows_The_Snippets_And_Reset_Restores_Default()
    {
        await Open("/themes");
        var defaultCode = await Code.InnerTextAsync();
        await Knob("Accent").SelectOptionAsync(new SelectOptionValue { Label = "Teal" });

        await Page.GetByRole(AriaRole.Button, new() { Name = "Get code" }).ClickAsync();
        var snippets = Page.Locator(".composer-code pre");
        await Expect(snippets).ToHaveCountAsync(2);
        await Expect(snippets.First).ToContainTextAsync("--shiny-");
        await Expect(snippets.Nth(1)).ToContainTextAsync("<ResourceDictionary");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Hide code" }).ClickAsync();
        await Expect(snippets).ToHaveCountAsync(0);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset" }).ClickAsync();
        await Expect(Code).ToHaveTextAsync(defaultCode);
        await Expect(Knob("Accent")).ToHaveValueAsync("0");
        await AssertNoConsoleErrors();
    }
}
