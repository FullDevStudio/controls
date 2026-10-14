using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class BadgeTests : BlazorTest
{
    ILocator Host(string content) => Page.Locator(".shiny-badge-host", new() { HasTextString = content }).First;
    ILocator BadgeOf(string content) => Host(content).Locator(".shiny-badge");
    ILocator Button(string name) => Page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    [Test]
    public async Task Increment_Raises_The_Count()
    {
        await Open("badge");

        var badge = BadgeOf("Inbox");
        await Expect(badge).ToHaveTextAsync("3");

        await Button("+").ClickAsync();
        await Expect(badge).ToHaveTextAsync("4");
        await Expect(badge).ToHaveAttributeAsync("aria-label", "4");

        await Button("+").ClickAsync();
        await Expect(badge).ToHaveTextAsync("5");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Empty_Text_Hides_The_Badge_And_Increment_Brings_It_Back()
    {
        await Open("badge");

        var badge = BadgeOf("Inbox");
        await Expect(badge).ToBeVisibleAsync();

        await Button("Clear").ClickAsync();
        await Expect(badge).ToHaveCountAsync(0);

        // a non-numeric (empty) count restarts at one
        await Button("+").ClickAsync();
        await Expect(badge).ToHaveTextAsync("1");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Count_Above_MaxCount_Shows_Overflow()
    {
        await Open("badge");

        var cart = BadgeOf("Cart");
        await Expect(cart).ToHaveTextAsync("99+");
        await Expect(cart).ToHaveAttributeAsync("aria-label", "99+");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Toggle_Dot_Shows_And_Hides_The_Indicator()
    {
        await Open("badge");

        var host = Page.Locator(".shiny-badge-host", new() { Has = Page.Locator(".demo-avatar") });
        var dot = host.Locator(".shiny-badge");
        await Expect(dot).ToHaveClassAsync(new Regex("shiny-badge--dot"));
        await Expect(dot).ToHaveAttributeAsync("aria-label", "Indicator");
        await Expect(dot).ToHaveTextAsync("");

        // IsDot off and no Text: nothing to show
        await Button("Toggle dot").ClickAsync();
        await Expect(dot).ToHaveCountAsync(0);

        await Button("Toggle dot").ClickAsync();
        await Expect(dot).ToHaveClassAsync(new Regex("shiny-badge--dot"));
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Toggle_Pulse_Starts_And_Stops_The_Animation()
    {
        await Open("badge");

        var badge = BadgeOf("Features");
        await Expect(badge).ToHaveClassAsync(new Regex("shiny-badge--pulse"));

        await Button("Toggle pulse").ClickAsync();
        await Expect(badge).Not.ToHaveClassAsync(new Regex("shiny-badge--pulse"));
        await Expect(badge).ToHaveTextAsync("NEW");

        await Button("Toggle pulse").ClickAsync();
        await Expect(badge).ToHaveClassAsync(new Regex("shiny-badge--pulse"));
        await AssertNoConsoleErrors();
    }

    [Test]
    [Arguments("Top Left", true, true)]
    [Arguments("Top Right", false, true)]
    [Arguments("Bottom Left", true, false)]
    [Arguments("Bottom Right", false, false)]
    public async Task Position_Places_The_Badge_On_Its_Corner(string corner, bool left, bool top)
    {
        await Open("badge");

        var host = Host(corner);
        var hostBox = (await host.BoundingBoxAsync())!;
        var badgeBox = (await host.Locator(".shiny-badge").BoundingBoxAsync())!;

        var badgeCenterX = badgeBox.X + badgeBox.Width / 2;
        var badgeCenterY = badgeBox.Y + badgeBox.Height / 2;
        var hostCenterX = hostBox.X + hostBox.Width / 2;
        var hostCenterY = hostBox.Y + hostBox.Height / 2;

        await Assert.That(badgeCenterX < hostCenterX).IsEqualTo(left);
        await Assert.That(badgeCenterY < hostCenterY).IsEqualTo(top);
        await AssertNoConsoleErrors();
    }
}
