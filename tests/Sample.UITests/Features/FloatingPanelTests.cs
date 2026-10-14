namespace Sample.UITests.Features;

/// <summary>
/// FloatingPanel / ShinyContentPage.Panels across <c>sheet</c>, <c>minimizedsheetstandalone</c>,
/// <c>minimizedsheet</c>, <c>topsheet</c> and <c>dualpanel</c>. IsOpen is two-way, so the panels are
/// driven both from the view-model's buttons and by setting IsOpen on the panel itself, and the
/// view-model's status line proves the value made the round trip.
/// </summary>
[SkipOn(UIPlatform.Linux, "the floating panel pages are not in the Linux head")]
public class FloatingPanelTests : UITest
{
    async Task Closed(params string[] panels)
    {
        foreach (var panel in panels)
        {
            if (await Driver.GetProperty("IsOpen", automationId: panel) != "False")
                await Driver.SetProperty("IsOpen", "False", automationId: panel);
            await Driver.WaitForProperty(panel, "IsOpen", "False");
        }
    }

    // ── sheet ────────────────────────────────────────────────────────────────

    async Task OpenSheetPage()
    {
        await Open("sheet", "SheetPage");
        await Closed("MainSheet", "TopSheetPanel", "SignatureSheet", "CountrySelectorSheet");
    }

