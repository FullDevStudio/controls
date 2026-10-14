namespace Sample.UITests.Features;

public class ExpanderTests : UITest
{
    /// <summary>
    /// Opens the page and puts back what other tests change: Shell keeps this page and its view-model
    /// for the whole session, so every test starts from the page's declared defaults.
    /// </summary>
    async Task OpenPage()
    {
        await Open("expander", "ExpanderPage");

        await Driver.SetProperty("SelectedIndex", "0", automationId: "SelectionModePicker");   // Single
        await Driver.WaitForProperty("StaticAccordion", "SelectionMode", "Single");
        await Driver.SetProperty("IsToggled", "True", automationId: "AllowCollapseAllSwitch");
        await Driver.WaitForProperty("StaticAccordion", "AllowCollapseAll", "True");
        await Driver.SetProperty("ExpandedIndex", "-1", automationId: "StaticAccordion");
        foreach (var id in new[] { "AccountExpander", "BillingExpander", "NotificationsExpander", "BasicExpander", "ElevatedExpander", "PlaygroundExpander" })
        {
            if (await Driver.GetProperty("IsExpanded", automationId: id) != "False")
                await Driver.SetProperty("IsExpanded", "False", automationId: id);
        }
        await Driver.SetProperty("SelectedIndex", "0", automationId: "DirectionPicker");        // Down
        await Driver.SetProperty("Value", "250", automationId: "DurationSlider");
        await Driver.WaitForProperty("ExpandedIndexLabel", "Text", "ExpandedIndex: -1");
    }


