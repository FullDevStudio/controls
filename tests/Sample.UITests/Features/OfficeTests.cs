namespace Sample.UITests.Features;

// The Office controls draw their content on a Skia canvas DevFlow cannot tap, so these tests drive the
// sample pages' own toolbar Buttons and the views' bindable properties, and assert on the labels the
// pages bind to them. Shell keeps these pages (and their open documents) alive between visits, so every
// test either resets the state it depends on or asserts relative to what it read first.
//
// None of the Office pages are in the Linux head's AppShell.

static class OfficeUi
{
    public static async Task<bool> Toggle(MauiDevFlowDriver driver, string button, string view, string property)
    {
        var before = await driver.GetProperty(property, automationId: view);
        var expected = String.Equals(before, "True", StringComparison.OrdinalIgnoreCase) ? "False" : "True";
        await driver.Tap(button);
        await driver.WaitForProperty(view, property, expected);
        return expected == "True";
    }
}

[SkipOn(UIPlatform.Linux, "page not in the Linux head")]
[SkipOn(UIPlatform.MacOS, KnownGaps.SkiaNoAppKitHandler)]
public class SpreadsheetTests : UITest
{
    const string View = "SpreadsheetView";

    [Test]
    public async Task Zoom_Is_Clamped_To_10_And_400_Percent()
    {
        await Open("spreadsheet", "SpreadsheetPage");

        await Driver.SetProperty("Zoom", "10", automationId: View);
        await Driver.WaitUntil(async () => await Driver.GetDouble("Zoom", View) is { } z && Math.Abs(z - 4) < 0.001, "Zoom clamped to 4");

        await Driver.SetProperty("Zoom", "0.01", automationId: View);
        await Driver.WaitUntil(async () => await Driver.GetDouble("Zoom", View) is { } z && Math.Abs(z - 0.1) < 0.001, "Zoom clamped to 0.1");

        await Driver.SetProperty("Zoom", "1.5", automationId: View);
        await Driver.WaitUntil(async () => await Driver.GetDouble("Zoom", View) is { } z && Math.Abs(z - 1.5) < 0.001, "Zoom 1.5 kept");

        await Driver.SetProperty("Zoom", "1", automationId: View);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Shell_Button_Toggles_ShowShell()
    {
        await Open("spreadsheet", "SpreadsheetPage");
        await OfficeUi.Toggle(Driver, "SpreadsheetShellToggle", View, "ShowShell");
        var on = await OfficeUi.Toggle(Driver, "SpreadsheetShellToggle", View, "ShowShell");
        if (!on)
            await OfficeUi.Toggle(Driver, "SpreadsheetShellToggle", View, "ShowShell");

        await AssertNoAppErrors();
    }

    [Test]
    public async Task SheetName_Switches_The_Visible_Sheet()
    {
        await Open("spreadsheet", "SpreadsheetPage");

        await Driver.SetProperty("SheetName", "Summary", automationId: View);
        await Driver.WaitForProperty(View, "SheetName", "Summary");

        await Driver.SetProperty("SheetName", "Budget", automationId: View);
        await Driver.WaitForProperty(View, "SheetName", "Budget");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Add_Row_And_Theme_Run_Without_Errors()
    {
        await Open("spreadsheet", "SpreadsheetPage");
        await Driver.Tap("SpreadsheetAddRow");
        await Driver.Tap("SpreadsheetAddRow");
        await Driver.Tap("SpreadsheetTheme");
        await Driver.Tap("SpreadsheetTheme");

        // The file line only changes on a Save/Export — adding rows must not touch it.
        await Assert.That(await Driver.GetProperty("Text", automationId: "SpreadsheetLastFile")).StartsWith("File ▸ Save");
        await AssertNoAppErrors();
    }
}

[SkipOn(UIPlatform.Linux, "page not in the Linux head")]
[SkipOn(UIPlatform.MacOS, KnownGaps.SkiaNoAppKitHandler)]
public class DocumentViewerTests : UITest
{
    const string View = "DocViewerView";
    const string Label = "DocViewerZoomLabel";

    async Task ZoomOutToMinimum()
    {
        for (var i = 0; i < 16; i++)
            await Driver.Tap("DocViewerZoomOut");
        await Driver.WaitForProperty(Label, "Text", "50 %");
    }

    [Test]
    public async Task Zoom_Out_Clamps_At_50_Percent()
    {
        await Open("documentviewer", "DocumentViewerPage");
        await ZoomOutToMinimum();
        await Driver.Tap("DocViewerZoomOut");
        await Driver.WaitForProperty(Label, "Text", "50 %");
        await Assert.That(Math.Abs((await Driver.GetDouble("Zoom", View) ?? -1) - 0.5)).IsLessThan(0.001);
    }

    [Test]
    public async Task Zoom_In_Steps_Ten_Percent_And_Reaches_The_View()
    {
        await Open("documentviewer", "DocumentViewerPage");
        await ZoomOutToMinimum();

        await Driver.Tap("DocViewerZoomIn");
        await Driver.WaitForProperty(Label, "Text", "60 %");
        await Assert.That(Math.Abs((await Driver.GetDouble("Zoom", View) ?? -1) - 0.6)).IsLessThan(0.001);

        await Driver.Tap("DocViewerZoomIn");
        await Driver.WaitForProperty(Label, "Text", "70 %");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Zoom_In_Clamps_At_200_Percent()
    {
        await Open("documentviewer", "DocumentViewerPage");
        for (var i = 0; i < 16; i++)
            await Driver.Tap("DocViewerZoomIn");
        await Driver.WaitForProperty(Label, "Text", "200 %");
        await Assert.That(Math.Abs((await Driver.GetDouble("Zoom", View) ?? -1) - 2.0)).IsLessThan(0.001);
        await ZoomOutToMinimum();
    }

    [Test]
    public async Task Document_Loads_And_Reports_What_It_Did_Not_Render()
    {
        await Open("documentviewer", "DocumentViewerPage");

        // Set only after WordDocument.OpenAsync finished and the document reached the view.
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: "DocViewerNotRendered")) is { Length: > 0 },
            "the not-rendered report to be filled in",
            timeoutSeconds: 20
        );
        var text = await Driver.GetProperty("Text", automationId: "DocViewerNotRendered");
        await Assert.That(text!.StartsWith("Nothing in this document") || text.StartsWith("Preserved but not shown")).IsTrue();
        await Assert.That(await Driver.GetProperty("Document", automationId: View)).IsNotNull();
    }