    [Test]
    public async Task Sheet_Opens_From_The_Page_And_Closes_From_Inside()
    {
        await OpenSheetPage();
        await Driver.WaitForProperty("SheetStatus", "Text", "Sheet is closed");

        await Driver.Tap(automationId: "OpenSheetButton");
        await Driver.WaitForProperty("MainSheet", "IsOpen", "True");
        await Driver.WaitForProperty("SheetStatus", "Text", "Sheet is open");

        await Driver.Tap(automationId: "CloseSheetInsideButton");
        await Driver.WaitForProperty("MainSheet", "IsOpen", "False");
        await Driver.WaitForProperty("SheetStatus", "Text", "Sheet is closed");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Sheet_Toggle_Command_Flips_It()
    {
        await OpenSheetPage();

        await Driver.Tap(automationId: "ToggleSheetButton");
        await Driver.WaitForProperty("MainSheet", "IsOpen", "True");
        await Driver.Tap(automationId: "ToggleSheetButton");
        await Driver.WaitForProperty("MainSheet", "IsOpen", "False");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Setting_IsOpen_On_The_Panel_Writes_Back_To_The_ViewModel()
    {
        await OpenSheetPage();

        // what a swipe-to-dismiss does: the panel changes itself and the binding carries it back
        await Driver.SetProperty("IsOpen", "True", automationId: "MainSheet");
        await Driver.WaitForProperty("SheetStatus", "Text", "Sheet is open");

        await Driver.SetProperty("IsOpen", "False", automationId: "MainSheet");
        await Driver.WaitForProperty("SheetStatus", "Text", "Sheet is closed");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Top_Sheet_Opens_From_The_Top_And_Closes()
    {
        await OpenSheetPage();
        await Driver.WaitForProperty("TopSheetPanel", "Position", "Top");

        await Driver.Tap(automationId: "OpenTopSheetButton");
        await Driver.WaitForProperty("TopSheetPanel", "IsOpen", "True");

        await Driver.Tap(automationId: "CloseTopSheetButton");
        await Driver.WaitForProperty("TopSheetPanel", "IsOpen", "False");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Locked_Signature_Sheet_Closes_Only_From_Its_Own_Controls()
    {
        await OpenSheetPage();
        await Driver.WaitForProperty("SignatureSheet", "IsLocked", "True");

        await Driver.Tap(automationId: "OpenSignatureButton");
        await Driver.WaitForProperty("SignatureSheet", "IsOpen", "True");

        await Driver.Tap(automationId: "CancelSignatureButton");
        await Driver.WaitForProperty("SignatureSheet", "IsOpen", "False");
        await AssertNoAppErrors();
    }

    [Test]
    [Skip("DevFlow can't drive it: CollectionView.SelectedItem is typed object, so set-property refuses a string ('Cannot convert Japan to Object'), "
        + "and a cell has no gesture recognizer for tap/gesture to invoke - native CollectionView selection is out of DevFlow's reach on every head")]
    public async Task Country_Selection_Closes_The_Locked_Selector_And_Shows_The_Choice()
    {
        await OpenSheetPage();
        await Driver.Tap(automationId: "OpenSelectorButton");
        await Driver.WaitForProperty("CountrySelectorSheet", "IsOpen", "True");

        // CollectionView keeps its selection across opens, so pick something it is not already on
        var current = await Driver.GetProperty("SelectedItem", automationId: "CountryList");
        var country = current == "Japan" ? "Kenya" : "Japan";
        await Driver.SetProperty("SelectedItem", country, automationId: "CountryList");

        await Driver.WaitForProperty("CountrySelectorSheet", "IsOpen", "False");
        await Driver.WaitForProperty("SelectedCountryLabel", "Text", $"Selected: {country}");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Country_Selector_Cancel_Closes_It()
    {
        await OpenSheetPage();
        await Driver.Tap(automationId: "OpenSelectorButton");
        await Driver.WaitForProperty("CountrySelectorSheet", "IsOpen", "True");

        await Driver.Tap(automationId: "CancelSelectorButton");
        await Driver.WaitForProperty("CountrySelectorSheet", "IsOpen", "False");
        await AssertNoAppErrors();
    }

    // ── header peek (bottom and BottomTabs) ─────────────────────────────────

    [Test]
    [Arguments("minimizedsheetstandalone", "MinimizedSheetStandalonePage", "Peek", "Bottom")]
    [Arguments("minimizedsheet", "MinimizedSheetPage", "TabsPeek", "BottomTabs")]
    public async Task Peeking_Header_Stays_While_Closed_And_The_Player_Opens(string route, string page, string prefix, string position)
    {
        // sibling ShellContent switches don't present on AppKit (KnownGaps.AppKitSiblingShellContentSwitch)
        if (Platform == UIPlatform.MacOS)
            await Open("home");
        await Open(route, page);
        await Closed(prefix + "Panel");
        await Driver.WaitForProperty(prefix + "Panel", "Position", position);
        await Driver.WaitForProperty(prefix + "Status", "Text", "Music player header peeks from the bottom");

        // ShowHeaderWhenClosed: the mini player is on screen with the panel shut
        await Driver.WaitForProperty(prefix + "Panel", "ShowHeaderWhenClosed", "True");
        await Driver.WaitUntil(() => Driver.IsElementVisible(prefix + "HeaderTitle"), "the peeking header");

        await Driver.Tap(automationId: prefix + "OpenButton");
        await Driver.WaitForProperty(prefix + "Panel", "IsOpen", "True");
        await Driver.WaitForProperty(prefix + "Status", "Text", "Music player is open");

        await Driver.Tap(automationId: prefix + "ClosePlayerButton");
        await Driver.WaitForProperty(prefix + "Panel", "IsOpen", "False");
        await Driver.WaitForProperty(prefix + "Status", "Text", "Music player header peeks from the bottom");
        await Driver.WaitUntil(() => Driver.IsElementVisible(prefix + "HeaderTitle"), "the header to keep peeking");
        await AssertNoAppErrors();
    }

    // ── top sheet ────────────────────────────────────────────────────────────

    [Test]
    public async Task Weather_Top_Sheet_Peeks_And_Expands()
    {
        await Open("topsheet", "TopSheetPage");
        await Closed("WeatherPanel");
        await Driver.WaitForProperty("WeatherPanel", "Position", "Top");
        await Driver.WaitForProperty("WeatherStatus", "Text", "Weather header peeks from the top");
        await Driver.WaitUntil(() => Driver.IsElementVisible("WeatherHeaderTitle"), "the peeking weather header");

        await Driver.Tap(automationId: "WeatherOpenButton");
        await Driver.WaitForProperty("WeatherPanel", "IsOpen", "True");
        await Driver.WaitForProperty("WeatherStatus", "Text", "Weather details are open");

        await Driver.Tap(automationId: "WeatherClosePanelButton");
        await Driver.WaitForProperty("WeatherPanel", "IsOpen", "False");
        await Driver.WaitForProperty("WeatherStatus", "Text", "Weather header peeks from the top");
        await AssertNoAppErrors();
    }

    // ── dual panels ──────────────────────────────────────────────────────────

    [Test]
    public async Task Top_And_Bottom_Panels_Open_Independently()
    {
        await Open("dualpanel", "DualPanelPage");
        await Driver.Tap(automationId: "DualCloseBoth");
        await Driver.WaitForProperty("DualStatus", "Text", "Both panels have peek headers");

        await Driver.Tap(automationId: "DualOpenTop");
        await Driver.WaitForProperty("DualTopPanel", "IsOpen", "True");
        await Driver.WaitForProperty("DualStatus", "Text", "Top panel open");
        await Driver.WaitForProperty("DualBottomPanel", "IsOpen", "False");

        await Driver.Tap(automationId: "DualOpenBottom");
        await Driver.WaitForProperty("DualStatus", "Text", "Both panels open");

        await Driver.Tap(automationId: "DualCloseTop");
        await Driver.WaitForProperty("DualTopPanel", "IsOpen", "False");
        await Driver.WaitForProperty("DualStatus", "Text", "Bottom panel open");

        await Driver.Tap(automationId: "DualCloseBottom");
        await Driver.WaitForProperty("DualStatus", "Text", "Both panels have peek headers");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Open_Both_Then_Close_From_Inside_The_Top_Panel()
    {
        await Open("dualpanel", "DualPanelPage");
        await Driver.Tap(automationId: "DualOpenBoth");
        await Driver.WaitForProperty("DualTopPanel", "IsOpen", "True");
        await Driver.WaitForProperty("DualBottomPanel", "IsOpen", "True");
        await Driver.WaitForProperty("DualStatus", "Text", "Both panels open");

        await Driver.Tap(automationId: "DualMarkAllRead");
        await Driver.WaitForProperty("DualTopPanel", "IsOpen", "False");
        await Driver.WaitForProperty("DualStatus", "Text", "Bottom panel open");

        await Driver.Tap(automationId: "DualCloseBoth");
        await Driver.WaitForProperty("DualStatus", "Text", "Both panels have peek headers");
        await AssertNoAppErrors();
    }
}
