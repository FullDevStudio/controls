namespace Sample.Blazor.UITests.Features;

public class TimelineTests : BlazorTest
{
    ILocator Nodes => Page.Locator(".shiny-timeline__node");
    ILocator Status => Page.Locator(".timeline-status");
    ILocator BackButton => Page.GetByRole(AriaRole.Button, new() { Name = "◀ Back" });
    ILocator ForwardButton => Page.GetByRole(AriaRole.Button, new() { Name = "Forward ▶" });

    [Test]
    public async Task Starts_With_Third_Entry_Current()
    {
        await Open("timeline");

        await Expect(Status).ToHaveTextAsync("active: In transit");
        await Expect(Nodes).ToHaveCountAsync(5);
        await Expect(Nodes.Nth(0)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bis-complete\b"));
        await Expect(Nodes.Nth(1)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bis-complete\b"));
        await Expect(Nodes.Nth(2)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bis-current\b"));
        await Expect(Nodes.Nth(3)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bis-pending\b"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Forward_Advances_Active_Node()
    {
        await Open("timeline");

        await ForwardButton.ClickAsync();

        await Expect(Status).ToHaveTextAsync("active: Out for delivery");
        await Expect(Nodes.Nth(2)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bis-complete\b"));
        await Expect(Nodes.Nth(3)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bis-current\b"));

        await ForwardButton.ClickAsync();
        await Expect(Status).ToHaveTextAsync("active: Delivered");
        await Expect(ForwardButton).ToBeDisabledAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Back_Past_First_Leaves_Nothing_Active()
    {
        await Open("timeline");

        for (var i = 0; i < 3; i++)
            await BackButton.ClickAsync();

        await Expect(Status).ToHaveTextAsync("active: none");
        await Expect(BackButton).ToBeDisabledAsync();
        await Expect(Page.Locator(".shiny-timeline__node.is-pending")).ToHaveCountAsync(5);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task All_Active_Completes_Every_Node_And_Locks_Navigation()
    {
        await Open("timeline");

        await Page.GetByLabel("All active").CheckAsync();

        await Expect(Status).ToHaveTextAsync("all active");
        await Expect(Page.Locator(".shiny-timeline__node.is-pending")).ToHaveCountAsync(0);
        await Expect(Page.Locator(".shiny-timeline__node.is-current")).ToHaveCountAsync(0);
        await Expect(BackButton).ToBeDisabledAsync();
        await Expect(ForwardButton).ToBeDisabledAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Rail_Moves_To_The_Right()
    {
        await Open("timeline");
        var rail = Page.Locator("ol.shiny-timeline");
        await Expect(rail).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bis-left\b"));

        await Page.GetByLabel("Rail on the right").CheckAsync();

        await Expect(rail).ToHaveClassAsync(new System.Text.RegularExpressions.Regex(@"\bis-right\b"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Opposite_Template_Toggles_Timestamps()
    {
        await Open("timeline");
        await Expect(Page.Locator(".shiny-timeline__opposite")).ToHaveCountAsync(5);
        await Expect(Page.Locator(".shiny-timeline__opposite").First).ToHaveTextAsync("09:02");

        await Page.GetByLabel("Timestamps opposite").UncheckAsync();

        await Expect(Page.Locator(".shiny-timeline__opposite")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clicking_A_Node_Raises_NodeClicked()
    {
        await Open("timeline");

        await Nodes.Nth(1).Locator(".shiny-timeline__marker").ClickAsync();

        await Expect(Status).ToHaveTextAsync("clicked #2 — Picked");
        await AssertNoConsoleErrors();
    }
}
