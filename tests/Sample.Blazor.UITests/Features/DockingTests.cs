namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The docking page persists its layout to localStorage (one key, shared by every page in the browser
/// context), so each test starts from a cleared layout and the class never runs its tests side by side.
/// </summary>
[NotInParallel(nameof(DockingTests))]
public class DockingTests : BlazorTest
{
    const string LayoutKey = "shiny-dock-layout";

    ILocator Host => Page.Locator(".shiny-dock-host");
    ILocator Status => Page.Locator("p.status");
    ILocator Tab(string title) => Host.Locator($".shiny-dock-tab[title='{title}']");

    async Task OpenCleanLayout()
    {
        await Open("docking");
        await Page.EvaluateAsync($"() => localStorage.removeItem('{LayoutKey}')");
        await Open("docking");
        await Expect(Tab("Program.cs")).ToBeVisibleAsync();
    }

    [After(Test)]
    public async Task ForgetLayout()
    {
        try { await Page.EvaluateAsync($"() => localStorage.removeItem('{LayoutKey}')"); } catch { }
    }

    [Test]
    public async Task Clicking_A_Tab_Activates_Its_Panel()
    {
        await OpenCleanLayout();

        await Expect(Tab("README.md")).ToHaveAttributeAsync("aria-selected", "false");
        await Tab("README.md").ClickAsync();

        await Expect(Tab("README.md")).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Tab("Program.cs")).ToHaveAttributeAsync("aria-selected", "false");
        await Expect(Host.GetByRole(AriaRole.Heading, new() { Name = "Shiny Docking" })).ToBeVisibleAsync();
        await Expect(Status).ToHaveTextAsync("Activated: readme");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Panel_State_Survives_Switching_Tabs()
    {
        await OpenCleanLayout();

        var counter = Page.Locator("#editor-counter");
        var click = Host.GetByRole(AriaRole.Button, new() { Name = "Click me" });
        await click.ClickAsync();
        await click.ClickAsync();
        await Expect(counter).ToHaveTextAsync("2");

        await Tab("README.md").ClickAsync();
        await Expect(click).ToBeHiddenAsync();
        await Tab("Program.cs").ClickAsync();

        await Expect(counter).ToHaveTextAsync("2");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Closing_A_Tab_Removes_It_And_ShowPanel_Brings_It_Back()
    {
        await OpenCleanLayout();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Close Output" }).ClickAsync();
        await Expect(Tab("Output")).ToHaveCountAsync(0);
        await Expect(Status).ToHaveTextAsync("Layout changed: hide-panel");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Show Output (bottom)" }).ClickAsync();
        await Expect(Tab("Output")).ToHaveCountAsync(1);
        await Expect(Tab("Output")).ToHaveAttributeAsync("aria-selected", "true");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Show_Panel_Activates_An_Already_Docked_Panel()
    {
        await OpenCleanLayout();

        await Tab("Error List").ClickAsync();
        await Expect(Tab("Output")).ToHaveAttributeAsync("aria-selected", "false");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Show Output (bottom)" }).ClickAsync();

        await Expect(Tab("Output")).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Tab("Output")).ToHaveCountAsync(1); // activated, not docked a second time
        await Expect(Status).ToHaveTextAsync("Activated: output");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Reset_Layout_Restores_A_Closed_Panel()
    {
        await OpenCleanLayout();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Close README.md" }).ClickAsync();
        await Expect(Tab("README.md")).ToHaveCountAsync(0);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Reset layout" }).ClickAsync();

        await Expect(Tab("README.md")).ToHaveCountAsync(1);
        await Expect(Status).ToHaveTextAsync("Layout changed: reset");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Float_Properties_Opens_A_Floating_Window()
    {
        await OpenCleanLayout();

        var floats = Host.Locator(".shiny-dock-float");
        await Expect(floats).ToHaveCountAsync(0);

        await Page.GetByRole(AriaRole.Button, new() { Name = "Float Properties" }).ClickAsync();

        await Expect(floats).ToHaveCountAsync(1);
        await Expect(floats.Locator(".shiny-dock-float__title")).ToContainTextAsync("Properties");
        await Expect(Status).ToHaveTextAsync("Activated: properties"); // a floated panel becomes the active one
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Lock_Hides_Close_And_Float_Chrome()
    {
        await OpenCleanLayout();

        await Expect(Host.Locator(".shiny-dock-tab__close").First).ToBeVisibleAsync();
        await Expect(Host.GetByRole(AriaRole.Button, new() { Name = "Float panel" }).First).ToBeVisibleAsync();

        await Page.GetByLabel("Lock layout").CheckAsync();

        await Expect(Host.Locator(".shiny-dock-tab__close")).ToHaveCountAsync(0);
        await Expect(Host.GetByRole(AriaRole.Button, new() { Name = "Float panel" })).ToHaveCountAsync(0);
        await Expect(Host.Locator(".shiny-dock-split__separator--live")).ToHaveCountAsync(0);

        // tabs still switch while locked — only rearranging is off
        await Tab("README.md").ClickAsync();
        await Expect(Tab("README.md")).ToHaveAttributeAsync("aria-selected", "true");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Layout_Persists_Across_A_Reload()
    {
        await OpenCleanLayout();

        await Page.GetByRole(AriaRole.Button, new() { Name = "Close Error List" }).ClickAsync();
        await Expect(Tab("Error List")).ToHaveCountAsync(0);

        // the store writes asynchronously after the mutation
        await Page.WaitForFunctionAsync($"() => !!localStorage.getItem('{LayoutKey}')");
        await Open("docking");

        await Expect(Tab("Program.cs")).ToBeVisibleAsync();
        await Expect(Tab("Error List")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }
}
