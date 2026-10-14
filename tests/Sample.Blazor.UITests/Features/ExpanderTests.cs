using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class ExpanderTests : BlazorTest
{
    /// <summary>The expander whose built-in title reads exactly <paramref name="title"/>.</summary>
    ILocator Expander(string title)
        => Page.Locator(".shiny-expander").Filter(new()
        {
            Has = Page.Locator(".shiny-expander__title", new() { HasTextRegex = new Regex("^" + Regex.Escape(title) + "$") })
        });

    static ILocator Header(ILocator expander) => expander.Locator(".shiny-expander__header").First;

    ILocator ExpandedIndex => Page.Locator("p", new() { HasTextString = "ExpandedIndex:" }).Locator("strong");


    [Test]
    public async Task Clicking_The_Header_Toggles_The_Content()
    {
        await Open("expander");
        var shipping = Expander("Shipping");
        var header = Header(shipping);

        await Expect(header).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(shipping.Locator(".shiny-expander__panel")).ToHaveAttributeAsync("inert", "");

        await header.ClickAsync();
        await Expect(header).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(shipping).ToHaveClassAsync(new Regex(@"\bis-expanded\b"));
        await Expect(shipping.GetByText("123 Fake Street")).ToBeVisibleAsync();

        await header.ClickAsync();
        await Expect(header).ToHaveAttributeAsync("aria-expanded", "false");
        await Expect(shipping).ToHaveClassAsync(new Regex(@"\bis-collapsed\b"));
        await Expect(shipping.Locator(".shiny-expander__panel")).ToHaveAttributeAsync("aria-hidden", "true");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Keyboard_Enter_And_Space_Toggle_The_Header()
    {
        await Open("expander");
        var header = Header(Expander("Shipping"));

        await header.FocusAsync();
        await header.PressAsync("Enter");
        await Expect(header).ToHaveAttributeAsync("aria-expanded", "true");

        await header.PressAsync(" ");
        await Expect(header).ToHaveAttributeAsync("aria-expanded", "false");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Swap_Indicator_Changes_Glyph_When_Opened()
    {
        await Open("expander");
        var elevated = Expander("No border, elevated instead");
        var indicator = elevated.Locator(".shiny-expander__indicator");

        await Expect(indicator).ToHaveTextAsync("＋");
        await Header(elevated).ClickAsync();
        await Expect(indicator).ToHaveTextAsync("－");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Playground_Settings_Reach_The_Expander()
    {
        await Open("expander");
        var playground = Expander("Try the settings above");

        await Page.Locator("label", new() { HasTextString = "Direction" }).Locator("select").SelectOptionAsync("Up");
        await Expect(playground).ToHaveClassAsync(new Regex(@"\bdir-up\b"));

        await Page.Locator("label", new() { HasTextString = "Indicator" }).First.Locator("select").SelectOptionAsync("Swap");
        await Expect(playground).ToHaveClassAsync(new Regex(@"\bind-swap\b"));

        // The Height flag drives the anim-height class.
        await Page.Locator("label", new() { HasTextString = "Height" }).Locator("input[type=checkbox]").UncheckAsync();
        await Expect(playground).Not.ToHaveClassAsync(new Regex(@"\banim-height\b"));

        await Page.Locator("input[type=range]").FillAsync("600");
        await Expect(playground.Locator(".shiny-expander__detail")).ToHaveTextAsync("600ms");
        await Expect(playground).ToHaveAttributeAsync("style", new Regex("--shiny-expander-duration:600ms"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Accordion_Single_Mode_Keeps_One_Open_And_Reports_Index()
    {
        await Open("expander");
        await Expect(ExpandedIndex).ToHaveTextAsync("-1");

        await Header(Expander("Account")).ClickAsync();
        await Expect(ExpandedIndex).ToHaveTextAsync("0");

        await Header(Expander("Billing")).ClickAsync();
        await Expect(ExpandedIndex).ToHaveTextAsync("1");
        await Expect(Header(Expander("Billing"))).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Header(Expander("Account"))).ToHaveAttributeAsync("aria-expanded", "false");

        // Closing the only open item is allowed by default.
        await Header(Expander("Billing")).ClickAsync();
        await Expect(ExpandedIndex).ToHaveTextAsync("-1");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Accordion_Multiple_Mode_Allows_Several_Open()
    {
        await Open("expander");
        await Page.Locator("label", new() { HasTextString = "Mode" }).Locator("select").SelectOptionAsync("Multiple");

        await Header(Expander("Account")).ClickAsync();
        await Header(Expander("Notifications")).ClickAsync();

        await Expect(Header(Expander("Account"))).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(Header(Expander("Notifications"))).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(ExpandedIndex).ToHaveTextAsync("0");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Accordion_Disallowing_All_Closed_Refuses_To_Close_The_Last_Open_Item()
    {
        await Open("expander");
        await Page.Locator("label", new() { HasTextString = "Allow all closed" }).Locator("input[type=checkbox]").UncheckAsync();

        var account = Header(Expander("Account"));
        await account.ClickAsync();
        await Expect(account).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(ExpandedIndex).ToHaveTextAsync("0");

        // The last open item stops responding to clicks...
        await account.ClickAsync();
        await Expect(account).ToHaveAttributeAsync("aria-expanded", "true");
        await Expect(ExpandedIndex).ToHaveTextAsync("0");

        // ...but opening another (single mode) still moves the selection.
        await Header(Expander("Billing")).ClickAsync();
        await Expect(ExpandedIndex).ToHaveTextAsync("1");
        await Expect(account).ToHaveAttributeAsync("aria-expanded", "false");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Accordion_Disallowing_All_Closed_Opens_The_First_Item()
    {
        await Open("expander");
        await Expect(ExpandedIndex).ToHaveTextAsync("-1");
        await Page.Locator("label", new() { HasTextString = "Allow all closed" }).Locator("input[type=checkbox]").UncheckAsync();

        await Expect(ExpandedIndex).ToHaveTextAsync("0");
        await Expect(Header(Expander("Account"))).ToHaveAttributeAsync("aria-expanded", "true");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Data_Accordion_Raises_ItemExpanded()
    {
        await Open("expander");
        var lastEvent = Page.Locator("p", new() { HasTextString = "Last event:" }).Locator("strong");
        await Expect(lastEvent).ToHaveTextAsync("nothing yet");

        await Header(Expander("Does it need JS interop?")).ClickAsync();
        await Expect(lastEvent).ToHaveTextAsync("opened item 1");
        await Expect(Page.GetByText("The expander does not — the reveal is a CSS grid-row transition.")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Data_Accordion_Loads_Content_On_Demand()
    {
        await Open("expander");
        var item = Expander("Is the source available?");

        await Expect(item.GetByText("MIT licensed, on GitHub at shinyorg/controls.")).ToHaveCountAsync(0);
        await Header(item).ClickAsync();
        await Expect(item.GetByText("MIT licensed, on GitHub at shinyorg/controls.")).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }
}
