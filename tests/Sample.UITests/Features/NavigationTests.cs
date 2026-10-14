namespace Sample.UITests.Features;

/// <summary>
/// ShinyNavigationPage on <c>navigationpage</c>: the demo stack opens modally, so each test opens a
/// fresh one and always closes it again - a modal left up would sit over every later test.
/// </summary>
[SkipOn(UIPlatform.Linux, "navigationpage is not in the Linux head")]
public class NavigationTests : UITest
{
    const string Status = "NavStatus";

    async Task OpenDemo(string button = "OpenNavDemo")
    {
        await CloseDemoIfOpen();
        await Open("navigationpage", "NavigationDemoPage");
        await Driver.Tap(automationId: button);
        await Driver.WaitUntilExists("NavInboxPage");
        await Driver.WaitUntilExists("nav-left-menu");
    }

    async Task CloseDemoIfOpen()
    {
        if (await Driver.Exists("GoBack"))
        {
            await Driver.Tap(automationId: "GoBack");
            await Driver.WaitUntilGone("NavMessagePage");
        }
        if (await Driver.Exists("CloseNavDemo"))
        {
            await Driver.Tap(automationId: "CloseNavDemo");
            await Driver.WaitUntilGone("NavInboxPage");
        }
    }

    async Task Run(Func<Task> body, string button = "OpenNavDemo")
    {
        await OpenDemo(button);
        try
        {
            await body();
        }
        finally
        {
            await CloseDemoIfOpen();
        }
    }

    [Test]
    [Arguments("nav-left-menu", "Left item: menu")]
    [Arguments("nav-right-search", "Right item: search")]
    [Arguments("nav-right-alerts", "Right item: alerts")]
    [Arguments("nav-right-drafts", "Toolbar item: drafts")]
    public async Task Bar_Items_On_Both_Sides_Raise_Their_Clicked(string item, string expected) => await Run(async () =>
    {
        await Driver.Tap(automationId: item);
        await Driver.WaitForProperty(Status, "Text", expected);
        await AssertNoAppErrors();
    });

    [Test]
    public async Task Push_And_Pop_Keep_The_Stack_And_Swap_The_Bar_Items() => await Run(async () =>
    {
        await Driver.Tap(automationId: "PushMessage");
        await Driver.WaitUntilExists("NavMessagePage");

        // the pushed page brings its own left item, alongside the back button
        await Driver.Tap(automationId: "nav-left-star");
        await Driver.WaitForProperty("NavMessageStatus", "Text", "Left item: starred");

        await Driver.Tap(automationId: "GoBack");
        await Driver.WaitUntilGone("NavMessagePage");
        await Driver.WaitUntilExists("nav-left-menu");
        await AssertNoAppErrors();
    });

    [Test]
    public async Task Item_Badge_Is_Bindable_At_Runtime() => await Run(async () =>
    {
        // alerts starts at 3, the button clears it, then sets 9
        await Driver.Tap(automationId: "ToggleBadge");
        await Driver.WaitForProperty(Status, "Text", "Badge is now off");
        await Driver.Tap(automationId: "ToggleBadge");
        await Driver.WaitForProperty(Status, "Text", "Badge is now 9");
        await AssertNoAppErrors();
    });

    [Test]
    public async Task Toolbar_Item_Badge_Is_Bindable_At_Runtime() => await Run(async () =>
    {
        await Driver.Tap(automationId: "ToggleToolbarBadge");
        await Driver.WaitForProperty(Status, "Text", "Toolbar item badge is now off");
        await Driver.Tap(automationId: "ToggleToolbarBadge");
        await Driver.WaitForProperty(Status, "Text", "Toolbar item badge is now 2");
        await AssertNoAppErrors();
    });

    [Test]
    public async Task Bar_Can_Be_Hidden_And_Shown_At_Runtime() => await Run(async () =>
    {
        await Driver.Tap(automationId: "HideBar");
        await Driver.WaitForProperty(Status, "Text", "Bar hidden");
        await Driver.WaitUntil(async () => !await Driver.IsDisplayed("nav-left-menu"), "the bar's items to go with it");

        await Driver.Tap(automationId: "HideBar");
        await Driver.WaitForProperty(Status, "Text", "Bar shown");
        await Driver.WaitUntil(() => Driver.IsDisplayed("nav-left-menu"), "the bar's items to come back");
        await AssertNoAppErrors();
    });

    [Test]
    [Arguments("OpenNavDemoLarge")]
    [Arguments("OpenNavDemoTinted")]
    public async Task Variants_Open_With_A_Working_Bar(string button) => await Run(async () =>
    {
        await Driver.Tap(automationId: "nav-right-search");
        await Driver.WaitForProperty(Status, "Text", "Right item: search");
        await AssertNoAppErrors();
    }, button);

    [Test]
    public async Task Close_Pops_The_Modal_Stack()
    {
        await OpenDemo();
        await Driver.Tap(automationId: "CloseNavDemo");
        await Driver.WaitUntilGone("NavInboxPage");
        await Driver.WaitUntilExists("OpenNavDemo");
        await AssertNoAppErrors();
    }
}
