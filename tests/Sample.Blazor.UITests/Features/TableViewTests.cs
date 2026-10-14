using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>/cells (every cell type) and /tableview (templated sections).</summary>
public class TableViewTests : BlazorTest
{
    ILocator Cell(string title)
        => Page.Locator(".shiny-tv-cell").Filter(new()
        {
            Has = Page.Locator(".shiny-tv-cell-title", new() { HasTextRegex = new Regex("^" + Regex.Escape(title) + "$") })
        });

    ILocator State => Page.Locator("pre.state");
    ILocator LastAction => Page.Locator("p.state strong");


    // ── /cells ──────────────────────────────────────────────────────────────

    [Test]
    public async Task Switch_Cell_Toggles_Its_Binding()
    {
        await Open("cells");
        await Expect(State).ToContainTextAsync("notifications=True");
        var input = Cell("Notifications").Locator("input[type=checkbox]");
        await Expect(input).ToBeCheckedAsync();

        await Cell("Notifications").Locator(".shiny-tv-switch").ClickAsync();
        await Expect(State).ToContainTextAsync("notifications=False");
        await Expect(input).Not.ToBeCheckedAsync();

        await Cell("Background sync").Locator(".shiny-tv-switch").ClickAsync();
        await Expect(State).ToContainTextAsync("bgSync=True");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Tapping_The_Row_Toggles_A_Switch_Cell()
    {
        await Open("cells");
        await Cell("Notifications").Locator(".shiny-tv-cell-title").ClickAsync();
        await Expect(State).ToContainTextAsync("notifications=False");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Tapping_A_Radio_Row_Selects_It()
    {
        await Open("cells");

        await Cell("Small").Locator(".shiny-tv-cell-title").ClickAsync();
        await Expect(State).ToContainTextAsync("size=Small");
        await Expect(Cell("Small").Locator("input[type=radio]")).ToBeCheckedAsync();
        await Expect(Cell("Medium").Locator("input[type=radio]")).Not.ToBeCheckedAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Checkbox_And_Simple_Check_Cells_Toggle()
    {
        await Open("cells");

        await Cell("Subscribe").Locator("input[type=checkbox]").ClickAsync();
        await Expect(State).ToContainTextAsync("subscribe=True");

        await Expect(Cell("Default").Locator(".shiny-tv-check-mark")).ToBeVisibleAsync();
        await Cell("Default").ClickAsync();
        await Expect(State).ToContainTextAsync("default=False");
        await Expect(Cell("Default").Locator(".shiny-tv-check-mark")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Radio_Cells_Behave_As_One_Group()
    {
        await Open("cells");
        await Expect(Cell("Medium").Locator("input[type=radio]")).ToBeCheckedAsync();

        await Cell("Large").Locator("input[type=radio]").ClickAsync();
        await Expect(State).ToContainTextAsync("size=Large");
        await Expect(Cell("Large").Locator("input[type=radio]")).ToBeCheckedAsync();
        await Expect(Cell("Medium").Locator("input[type=radio]")).Not.ToBeCheckedAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Entry_Cell_Binds_And_Masks()
    {
        await Open("cells");

        var email = Cell("Email").Locator("input");
        await email.FillAsync("allan@shiny.net");
        await email.BlurAsync();
        await Expect(State).ToContainTextAsync("email=allan@shiny.net");

        var phone = Cell("Phone").Locator("input");
        await phone.ClickAsync();
        await phone.PressSequentiallyAsync("5551234567");
        await Expect(phone).ToHaveValueAsync("(555) 123-4567");

        var card = Cell("Card").Locator("input");
        await card.ClickAsync();
        await card.PressSequentiallyAsync("4111111111111111");
        await Expect(card).ToHaveValueAsync("4111 1111 1111 1111");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Number_And_Text_Picker_Cells_Update_Their_Values()
    {
        await Open("cells");

        var age = Cell("Age").Locator("input[type=number]");
        await age.FillAsync("42");
        await age.BlurAsync();
        await Expect(State).ToContainTextAsync("age=42");

        await Cell("Country").Locator("select").SelectOptionAsync("Mexico");
        await Expect(State).ToContainTextAsync("country=Mexico");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Command_And_Button_Cells_Run_Their_Actions()
    {
        await Open("cells");
        await Expect(LastAction).ToHaveTextAsync("(none)");

        await Cell("About").ClickAsync();
        await Expect(LastAction).ToHaveTextAsync("Tapped About");

        await Cell("Privacy").ClickAsync();
        await Expect(LastAction).ToHaveTextAsync("Tapped Privacy");

        await Page.GetByText("Sign out", new() { Exact = true }).ClickAsync();
        await Expect(LastAction).ToHaveTextAsync(new Regex("^Signed out at "));
        await AssertNoConsoleErrors();
    }


    // ── /tableview ──────────────────────────────────────────────────────────

    [Test]
    public async Task Templated_Section_Renders_A_Cell_Per_Item()
    {
        await Open("tableview");
        var devices = Page.Locator("section.shiny-tv-section", new() { Has = Page.Locator("header", new() { HasTextString = "Devices" }) });

        await Expect(devices.Locator(".shiny-tv-cell")).ToHaveCountAsync(3);
        await Expect(Cell("Pixel 8").Locator(".shiny-tv-value")).ToHaveTextAsync("offline");
        await Expect(Cell("MacBook Pro").Locator(".shiny-tv-value")).ToHaveTextAsync("online");
    }


    [Test]
    public async Task Button_Cell_Fires_OnClick()
    {
        await Open("tableview");
        await Expect(Page.GetByText("Account deleted (demo only).")).ToHaveCountAsync(0);

        await Page.GetByText("Delete account", new() { Exact = true }).ClickAsync();
        await Expect(Page.GetByText("Account deleted (demo only).")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }
}
