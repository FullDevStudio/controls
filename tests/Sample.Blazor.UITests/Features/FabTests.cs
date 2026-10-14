namespace Sample.Blazor.UITests.Features;

/// <summary>Fab (a click raises Clicked) and FabMenu (speed dial: open, pick an item, backdrop closes).</summary>
public class FabTests : BlazorTest
{
    ILocator Trigger => Page.Locator(".shiny-fab-menu-trigger .shiny-fab");
    ILocator Items => Page.Locator(".shiny-fab-menu-item");
    ILocator LastTapped => Page.Locator("p", new() { HasText = "Last tapped:" }).Locator("strong");


    [Test]
    public async Task Fab_Click_Raises_Clicked()
    {
        await Open("fab");
        var section = Page.Locator(".demo-section").Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Icon-only (circle)" }) });
        var counter = section.Locator("strong");

        await section.Locator(".shiny-fab").First.ClickAsync();
        await section.Locator(".shiny-fab").First.ClickAsync();
        await Expect(counter).ToHaveTextAsync("2");

        // The other FABs have no handler — clicking them changes nothing.
        await section.Locator(".shiny-fab").Nth(1).ClickAsync();
        await Expect(counter).ToHaveTextAsync("2");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Extended_Fab_Shows_Its_Text_And_Icon_Only_Ones_Are_Circles()
    {
        await Open("fab");

        await Expect(Page.Locator(".shiny-fab", new() { HasText = "Compose" }).Locator(".shiny-fab-text")).ToHaveTextAsync("Compose");
        await Expect(Page.Locator(".shiny-fab", new() { HasText = "Compose" })).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-circle"));

        // HasShadow="false" drops the shadow class; the default keeps it.
        var sizes = Page.Locator(".demo-section").Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = "Sizes & shadow" }) }).Locator(".shiny-fab");
        await Expect(sizes.Nth(0)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-shadow"));
        await Expect(sizes.Nth(3)).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-shadow"));

        // Size drives the rendered diameter.
        var small = (await sizes.Nth(0).BoundingBoxAsync())!;
        var large = (await sizes.Nth(2).BoundingBoxAsync())!;
        await Assert.That(Math.Round(small.Width)).IsEqualTo(40);
        await Assert.That(Math.Round(large.Width)).IsEqualTo(72);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Menu_Trigger_Opens_And_Closes_The_Items()
    {
        await Open("fab-menu");
        await Expect(Items.First).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-menu-item-open"));

        await Trigger.ClickAsync();
        await Expect(Items).ToHaveCountAsync(4);
        for (var i = 0; i < 4; i++)
            await Expect(Items.Nth(i)).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-menu-item-open"));
        await Expect(Page.Locator(".shiny-fab-backdrop")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-backdrop-open"));

        await Trigger.ClickAsync();
        await Expect(Items.First).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-menu-item-open"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Picking_An_Item_Reports_It_And_Closes_The_Menu()
    {
        await Open("fab-menu");
        await Trigger.ClickAsync();
        await Expect(Items.First).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-menu-item-open"));

        await Page.Locator(".shiny-fab-menu-btn", new() { HasText = "New Photo" }).ClickAsync();

        await Expect(LastTapped).ToHaveTextAsync("New Photo");
        await Expect(Items.First).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-menu-item-open"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Text_Less_Item_Is_A_Circle_And_Reports_Its_Tag()
    {
        await Open("fab-menu");
        await Trigger.ClickAsync();

        var circle = Page.Locator(".shiny-fab-menu-btn-circle");
        await Expect(circle).ToHaveCountAsync(1);
        await Expect(circle.Locator(".shiny-fab-menu-label")).ToHaveCountAsync(0);

        await circle.ClickAsync();
        await Expect(LastTapped).ToHaveTextAsync("link");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Backdrop_Click_Closes_Without_Picking()
    {
        await Open("fab-menu");
        await Trigger.ClickAsync();
        await Expect(Page.Locator(".shiny-fab-backdrop")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-backdrop-open"));

        await Page.Locator(".shiny-fab-backdrop").ClickAsync(new() { Position = new() { X = 10, Y = 10 } });

        await Expect(Items.First).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-menu-item-open"));
        await Expect(LastTapped).ToHaveTextAsync("(none)");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Start_Alignment_Grows_From_The_Left()
    {
        await Open("fab-menu");
        await Expect(Page.Locator(".shiny-fab-menu")).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-menu-start"));

        await Page.GetByRole(AriaRole.Checkbox, new() { Name = "Grow from the left" }).CheckAsync();

        await Expect(Page.Locator(".shiny-fab-menu")).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("shiny-fab-menu-start"));
        await AssertNoConsoleErrors();
    }
}