    [Test]
    public async Task Theme_And_Top_Run_Without_Errors()
    {
        await Open("documentviewer", "DocumentViewerPage");
        await Driver.Tap("DocViewerTheme");
        await Driver.Tap("DocViewerTop");
        await Driver.Tap("DocViewerTheme");
        await AssertNoAppErrors();
    }
}

[SkipOn(UIPlatform.Linux, "page not in the Linux head")]
[SkipOn(UIPlatform.MacOS, KnownGaps.SkiaNoAppKitHandler)]
public class SlideViewerTests : UITest
{
    const string View = "SlideViewerView";
    const string Counter = "SlideViewerCounter";

    // Previous on the first slide is a no-op, so two taps always land on slide 1 of the 3-slide deck.
    async Task OpenOnFirstSlide()
    {
        await Open("slideviewer", "SlideViewerPage");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: Counter)) is { Length: > 0 },
            "the deck to load",
            timeoutSeconds: 20
        );
        await Driver.Tap("SlideViewerPrevious");
        await Driver.Tap("SlideViewerPrevious");
        await Driver.WaitForProperty(Counter, "Text", "1 / 3");
    }

    [Test]
    public async Task Next_And_Previous_Move_Counter_Index_And_Notes()
    {
        await OpenOnFirstSlide();
        await Assert.That(await Driver.GetProperty("Text", automationId: "SlideViewerNotes")).Contains("embedding PowerPoint");

        await Driver.Tap("SlideViewerNext");
        await Driver.WaitForProperty(Counter, "Text", "2 / 3");
        await Driver.WaitForProperty(View, "SlideIndex", "1");
        await Assert.That(await Driver.GetProperty("Text", automationId: "SlideViewerNotes")).Contains("Inheritance");

        await Driver.Tap("SlideViewerPrevious");
        await Driver.WaitForProperty(Counter, "Text", "1 / 3");
        await Driver.WaitForProperty(View, "SlideIndex", "0");
    }

    [Test]
    public async Task Next_Stops_At_The_Last_Slide_Which_Has_No_Notes()
    {
        await OpenOnFirstSlide();
        await Driver.Tap("SlideViewerNext");
        await Driver.Tap("SlideViewerNext");
        await Driver.WaitForProperty(Counter, "Text", "3 / 3");

        await Driver.Tap("SlideViewerNext");
        await Driver.WaitForProperty(Counter, "Text", "3 / 3");
        await Driver.WaitForProperty(View, "SlideIndex", "2");
        await Assert.That(await Driver.GetProperty("Text", automationId: "SlideViewerNotes") ?? "").IsEmpty();
    }

    [Test]
    public async Task Mode_Button_Switches_Between_Single_And_Grid()
    {
        await OpenOnFirstSlide();
        var before = await Driver.GetProperty("Mode", automationId: View);
        var expected = before == "Grid" ? "Single" : "Grid";

        await Driver.Tap("SlideViewerMode");
        await Driver.WaitForProperty(View, "Mode", expected);

        // The button names the mode it will switch to.
        await Driver.WaitForProperty("SlideViewerMode", "Text", expected == "Grid" ? "Single" : "Grid");

        await Driver.Tap("SlideViewerMode");
        await Driver.WaitForProperty(View, "Mode", before!);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Theme_Runs_Without_Errors()
    {
        await OpenOnFirstSlide();
        await Driver.Tap("SlideViewerTheme");
        await Driver.Tap("SlideViewerTheme");
        await AssertNoAppErrors();
    }
}

