namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The toolbar's icon buttons carry their name only in a sibling tooltip, so items are found by
/// position within a section's toolbar (the order of the sample's Items lists). The hidden measuring
/// copy of the bar is excluded.
/// </summary>
public class ToolbarTests : BlazorTest
{
    ILocator Section(string headingStart)
        => Page.Locator("section.demo-section").Filter(new() { Has = Page.Locator($"h2:text-matches('^{headingStart}')") });

    const string NotMeasuring = ":not(.shiny-toolbar-measure *)";

    static ILocator Items(ILocator section)
        => section.Locator($".shiny-toolbar-items button.shiny-toolbar-item:not(.shiny-toolbar-more){NotMeasuring}");

    static ILocator More(ILocator section)
        => section.Locator($".shiny-toolbar-items button.shiny-toolbar-more{NotMeasuring}");

    static ILocator OpenMenus(ILocator section)
        => section.Locator(".shiny-toolbar-menu[role=menu]");

    ILocator LastTapped => Page.Locator("p.state strong").First;

    [Test]
    public async Task Clicking_An_Item_Raises_ItemClicked()
    {
        await Open("toolbar");

        var inbox = Section("Frosted top toolbar");
        await Expect(inbox.Locator(".shiny-toolbar-title")).ToHaveTextAsync("Inbox");
        await Expect(LastTapped).ToHaveTextAsync("—");

        await Items(inbox).Nth(2).ClickAsync(); // Compose
        await Expect(LastTapped).ToHaveTextAsync("Compose");

        await Items(Section("Solid colors")).Nth(1).ClickAsync(); // Share
        await Expect(LastTapped).ToHaveTextAsync("Share");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Item_Badge_Is_Shown()
    {
        await Open("toolbar");

        var inbox = Section("Frosted top toolbar");
        await Expect(Items(inbox).Nth(1).Locator(".shiny-toolbar-item-badge")).ToHaveTextAsync("3");
        await Expect(Items(inbox).Nth(0).Locator(".shiny-toolbar-item-badge")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Dropdown_Item_Opens_A_Menu_Instead_Of_Clicking()
    {
        await Open("toolbar");

        var section = Section("Dropdown buttons");
        var file = Items(section).Nth(0);
        await Expect(file).ToHaveAttributeAsync("aria-haspopup", "true");
        await Expect(OpenMenus(section)).ToHaveCountAsync(0);

        await file.ClickAsync();

        var menu = OpenMenus(section).First;
        await Expect(menu).ToBeVisibleAsync();
        await Expect(LastTapped).ToHaveTextAsync("—"); // a parent raises no click of its own

        await menu.GetByRole(AriaRole.Menuitem, new() { Name = "New" }).ClickAsync();

        await Expect(LastTapped).ToHaveTextAsync("New");
        await Expect(OpenMenus(section)).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Dropdown_Reports_Its_Expanded_State()
    {
        await Open("toolbar");

        var section = Section("Dropdown buttons");
        var file = Items(section).Nth(0);
        await Expect(file).ToHaveAttributeAsync("aria-expanded", "false");

        await file.ClickAsync();
        await Expect(file).ToHaveAttributeAsync("aria-expanded", "true");

        var recent = section.GetByRole(AriaRole.Menuitem, new() { Name = "Open recent" });
        await Expect(recent).ToHaveAttributeAsync("aria-expanded", "false");
        await recent.ClickAsync();
        await Expect(recent).ToHaveAttributeAsync("aria-expanded", "true");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Nested_Submenus_Reach_A_Leaf()
    {
        await Open("toolbar");

        var section = Section("Dropdown buttons");
        await Items(section).Nth(0).ClickAsync();

        await section.GetByRole(AriaRole.Menuitem, new() { Name = "Open recent" }).ClickAsync();
        await Expect(section.GetByRole(AriaRole.Menuitem, new() { Name = "Roadmap.md" })).ToBeVisibleAsync();

        await section.GetByRole(AriaRole.Menuitem, new() { Name = "Older" }).ClickAsync();
        await section.GetByRole(AriaRole.Menuitem, new() { Name = "2023 notes.md" }).ClickAsync();

        await Expect(LastTapped).ToHaveTextAsync("2023 notes.md");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Disabled_Menu_Entry_Cannot_Be_Chosen()
    {
        await Open("toolbar");

        var section = Section("Dropdown buttons");
        await Items(section).Nth(1).ClickAsync(); // View

        var presenter = section.GetByRole(AriaRole.Menuitem, new() { Name = "Presenter mode" });
        await Expect(presenter).ToBeDisabledAsync();

        await section.GetByRole(AriaRole.Menuitem, new() { Name = "Zoom in" }).ClickAsync();
        await Expect(LastTapped).ToHaveTextAsync("Zoom in");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Escape_Closes_An_Open_Menu()
    {
        await Open("toolbar");

        var section = Section("Dropdown buttons");
        await Items(section).Nth(0).ClickAsync();
        await Expect(OpenMenus(section).First).ToBeVisibleAsync();

        await Page.Keyboard.PressAsync("Escape");

        await Expect(OpenMenus(section)).ToHaveCountAsync(0);
        await Expect(LastTapped).ToHaveTextAsync("—");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clicking_Outside_Closes_An_Open_Menu()
    {
        await Open("toolbar");

        var section = Section("Dropdown buttons");
        await Items(section).Nth(0).ClickAsync();
        await Expect(OpenMenus(section).First).ToBeVisibleAsync();

        var viewport = Page.ViewportSize!;
        await Page.Mouse.ClickAsync(viewport.Width - 40, viewport.Height - 40);

        await Expect(OpenMenus(section)).ToHaveCountAsync(0);
        await Expect(LastTapped).ToHaveTextAsync("—");
        await AssertNoConsoleErrors();
    }

    [Test]
    [SkipOnPhone("counts the items that fit a desktop-width bar")]
    public async Task Items_That_Do_Not_Fit_Move_Into_The_Overflow_Menu()
    {
        await Open("toolbar");

        var section = Section("Overflow");
        var demo = section.Locator(".scroll-demo");
        var more = More(section);

        await Expect(Items(section)).ToHaveCountAsync(6);
        await Expect(more).ToHaveCountAsync(0);

        // what dragging the resize corner does
        await demo.EvaluateAsync("e => e.style.width = '240px'");

        await Expect(more).ToBeVisibleAsync();
        await Expect(Items(section)).Not.ToHaveCountAsync(6);
        var visibleNow = await Items(section).CountAsync();
        await Assert.That(visibleNow).IsLessThan(6);

        await more.ClickAsync();
        await Expect(OpenMenus(section).First).ToBeVisibleAsync();
        // the overflow menu holds exactly the items the bar dropped, last one included
        await Expect(OpenMenus(section).First.GetByRole(AriaRole.Menuitem)).ToHaveCountAsync(6 - visibleNow);
        var delete = section.GetByRole(AriaRole.Menuitem, new() { Name = "Delete" });
        await delete.ClickAsync();
        await Expect(LastTapped).ToHaveTextAsync("Delete");

        // widening again brings everything back out
        await demo.EvaluateAsync("e => e.style.width = ''");
        await Expect(Items(section)).ToHaveCountAsync(6);
        await Expect(more).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Custom_Start_Content_Is_Interactive()
    {
        await Open("toolbar");

        var section = Section("Custom content");
        await Expect(section.GetByText("Project Atlas")).ToBeVisibleAsync();
        await Expect(section.Locator(".shiny-toolbar").GetByText("Live")).ToBeVisibleAsync();

        await section.GetByRole(AriaRole.Button, new() { Name = "←" }).ClickAsync();
        await Expect(LastTapped).ToHaveTextAsync("Back");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Bottom_Bar_Shows_Labels_And_Opens_Its_Menu()
    {
        await Open("toolbar");

        var section = Section("Bottom-docked");
        await Expect(section.Locator(".shiny-toolbar--bottom")).ToHaveCountAsync(1);

        var share = Items(section).Nth(0);
        await Expect(share).ToContainTextAsync("Share"); // ShowItemLabels

        await share.ClickAsync();
        await section.GetByRole(AriaRole.Menuitem, new() { Name = "Email" }).ClickAsync();
        await Expect(LastTapped).ToHaveTextAsync("Email");
        await AssertNoConsoleErrors();
    }
}
