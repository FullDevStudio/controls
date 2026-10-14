using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>ShinyTabBar on <c>/tabbar</c>: selection binding, active state, badges, label-less variant.</summary>
public class TabBarTests : BlazorTest
{
    static readonly Regex Active = new(@"\bshiny-tabbar-item--active\b");

    // the sample layout has its own tab bar in the footer, so stay inside the mock phones
    ILocator FirstBar => Page.Locator(".phone .shiny-tabbar").Nth(0);
    ILocator SecondBar => Page.Locator(".phone .shiny-tabbar").Nth(1);
    ILocator SelectedKey => Page.Locator("p.state strong");

    ILocator Item(ILocator bar, string label) => bar.Locator(".shiny-tabbar-item").Filter(new() { HasTextString = label });

    [Test]
    public async Task Clicking_A_Tab_Selects_It_And_Updates_The_Binding()
    {
        await Open("tabbar");
        await Expect(SelectedKey).ToHaveTextAsync("home");
        await Expect(Item(FirstBar, "Home")).ToHaveClassAsync(Active);

        await Item(FirstBar, "Search").ClickAsync();
        await Expect(SelectedKey).ToHaveTextAsync("search");
        await Expect(Item(FirstBar, "Search")).ToHaveClassAsync(Active);
        await Expect(Item(FirstBar, "Home")).Not.ToHaveClassAsync(Active);
        await Expect(FirstBar.Locator(".shiny-tabbar-item--active")).ToHaveCountAsync(1);

        // the content above follows the bound key
        await Expect(Page.Locator(".phone-body--photo h3")).ToHaveTextAsync("Search");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Active_Tab_Takes_The_Active_Colour()
    {
        await Open("tabbar");
        await Item(FirstBar, "Chat").ClickAsync();
        await Expect(Item(FirstBar, "Chat")).ToHaveCSSAsync("color", "rgb(124, 58, 237)"); // #7C3AED
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Badges_Show_Count_And_Dot()
    {
        await Open("tabbar");
        await Expect(Item(FirstBar, "Chat").Locator(".shiny-tabbar-item-badge")).ToHaveTextAsync("5");
        await Expect(Item(FirstBar, "Profile").Locator(".shiny-tabbar-item-badge--dot")).ToHaveCountAsync(1);
        await Expect(Item(FirstBar, "Home").Locator(".shiny-tabbar-item-badge")).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Icon_Only_Bar_Has_No_Labels_And_Its_Own_Selection()
    {
        await Open("tabbar");
        await Expect(SecondBar).ToHaveClassAsync(new Regex(@"\bshiny-tabbar--no-labels\b"));
        await Expect(SecondBar.Locator(".shiny-tabbar-item-label")).ToHaveCountAsync(0);

        // third item = Chat; selecting it must not move the first bar
        await SecondBar.Locator(".shiny-tabbar-item").Nth(2).ClickAsync();
        await Expect(SecondBar.Locator(".shiny-tabbar-item").Nth(2)).ToHaveClassAsync(Active);
        await Expect(SelectedKey).ToHaveTextAsync("home");
        await Expect(Item(FirstBar, "Home")).ToHaveClassAsync(Active);
        await AssertNoConsoleErrors();
    }
}