[SkipOn(UIPlatform.Linux, "page not in the Linux head")]
[SkipOn(UIPlatform.MacOS, KnownGaps.SkiaNoAppKitHandler)]
public class SlideEditorTests : UITest
{
    const string View = "SlideEditorView";
    const string Status = "SlideEditorStatus";

    async Task OpenLoaded()
    {
        await Open("slideeditor", "SlideEditorPage");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: Status)) is { } t && t.StartsWith("Slide "),
            "the deck to load",
            timeoutSeconds: 20
        );
    }

    [Test]
    public async Task Status_Reports_The_Deck()
    {
        await OpenLoaded();
        await Driver.WaitForProperty(View, "SlideCount", "3");
        await Assert.That(await Driver.GetProperty("Text", automationId: Status)).Contains(" of 3 · ");
    }

    [Test]
    public async Task SlideIndex_Moves_The_Current_Slide_And_The_Status()
    {
        await OpenLoaded();

        await Driver.SetProperty("SlideIndex", "2", automationId: View);
        await Driver.WaitForProperty(View, "CurrentSlideIndex", "2");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: Status))?.StartsWith("Slide 3 of 3") == true,
            "status to read Slide 3 of 3"
        );

        await Driver.SetProperty("SlideIndex", "0", automationId: View);
        await Driver.WaitForProperty(View, "CurrentSlideIndex", "0");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: Status))?.StartsWith("Slide 1 of 3") == true,
            "status to read Slide 1 of 3"
        );
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Toolbar_Button_Toggles_ShowToolbar()
    {
        await OpenLoaded();
        var on = await OfficeUi.Toggle(Driver, "SlideEditorToolbarToggle", View, "ShowToolbar");
        if (!on)
            await OfficeUi.Toggle(Driver, "SlideEditorToolbarToggle", View, "ShowToolbar");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Theme_Runs_Without_Errors()
    {
        await OpenLoaded();
        await Driver.Tap("SlideEditorTheme");
        await Driver.Tap("SlideEditorTheme");
        await AssertNoAppErrors();
    }
}

[SkipOn(UIPlatform.Linux, "page not in the Linux head")]
[SkipOn(UIPlatform.MacOS, KnownGaps.SkiaNoAppKitHandler)]
public class DocumentEditorTests : UITest
{
    const string View = "DocEditorView";

    static readonly string[] Margins = ["Normal", "Narrow", "Moderate", "Wide"];

    async Task OpenLoaded()
    {
        await Open("documenteditor", "DocumentEditorPage");

        // The margins button is enabled once the document has been opened into the editor.
        await Driver.WaitForProperty("DocEditorMargins", "IsEnabled", "True", timeoutSeconds: 20);
    }

