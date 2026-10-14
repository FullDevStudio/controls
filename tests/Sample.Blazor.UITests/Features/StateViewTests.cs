using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>StateView on <c>/stateview</c>: one string picks the branch on screen, with a transition.</summary>
public class StateViewTests : BlazorTest
{
    ILocator Main => Page.Locator(".shiny-stateview").First;
    ILocator Pane => Main.Locator(".shiny-stateview__pane");
    ILocator Current => Page.Locator("p", new() { HasTextString = "Current state:" }).Locator("strong");

    Task Click(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true }).First.ClickAsync();

    [Test]
    public async Task Load_Passes_Through_Loading_To_Loaded()
    {
        await Open("stateview");
        await Expect(Pane.Locator("h3")).ToHaveTextAsync("Nothing loaded");

        await Click("Load");
        await Expect(Current).ToHaveTextAsync("Loading");
        await Expect(Pane.Locator("h3")).ToHaveTextAsync("Loading…");

        await Expect(Current).ToHaveTextAsync("Loaded", new() { Timeout = 5000 });
        await Expect(Pane.Locator("h3")).ToHaveTextAsync("Loaded");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Fail_Shows_Error_And_Try_Again_Recovers()
    {
        await Open("stateview");
        await Click("Fail");
        await Expect(Pane.Locator("h3")).ToHaveTextAsync("Something went wrong", new() { Timeout = 5000 });

        await Pane.GetByRole(AriaRole.Button, new() { Name = "Try again", Exact = true }).ClickAsync();
        await Expect(Pane.Locator("h3")).ToHaveTextAsync("Loaded", new() { Timeout = 5000 });
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Only_The_Current_Branch_Is_Rendered()
    {
        await Open("stateview");
        await Expect(Main.Locator("h3", new() { HasTextRegex = new Regex("^Loaded$") })).ToHaveCountAsync(0);
        await Expect(Main.Locator("h3", new() { HasTextString = "Something went wrong" })).ToHaveCountAsync(0);

        await Click("Load");
        await Expect(Current).ToHaveTextAsync("Loaded", new() { Timeout = 5000 });
        await Expect(Main.Locator("h3", new() { HasTextRegex = new Regex("^Nothing loaded$") })).ToHaveCountAsync(0);

        await Click("Empty");
        await Expect(Pane.Locator("h3")).ToHaveTextAsync("Nothing loaded");
        await AssertNoConsoleErrors();
    }

    [Test]
    [Arguments("Fade", "is-fade")]
    [Arguments("Scale", "is-scale")]
    [Arguments("SlideUp", "is-slide-up")]
    public async Task Transition_Choice_Is_Applied_To_The_Incoming_Pane(string transition, string cssClass)
    {
        await Open("stateview");
        await Click(transition);
        await Click("Two");

        var demo = Page.Locator(".shiny-stateview").Nth(1);
        await Expect(demo.Locator(".shiny-stateview__pane h3")).ToHaveTextAsync("Panel two");
        await Expect(demo.Locator(".shiny-stateview__pane")).ToHaveClassAsync(new Regex($@"\b{cssClass}\b"));
        await AssertNoConsoleErrors();
    }
}
