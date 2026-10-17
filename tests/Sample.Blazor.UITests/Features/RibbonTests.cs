namespace Sample.Blazor.UITests.Features;

public class RibbonTests : BlazorTest
{
    ILocator Ribbon => Page.Locator(".shiny-ribbon");
    ILocator Log => Page.Locator("p.log");
    ILocator Tab(string title) => Ribbon.Locator(".shiny-ribbon__tabs").GetByRole(AriaRole.Tab, new() { Name = title, Exact = true });
    ILocator Item(string label) => Ribbon.Locator($".shiny-ribbon__body button[aria-label='{label}']");

    [Test]
    [SkipOnPhone("the ribbon folds its groups into buttons at phone width by design; these drive the unfolded commands")]
    public async Task Command_Buttons_Raise_Their_Handlers()
    {
        await Open("ribbon");

        await Item("Cut").ClickAsync();
        await Expect(Log).ToContainTextAsync("Cut");

        await Ribbon.Locator(".shiny-ribbon__qat").GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Expect(Log).ToContainTextAsync("Save");

        await Ribbon.Locator(".shiny-ribbon__app").ClickAsync();
        await Expect(Log).ToContainTextAsync("File");

        await Ribbon.GetByRole(AriaRole.Button, new() { Name = "Font settings" }).ClickAsync();
        await Expect(Log).ToContainTextAsync("Font dialog");
        await AssertNoConsoleErrors();
    }

    [Test]
    [SkipOnPhone("the ribbon folds its groups into buttons at phone width by design; these drive the unfolded commands")]
    public async Task Toggle_Button_Flips_Pressed_State()
    {
        await Open("ribbon");

        var bold = Item("Bold");
        await Expect(bold).ToHaveAttributeAsync("aria-pressed", "false");

        await bold.ClickAsync();
        await Expect(bold).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(bold).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-checked"));

        await bold.ClickAsync();
        await Expect(bold).ToHaveAttributeAsync("aria-pressed", "false");
        await AssertNoConsoleErrors();
    }

    [Test]
    [SkipOnPhone("the ribbon folds its groups into buttons at phone width by design; these drive the unfolded commands")]
    public async Task Alignment_Toggles_Are_Exclusive()
    {
        await Open("ribbon");

        await Expect(Item("Left")).ToHaveAttributeAsync("aria-pressed", "true");

        await Item("Centre").ClickAsync();

        await Expect(Item("Centre")).ToHaveAttributeAsync("aria-pressed", "true");
        await Expect(Item("Left")).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(Item("Right")).ToHaveAttributeAsync("aria-pressed", "false");
        await Expect(Log).ToContainTextAsync("Align center");
        await AssertNoConsoleErrors();
    }