    // devflow on the macOS head: IsExpanded=True lands, "123 Fake Street" keeps bounds -1 indefinitely,
    // and a `ui resize` of the window lays it out at once (1036x17) - the layout is right, nobody runs it.
    [Test, SkipOn(UIPlatform.MacOS, KnownGaps.AppKitNoRelayoutOnChange)]
    public async Task Expanding_Lays_Out_The_Content()
    {
        await OpenPage();
        await Driver.WaitForProperty("BasicExpander", "IsExpanded", "False");

        await Driver.SetProperty("IsExpanded", "True", automationId: "BasicExpander");
        await Driver.WaitForProperty("BasicExpander", "IsExpanded", "True");

        // The content panel is hidden while collapsed, so its first line only gets a real height once revealed.
        await Driver.WaitUntil(async () =>
        {
            var line = (await Driver.Query(text: "123 Fake Street")).FirstOrDefault();
            if (line == null)
                return false;

            var height = await Driver.GetProperty("Height", elementId: line.Id);
            return Double.TryParse(height, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h) && h > 0;
        }, "the expanded content to be laid out", timeoutSeconds: 10);

        await Driver.SetProperty("IsExpanded", "False", automationId: "BasicExpander");
        await Driver.WaitForProperty("BasicExpander", "IsExpanded", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Swap_Indicator_Changes_Glyph()
    {
        await OpenPage();
        await Driver.WaitUntilTextExists("＋");

        await Driver.SetProperty("IsExpanded", "True", automationId: "ElevatedExpander");
        await Driver.WaitUntilTextExists("－", timeoutSeconds: 10);
        await Assert.That((await Driver.Query(text: "＋")).Count).IsEqualTo(0);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Playground_Duration_Reaches_The_Expander()
    {
        await OpenPage();

        await Driver.SetProperty("Value", "600", automationId: "DurationSlider");
        await Driver.WaitForProperty("PlaygroundExpander", "AnimationDuration", "600");
        await Driver.WaitForProperty("PlaygroundExpander", "HeaderDetail", "600ms");
    }


    [Test]
    public async Task Playground_Direction_Picker_Flips_Expander()
    {
        await OpenPage();

        // ExpandDirection: Down, Up
        await Driver.SetProperty("SelectedIndex", "1", automationId: "DirectionPicker");
        await Driver.WaitForProperty("PlaygroundExpander", "ExpandDirection", "Up");

        await Driver.SetProperty("IsExpanded", "True", automationId: "PlaygroundExpander");
        await Driver.WaitForProperty("PlaygroundExpander", "IsExpanded", "True");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Accordion_Single_Mode_Keeps_One_Open_And_Reports_Index()
    {
        await OpenPage();
        await Driver.WaitForProperty("ExpandedIndexLabel", "Text", "ExpandedIndex: -1");

        await Driver.SetProperty("IsExpanded", "True", automationId: "AccountExpander");
        await Driver.WaitForProperty("ExpandedIndexLabel", "Text", "ExpandedIndex: 0");

        await Driver.SetProperty("IsExpanded", "True", automationId: "BillingExpander");
        await Driver.WaitForProperty("ExpandedIndexLabel", "Text", "ExpandedIndex: 1");
        await Driver.WaitForProperty("AccountExpander", "IsExpanded", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Accordion_ExpandedIndex_Drives_The_Items()
    {
        await OpenPage();

        await Driver.SetProperty("ExpandedIndex", "2", automationId: "StaticAccordion");
        await Driver.WaitForProperty("NotificationsExpander", "IsExpanded", "True");
        await Driver.WaitForProperty("ExpandedIndexLabel", "Text", "ExpandedIndex: 2");

        await Driver.SetProperty("ExpandedIndex", "-1", automationId: "StaticAccordion");
        await Driver.WaitForProperty("NotificationsExpander", "IsExpanded", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Accordion_Multiple_Mode_Allows_Several_Open()
    {
        await OpenPage();

        // AccordionSelectionMode: Single, Multiple
        await Driver.SetProperty("SelectedIndex", "1", automationId: "SelectionModePicker");
        await Driver.WaitForProperty("StaticAccordion", "SelectionMode", "Multiple");

        await Driver.SetProperty("IsExpanded", "True", automationId: "AccountExpander");
        await Driver.SetProperty("IsExpanded", "True", automationId: "NotificationsExpander");

        await Driver.WaitForProperty("AccountExpander", "IsExpanded", "True");
        await Driver.WaitForProperty("NotificationsExpander", "IsExpanded", "True");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Accordion_Disallowing_All_Closed_Opens_The_First_Item()
    {
        await OpenPage();

        await Driver.SetProperty("IsToggled", "False", automationId: "AllowCollapseAllSwitch");
        await Driver.WaitForProperty("StaticAccordion", "AllowCollapseAll", "False");
        await Driver.WaitForProperty("AccountExpander", "IsExpanded", "True");

        // The open item loses its close affordance.
        await Driver.WaitForProperty("AccountExpander", "CanCollapse", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Accordion_Disallowing_All_Closed_Publishes_The_Index()
    {
        await OpenPage();

        await Driver.SetProperty("IsToggled", "False", automationId: "AllowCollapseAllSwitch");
        await Driver.WaitForProperty("AccountExpander", "IsExpanded", "True");
        await Driver.WaitForProperty("ExpandedIndexLabel", "Text", "ExpandedIndex: 0");
    }


    [Test]
    public async Task Data_Accordion_Runs_ItemExpandedCommand()
    {
        await OpenPage();
        // LastEvent lives in the view-model for the session: open another item first so the
        // assertion below can only pass on this test's own event.
        await Driver.SetProperty("ExpandedIndex", "0", automationId: "DataAccordion");
        await Driver.WaitForProperty("LastEventLabel", "Text", "Last event: opened \"How do I install it?\"");

        await Driver.SetProperty("ExpandedIndex", "1", automationId: "DataAccordion");
        await Driver.WaitForProperty("LastEventLabel", "Text", "Last event: opened \"Does it work on Windows?\"");

        // Content is built on first open.
        await Driver.WaitUntilTextExists("Yes — iOS, Android, Mac Catalyst, macOS, Windows and GTK4 on Linux.", timeoutSeconds: 10);
        await AssertNoAppErrors();
    }
}