    [Test]
    public async Task Margins_Button_Cycles_The_Presets()
    {
        await OpenLoaded();
        var before = await Driver.GetProperty("Text", automationId: "DocEditorMargins");
        var index = Array.FindIndex(Margins, m => before == "Margins: " + m);
        await Assert.That(index).IsGreaterThanOrEqualTo(0);

        await Driver.Tap("DocEditorMargins");
        await Driver.WaitForProperty("DocEditorMargins", "Text", "Margins: " + Margins[(index + 1) % Margins.Length]);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Shell_Button_Toggles_ShowShell()
    {
        await OpenLoaded();
        var on = await OfficeUi.Toggle(Driver, "DocEditorShellToggle", View, "ShowShell");
        if (!on)
            await OfficeUi.Toggle(Driver, "DocEditorShellToggle", View, "ShowShell");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Toolbar_Button_Toggles_ShowToolbar()
    {
        await OpenLoaded();
        var on = await OfficeUi.Toggle(Driver, "DocEditorToolbarToggle", View, "ShowToolbar");
        if (!on)
            await OfficeUi.Toggle(Driver, "DocEditorToolbarToggle", View, "ShowToolbar");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Spelling_Button_Toggles_The_Checker()
    {
        await OpenLoaded();
        if (await Driver.GetProperty("IsEnabled", automationId: "DocEditorSpelling") != "True")
            Skip.Test("No platform spell checker on this head");

        var on = await OfficeUi.Toggle(Driver, "DocEditorSpelling", View, "IsSpellCheckEnabled");
        await Driver.WaitForProperty("DocEditorSpelling", "Text", on ? "Spelling: on" : "Spelling: off");

        var back = await OfficeUi.Toggle(Driver, "DocEditorSpelling", View, "IsSpellCheckEnabled");
        await Driver.WaitForProperty("DocEditorSpelling", "Text", back ? "Spelling: on" : "Spelling: off");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Theme_Runs_Without_Errors()
    {
        await OpenLoaded();
        await Driver.Tap("DocEditorTheme");
        await Driver.Tap("DocEditorTheme");
        await AssertNoAppErrors();
    }
}

[SkipOn(UIPlatform.Linux, "page not in the Linux head")]
[SkipOn(UIPlatform.MacOS, KnownGaps.SkiaNoAppKitHandler)]
public class NotebookTests : UITest
{
    const string View = "NotebookEditorView";
    const string Status = "NotebookStatus";

    async Task OpenLoaded()
    {
        await Open("notebook", "NotebookEditorPage");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: Status))?.Contains("sections") == true
                || (await Driver.GetProperty("Text", automationId: Status))?.StartsWith("saved") == true,
            "the notebook to load",
            timeoutSeconds: 20
        );
    }

    [Test]
    public async Task Status_Counts_Sections_And_Pages()
    {
        await OpenLoaded();
        await Assert.That(await Driver.GetProperty("Notebook", automationId: View)).IsNotNull();

        // A Save from another test replaces the line; a fresh visit to the page does not recount, so
        // only assert the counts when the status line is the counter.
        var text = await Driver.GetProperty("Text", automationId: Status);
        if (text!.Contains("sections"))
            await Assert.That(text).StartsWith("2 sections · 3 pages · ");
    }

    [Test]
    public async Task Save_Writes_A_Shinynote_Package()
    {
        await OpenLoaded();
        await Driver.Tap("NotebookSave");
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: Status))?.StartsWith("saved to ") == true,
            "status to report the save"
        );
        await Assert.That(await Driver.GetProperty("Text", automationId: Status)).EndsWith("sample.shinynote");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Toolbar_Button_Toggles_ShowToolbar()
    {
        await OpenLoaded();
        var on = await OfficeUi.Toggle(Driver, "NotebookToolbarToggle", View, "ShowToolbar");
        if (!on)
            await OfficeUi.Toggle(Driver, "NotebookToolbarToggle", View, "ShowToolbar");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Navigation_Button_Toggles_ShowNavigation()
    {
        await OpenLoaded();
        var on = await OfficeUi.Toggle(Driver, "NotebookNavigationToggle", View, "ShowNavigation");
        if (!on)
            await OfficeUi.Toggle(Driver, "NotebookNavigationToggle", View, "ShowNavigation");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Pages_Button_Toggles_ShowPageList()
    {
        await OpenLoaded();

        // Starts hidden on a phone and shown elsewhere; the button flips whichever it is.
        var before = await Driver.GetProperty("ShowPageList", automationId: View);
        await OfficeUi.Toggle(Driver, "NotebookPagesToggle", View, "ShowPageList");
        await OfficeUi.Toggle(Driver, "NotebookPagesToggle", View, "ShowPageList");
        await Driver.WaitForProperty(View, "ShowPageList", before!);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Theme_Runs_Without_Errors()
    {
        await OpenLoaded();
        await Driver.Tap("NotebookTheme");
        await Driver.Tap("NotebookTheme");
        await AssertNoAppErrors();
    }
}