    [Test]
    [SkipOnPhone("the ribbon folds its groups into buttons at phone width by design; these drive the unfolded commands")]
    public async Task Split_Button_Menu_Runs_The_Chosen_Entry()
    {
        await Open("ribbon");

        await Item("Paste options").ClickAsync();
        var menu = Page.Locator(".shiny-ribbon-menu[role=menu]").First;
        await Expect(menu).ToBeVisibleAsync();

        await menu.GetByRole(AriaRole.Menuitem, new() { Name = "Text only" }).ClickAsync();

        await Expect(Log).ToContainTextAsync("Paste (text)");
        await Expect(Page.Locator(".shiny-ribbon-menu[role=menu]:visible")).ToHaveCountAsync(0);

        // the face of the split button runs the default command
        await Item("Paste").ClickAsync();
        await Expect(Log).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"\s+Paste$"));
        await AssertNoConsoleErrors();
    }

    [Test]
    [SkipOnPhone("the ribbon folds its groups into buttons at phone width by design; these drive the unfolded commands")]
    public async Task Nested_Menu_Entry_Opens_A_Submenu()
    {
        await Open("ribbon");

        await Item("Text colour").ClickAsync();
        await Expect(Item("Text colour")).ToHaveAttributeAsync("aria-expanded", "true");

        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Standard colours" }).ClickAsync();
        await Page.GetByRole(AriaRole.Menuitem, new() { Name = "Blue" }).ClickAsync();

        await Expect(Log).ToContainTextAsync("Colour: blue");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Switching_Tabs_Shows_That_Tabs_Groups()
    {
        await Open("ribbon");

        await Expect(Tab("Home")).ToHaveAttributeAsync("aria-selected", "true");
        await Tab("Insert").ClickAsync();

        await Expect(Tab("Insert")).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Tab("Home")).ToHaveAttributeAsync("aria-selected", "false");
        await Expect(Ribbon.GetByRole(AriaRole.Tabpanel, new() { Name = "Insert" })).ToBeVisibleAsync();
        await Expect(Item("Picture")).ToBeVisibleAsync();
        await Expect(Item("Cut")).ToBeHiddenAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Contextual_Tab_Follows_Its_Visibility_Binding()
    {
        await Open("ribbon");

        await Expect(Tab("Format")).ToHaveCountAsync(0);

        var picture = Page.GetByLabel("Picture selected (contextual tab)");
        await picture.CheckAsync();
        await Expect(Tab("Format")).ToBeVisibleAsync();

        // the "Picture Tools" band captions the contextual tab while it is the one showing
        await Tab("Format").ClickAsync();
        await Expect(Tab("Format")).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Ribbon.Locator(".shiny-ribbon__context")).ToHaveTextAsync("Picture Tools");
        await Expect(Item("Crop")).ToBeVisibleAsync();

        // hiding the tab the ribbon is on falls back to a real one rather than an empty body
        await picture.UncheckAsync();
        await Expect(Tab("Format")).ToHaveCountAsync(0);
        await Expect(Ribbon.Locator(".shiny-ribbon__context")).ToHaveCountAsync(0);
        await Expect(Ribbon.Locator(".shiny-ribbon__tab[aria-selected=true]")).ToHaveCountAsync(1);
        await Expect(Ribbon.Locator(".shiny-ribbon__panel:visible")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Insert_Picture_Brings_Up_And_Selects_The_Contextual_Tab()
    {
        await Open("ribbon");

        await Tab("Insert").ClickAsync();
        await Item("Picture").ClickAsync();

        await Expect(Log).ToContainTextAsync("Picture Tools tab appeared");
        await Expect(Tab("Format")).ToBeVisibleAsync();
        await Expect(Tab("Format")).ToHaveAttributeAsync("aria-selected", "true");
        await Expect(Item("Crop")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    [SkipOnPhone("the ribbon folds its groups into buttons at phone width by design; these drive the unfolded commands")]
    public async Task Display_Mode_Buttons_Switch_The_Bar()
    {
        await Open("ribbon");

        var body = Ribbon.Locator(".shiny-ribbon__body");
        var collapse = Ribbon.Locator(".shiny-ribbon__collapse");

        await Page.GetByRole(AriaRole.Button, new() { Name = "Simplified", Exact = true }).ClickAsync();
        await Expect(Ribbon).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-simplified"));

        await Page.GetByRole(AriaRole.Button, new() { Name = "Collapsed", Exact = true }).ClickAsync();
        await Expect(collapse).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(body).ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-hidden"));

        await Page.GetByRole(AriaRole.Button, new() { Name = "Expanded", Exact = true }).ClickAsync();
        await Expect(collapse).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Ribbon).Not.ToHaveClassAsync(new System.Text.RegularExpressions.Regex("is-simplified|is-collapsed"));
        await Expect(Item("Cut")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    [SkipOnPhone("the ribbon folds its groups into buttons at phone width by design; these drive the unfolded commands")]
    public async Task Clicking_The_Showing_Tab_Collapses_And_A_Command_Puts_A_Peek_Away()
    {
        await Open("ribbon");

        await Tab("Home").ClickAsync();
        var collapse = Ribbon.Locator(".shiny-ribbon__collapse");
        await Expect(collapse).ToHaveAttributeAsync("aria-expanded", "false");

        // a tab click on a collapsed bar peeks the body open...
        await Tab("Home").ClickAsync();
        await Expect(Item("Cut")).ToBeVisibleAsync();

        // ...and running a command from it puts it away again
        await Item("Cut").ClickAsync();
        await Expect(Log).ToContainTextAsync("Cut");
        await Expect(Item("Cut")).ToBeHiddenAsync();
        await AssertNoConsoleErrors();
    }
}
