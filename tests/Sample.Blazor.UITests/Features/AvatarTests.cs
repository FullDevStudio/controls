using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>/avatar: Avatar and AvatarGroup.</summary>
public class AvatarTests : BlazorTest
{
    // The Ada Lovelace demo points at https://example.invalid on purpose, to show the initials fallback.
    const string BrokenPicture = "ERR_NAME_NOT_RESOLVED";

    ILocator Section(string heading)
        => Page.Locator("section.demo-section", new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });


    [Test]
    public async Task Picture_Initials_And_Silhouette()
    {
        await Open("avatar");
        var row = Section("Picture, Initials, Silhouette").Locator(".avatar-row > *");
        await Expect(row).ToHaveCountAsync(4);

        await Expect(row.Nth(1)).ToHaveTextAsync("AR");
        // a picture that fails to load falls back to the initials
        await Expect(row.Nth(2)).ToHaveTextAsync("AL");
        await Expect(row.Nth(2).Locator("img")).ToHaveCountAsync(0);
        // no name, no picture: the silhouette
        await Expect(row.Nth(3).Locator("svg")).ToHaveCountAsync(1);
        await AssertNoConsoleErrors(BrokenPicture);
    }


    [Test]
    public async Task Clicking_The_Avatar_Cycles_Its_Status()
    {
        await Open("avatar");
        var row = Section("Presence").Locator(".avatar-row").First;
        var avatar = row.Locator("button.shiny-avatar");
        await Expect(row).ToContainTextAsync("Online");

        await avatar.ClickAsync();
        await Expect(row).ToContainTextAsync("Away");
        await avatar.ClickAsync();
        await Expect(row).ToContainTextAsync("Busy");

        // a clickable avatar is a real button, so the keyboard works too
        await avatar.FocusAsync();
        await Page.Keyboard.PressAsync("Enter");
        await Expect(row).ToContainTextAsync("Offline");
        await AssertNoConsoleErrors(BrokenPicture);
    }


    [Test]
    public async Task Group_Collapses_Everyone_Past_Max_Into_A_Chip()
    {
        await Open("avatar");
        var group = Section("Avatar Group");
        await Expect(group.GetByText("+2", new() { Exact = true })).ToBeVisibleAsync();
        await AssertNoConsoleErrors(BrokenPicture);
    }


    [Test]
    public async Task Adding_And_Removing_People_Moves_The_Total()
    {
        await Open("avatar");
        var bound = Section("Bound, With a Total");
        await Expect(bound.GetByText("+43", new() { Exact = true })).ToBeVisibleAsync();

        await bound.GetByRole(AriaRole.Button, new() { Name = "Add person" }).ClickAsync();
        await Expect(bound.GetByText("+44", new() { Exact = true })).ToBeVisibleAsync();

        await bound.GetByRole(AriaRole.Button, new() { Name = "Remove" }).ClickAsync();
        await Expect(bound.GetByText("+43", new() { Exact = true })).ToBeVisibleAsync();
        await AssertNoConsoleErrors(BrokenPicture);
    }


    [Test]
    public async Task Clicking_The_Overflow_Chip_Raises_OnOverflowClick()
    {
        await Open("avatar");
        var bound = Section("Bound, With a Total");

        await bound.GetByText("+43", new() { Exact = true }).ClickAsync();
        await Expect(bound).ToContainTextAsync(new Regex(@"Clicked \+43 - this is where the full list of 48 would open\."));
        await AssertNoConsoleErrors(BrokenPicture);
    }
}
