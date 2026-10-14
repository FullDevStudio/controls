using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>ModalView on <c>/modal</c>: every dismissal route, its reported reason, the veto, and stacking.</summary>
public class ModalTests : BlazorTest
{
    ILocator OpenModals => Page.Locator(".shiny-modal-root.is-open");
    ILocator TopDialog => Page.Locator(".shiny-modal-root.is-open [role=dialog]").Last;

    // "not opened yet" / "closed — Backdrop" status line of the first section
    ILocator BasicState => Page.Locator(".demo-section").Nth(0).Locator("p.state");

    Task OpenBasic() => Page.GetByRole(AriaRole.Button, new() { Name = "Open modal", Exact = true }).ClickAsync();

    [Test]
    public async Task Opens_With_Title_Subtitle_And_Footer_Buttons()
    {
        await Open("modal");
        await OpenBasic();

        await Expect(OpenModals).ToHaveCountAsync(1);
        var dialog = TopDialog;
        await Expect(dialog).ToHaveAttributeAsync("aria-modal", "true");
        await Expect(dialog.Locator(".shiny-modal-title")).ToHaveTextAsync("Delete project");
        await Expect(dialog.Locator(".shiny-modal-subtitle")).ToHaveTextAsync("This cannot be undone");
        await Expect(dialog.Locator(".shiny-modal-footer button")).ToHaveCountAsync(2);
        await AssertNoConsoleErrors();
    }

    [Test]
    [Arguments("Escape")]
    [Arguments("CloseButton")]
    [Arguments("Backdrop")]
    [Arguments("Button")]
    public async Task Each_Dismissal_Route_Closes_And_Reports_Its_Reason(string route)
    {
        await Open("modal");
        await OpenBasic();
        await Expect(OpenModals).ToHaveCountAsync(1);

        switch (route)
        {
            case "Escape":
                await Page.Keyboard.PressAsync("Escape");
                break;
            case "CloseButton":
                await TopDialog.Locator(".shiny-modal-close").ClickAsync();
                break;
            case "Backdrop":
                await Page.Locator(".shiny-modal-root.is-open .shiny-modal-backdrop").ClickAsync(new() { Position = new() { X = 10, Y = 10 }, Force = true });
                break;
            case "Button":
                await TopDialog.GetByRole(AriaRole.Button, new() { Name = "Cancel", Exact = true }).ClickAsync();
                break;
        }

        await Expect(OpenModals).ToHaveCountAsync(0);
        await Expect(BasicState).ToHaveTextAsync($"closed — {route}");
        await AssertNoConsoleErrors();
    }

    [Test]
    [Skip("BUG: a ModalButton.OnClick that sets the bound IsOpen=false never closes the modal - OnClick is a Func<Task>, not an EventCallback, so the page never re-renders and the false never reaches ModalView (the sample's Delete project modal stays open forever)")]
    public async Task Busy_Button_Keeps_Modal_Open_Until_Its_Work_Finishes()
    {
        await Open("modal");
        await OpenBasic();

        await TopDialog.GetByRole(AriaRole.Button, new() { Name = "Delete project", Exact = true }).ClickAsync();

        // ClosesModal = false: still up while the 900ms "request" runs
        await Page.WaitForTimeoutAsync(300);
        await Expect(OpenModals).ToHaveCountAsync(1);

        await Expect(OpenModals).ToHaveCountAsync(0, new() { Timeout = 5000 });
        await Expect(BasicState).ToHaveTextAsync("closed — Programmatic");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Bare_Modal_Refuses_Backdrop_And_Escape()
    {
        await Open("modal");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open bare", Exact = true }).ClickAsync();
        await Expect(OpenModals).ToHaveCountAsync(1);
        await Expect(TopDialog.Locator(".shiny-modal-header")).ToHaveCountAsync(0);

        await Page.Keyboard.PressAsync("Escape");
        await Page.Locator(".shiny-modal-root.is-open .shiny-modal-backdrop").ClickAsync(new() { Position = new() { X = 10, Y = 10 }, Force = true });
        await Page.WaitForTimeoutAsync(400);
        await Expect(OpenModals).ToHaveCountAsync(1);

        await TopDialog.GetByRole(AriaRole.Button, new() { Name = "Get started", Exact = true }).ClickAsync();
        await Expect(OpenModals).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Closing_Veto_Refuses_Dirty_Note_Until_Discarded()
    {
        await Open("modal");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open guarded", Exact = true }).ClickAsync();
        await Expect(OpenModals).ToHaveCountAsync(1);

        await TopDialog.Locator("textarea").FillAsync("half a thought");
        await TopDialog.Locator(".shiny-modal-footer").GetByRole(AriaRole.Button, new() { Name = "Close", Exact = true }).ClickAsync();

        await Expect(TopDialog.Locator("p.state")).ToContainTextAsync("Refused a Button dismissal");
        await Expect(OpenModals).ToHaveCountAsync(1);

        await Page.Keyboard.PressAsync("Escape");
        await Expect(TopDialog.Locator("p.state")).ToContainTextAsync("Refused a Escape dismissal");
        await Expect(OpenModals).ToHaveCountAsync(1);

        await TopDialog.GetByRole(AriaRole.Button, new() { Name = "Discard", Exact = true }).ClickAsync();
        await Expect(OpenModals).ToHaveCountAsync(0);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Stacked_Modals_Escape_Closes_Only_The_Top_One()
    {
        await Open("modal");
        await Page.GetByRole(AriaRole.Button, new() { Name = "Open first", Exact = true }).ClickAsync();
        await Expect(OpenModals).ToHaveCountAsync(1);

        await TopDialog.GetByRole(AriaRole.Button, new() { Name = "Reset everything…", Exact = true }).ClickAsync();
        await Expect(OpenModals).ToHaveCountAsync(2);
        await Expect(TopDialog.Locator(".shiny-modal-title")).ToHaveTextAsync("Are you sure?");

        await Page.Keyboard.PressAsync("Escape");
        await Expect(OpenModals).ToHaveCountAsync(1);
        await Expect(TopDialog.Locator(".shiny-modal-title")).ToHaveTextAsync("Settings");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Size_Selection_Applies_To_The_Panel()
    {
        await Open("modal");
        var section = Page.Locator(".demo-section").Nth(1);
        await section.Locator("select").Nth(0).SelectOptionAsync("Large");
        await section.GetByRole(AriaRole.Button, new() { Name = "Open", Exact = true }).ClickAsync();

        await Expect(OpenModals).ToHaveCountAsync(1);
        await Expect(TopDialog).ToHaveClassAsync(new Regex(@"\bsize-large\b"));
        await Expect(TopDialog.Locator(".shiny-modal-title")).ToContainTextAsync("Large");
        await AssertNoConsoleErrors();
    }
}
