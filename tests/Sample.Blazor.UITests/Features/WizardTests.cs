using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>Wizard on <c>/wizard</c>: validation gating, Next/Back, hidden steps, finish and cancel.</summary>
public class WizardTests : BlazorTest
{
    // the checkout wizard is the first on the page, the indicator-style demo the second
    ILocator Checkout => Page.Locator(".shiny-wizard").First;
    ILocator NextButton => Checkout.Locator(".shiny-wizard__nav-end button").Last;
    ILocator Current => Checkout.Locator(".shiny-wizard__progress [aria-current=step]");
    ILocator Steps => Checkout.Locator(".shiny-wizard__chevrons > li");
    ILocator Status => Page.Locator(".demo-section strong").Filter(new() { HasTextRegex = new Regex("submitted|Submitted|Cancelled") });

    async Task EnterEmail(string email)
    {
        var input = Checkout.Locator("input.shiny-te-input");
        await input.FillAsync(email);
        await input.PressAsync("Tab");
    }

    Task ClickNav(string name) => Checkout.Locator(".shiny-wizard__nav").GetByRole(AriaRole.Button, new() { Name = name, Exact = true }).ClickAsync();

    [Test]
    public async Task Next_Is_Disabled_Until_The_Email_Is_Valid()
    {
        await Open("wizard");
        await Expect(Current).ToHaveTextAsync("Account");
        await Expect(NextButton).ToBeDisabledAsync();

        await EnterEmail("not-an-email");
        await Expect(NextButton).ToBeDisabledAsync();

        await EnterEmail("ada@example.com");
        await Expect(NextButton).ToBeEnabledAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Next_And_Back_Move_Between_Steps()
    {
        await Open("wizard");
        await EnterEmail("ada@example.com");

        await ClickNav("Next");
        await Expect(Current).ToHaveTextAsync("Fulfilment");
        await Expect(Steps.Nth(0)).ToHaveClassAsync(new Regex(@"\bis-complete|is-done|is-visited|is-past\b"));

        await ClickNav("Back");
        await Expect(Current).ToHaveTextAsync("Account");
        // the value survives the round trip
        await Expect(Checkout.Locator("input.shiny-te-input")).ToHaveValueAsync("ada@example.com");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Hidden_Step_Drops_Off_The_Progress_And_Is_Skipped()
    {
        await Open("wizard");
        await Expect(Steps).ToHaveCountAsync(4);
        await EnterEmail("ada@example.com");
        await ClickNav("Next");
        await Expect(Current).ToHaveTextAsync("Fulfilment");

        await Checkout.Locator("label", new() { HasTextString = "Deliver it to me" }).Locator("input").UncheckAsync();
        await Expect(Steps).ToHaveCountAsync(3);
        await Expect(Checkout.Locator(".shiny-wizard__chev-text", new() { HasTextString = "Delivery" })).ToHaveCountAsync(0);

        await ClickNav("Next");
        await Expect(Current).ToHaveTextAsync("Review");
        await Expect(NextButton).ToHaveTextAsync("Place order");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Finishing_Runs_The_Async_Validator_Then_Submits()
    {
        await Open("wizard");
        await EnterEmail("ada@example.com");
        await ClickNav("Next");
        await Checkout.Locator("label", new() { HasTextString = "Deliver it to me" }).Locator("input").UncheckAsync();
        await ClickNav("Next");
        await Expect(Current).ToHaveTextAsync("Review");
        await Expect(Checkout).ToContainTextAsync("Receipt to ada@example.com");

        await ClickNav("Place order");
        await Expect(Page.Locator(".demo-section strong", new() { HasTextString = "Submitted" }))
            .ToHaveTextAsync("Submitted ada@example.com for collection");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Cancel_Reports_Nothing_Submitted()
    {
        await Open("wizard");
        await ClickNav("Cancel");
        await Expect(Page.Locator(".demo-section strong", new() { HasTextString = "Cancelled" }))
            .ToHaveTextAsync("Cancelled — nothing was submitted.");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Completed_Step_Can_Be_Selected_From_The_Progress()
    {
        await Open("wizard");
        await EnterEmail("ada@example.com");
        await ClickNav("Next");
        await Expect(Current).ToHaveTextAsync("Fulfilment");

        await Checkout.Locator(".shiny-wizard__chev-btn", new() { HasTextString = "Account" }).ClickAsync();
        await Expect(Current).ToHaveTextAsync("Account");
        await AssertNoConsoleErrors();
    }

    [Test]
    [Arguments("Chevron", ".shiny-wizard__chevrons")]
    [Arguments("Dots", ".shiny-wizard__dots")]
    [Arguments("Bar", ".shiny-wizard__bar-track")]
    public async Task Indicator_Style_Switches_The_Progress_Markup(string style, string selector)
    {
        await Open("wizard");
        await Page.GetByRole(AriaRole.Button, new() { Name = style, Exact = true }).ClickAsync();
        var demo = Page.Locator(".shiny-wizard").Nth(1);
        await Expect(demo.Locator(selector)).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }
}
