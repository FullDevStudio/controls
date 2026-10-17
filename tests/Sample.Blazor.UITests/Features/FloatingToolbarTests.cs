using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>
/// FloatingToolbar: one bar anchored to whichever card triggered it, item clicks carrying the target
/// index, and the second bar's dropdown menus and dismiss-on-click.
/// </summary>
public class FloatingToolbarTests : BlazorTest
{
    ILocator Card(int n) => Page.Locator(".fbar-card", new() { HasText = $"Card {n}" });
    ILocator LastAction => Page.Locator("p.state").First;

    ILocator Select(string label) => Page.Locator(".row label", new() { HasText = label }).Locator("select");

    [Test]
    public async Task Hover_Shows_The_Bar_And_Item_Click_Names_The_Card()
    {
        await Open("/floatingtoolbar");
        await Expect(LastAction).ToHaveTextAsync("Nothing clicked yet");

        await Card(2).HoverAsync();
        var bar = Page.GetByRole(AriaRole.Toolbar).Filter(new() { Visible = true });
        await Expect(bar).ToHaveCountAsync(1);

        await bar.GetByRole(AriaRole.Button, new() { Name = "Cut" }).ClickAsync();

        await Expect(LastAction).ToHaveTextAsync("Cut on target #1");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Click_Trigger_Reanchors_To_Another_Card()
    {
        await Open("/floatingtoolbar");
        await Select("Trigger").SelectOptionAsync("Click");

        await Card(1).ClickAsync();
        var bar = Page.GetByRole(AriaRole.Toolbar).Filter(new() { Visible = true });
        await Expect(bar).ToHaveCountAsync(1);

        await Card(4).ClickAsync();
        await Expect(bar).ToHaveCountAsync(1);
        await bar.GetByRole(AriaRole.Button, new() { Name = "Copy" }).ClickAsync();

        await Expect(LastAction).ToHaveTextAsync("Copy on target #3");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Orientation_And_Labels_Reach_The_Bar()
    {
        await Open("/floatingtoolbar");
        await Select("Orientation").SelectOptionAsync("Vertical");
        await Page.Locator(".row label", new() { HasText = "ShowLabels" }).Locator("input").CheckAsync();

        await Card(3).HoverAsync();
        var bar = Page.GetByRole(AriaRole.Toolbar).Filter(new() { Visible = true });
        await Expect(bar).ToHaveAttributeAsync("aria-orientation", "vertical");
        await Expect(bar.Locator(".shiny-fbar-label", new() { HasText = "Cut" })).ToBeVisibleAsync();
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Dropdown_Item_Fires_And_Dismisses_The_Bar()
    {
        await Open("/floatingtoolbar");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open", Exact = true }).ClickAsync();

        var bar = Page.GetByRole(AriaRole.Toolbar).Filter(new() { Visible = true });
        await Expect(bar).ToHaveCountAsync(1);

        // On a phone the bar is too narrow for Format, which folds into "More actions" - reached
        // from there as a submenu, the same items either way.
        var format = bar.GetByRole(AriaRole.Button, new() { Name = "Format" });
        if (await format.CountAsync() == 0)
        {
            var more = bar.GetByRole(AriaRole.Button, new() { Name = "More actions" });
            await Expect(more).ToHaveAttributeAsync("aria-haspopup", "true");
            await more.ClickAsync();
            format = Page.GetByRole(AriaRole.Menuitem, new() { Name = "Format" }).Filter(new() { Visible = true });
        }
        await Expect(format).ToHaveAttributeAsync("aria-haspopup", "true");
        await format.ClickAsync();

        var bold = Page.GetByRole(AriaRole.Menu).Filter(new() { Visible = true }).GetByText("Bold", new() { Exact = true });
        await Expect(bold).ToBeVisibleAsync();
        await bold.ClickAsync();

        await Expect(Page.Locator("p.state").First).ToHaveTextAsync(new Regex("^Bold on target #0$"));
        // DismissOnItemClick
        await Expect(bar).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Max_Visible_Items_Folds_The_Rest_Into_Overflow()
    {
        await Open("/floatingtoolbar");
        await Select("MaxVisibleItems").SelectOptionAsync("3");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open", Exact = true }).ClickAsync();

        var bar = Page.GetByRole(AriaRole.Toolbar).Filter(new() { Visible = true });
        // the cap counts the overflow button itself
        await Expect(bar.Locator("button.shiny-fbar-cell")).ToHaveCountAsync(3);
        await Expect(bar.GetByRole(AriaRole.Button, new() { Name = "Rename" })).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }
}
