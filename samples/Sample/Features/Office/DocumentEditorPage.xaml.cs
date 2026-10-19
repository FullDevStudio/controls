using Shiny.Controls.Office.Document;
using Shiny.Controls.Office.Packaging;
using Shiny.Controls.Office.Skia;
using Shiny.Controls.Office.Shell;
using Shiny.Controls.Office.Spelling;
using Shiny.Maui.Controls.Office;

namespace Sample.Features.Office;

/// <summary>
/// The .docx editor, with the platform's own spell checker.
/// </summary>
/// <remarks>
/// <para>
/// The toolbar carries the insert gallery — shapes, a table and a picture — plus the highlight split
/// button. Everything it inserts is inline, so it flows with the text; select one and drag a handle to
/// resize it. Dragging an image file onto the canvas from the desktop does the same thing as the
/// picture button, on the platforms that have a file drag.
/// </para>
/// <para>
/// Nothing here registers a checker: <c>Shiny.Maui.Controls.Office</c> installs the platform one —
/// UITextChecker on iOS, NSSpellChecker on macOS, Android's text services, the Windows COM checker —
/// as soon as the package is touched. Right-click (or long-press) a red-underlined word for
/// corrections; the same menu offers Ignore and Add to dictionary, and the last of those writes to
/// the user's real dictionary, shared with every other app on the device.
/// </para>
/// </remarks>
public partial class DocumentEditorPage : ContentPage
{
    bool opened;
    bool dark;
    int marginPreset;

    public DocumentEditorPage()
    {
        this.InitializeComponent();
        SampleSourceCode.Attach(this);
        this.UpdateStatus();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (this.opened)
            return;

        // OpenAsync hands the document to the view, which owns it from then on (and disposes it when a
        // file opened from the backstage replaces it) - and
        // everything the File backstage does after that is MauiProgram's UseOfficeDocumentStore():
        // Browse is the file picker, Save writes to the app's Documents folder and back over the same
        // file next time, Save As / Export write new files, and the recent list fills itself.
        this.opened = true;
        await this.Editor.OpenAsync(new MemoryStream(SampleOfficeDocuments.BuildDocument()), "Welcome.docx");
        this.Editor.DocumentChanged += this.OnDocumentChanged;
        this.Editor.DropRejected += this.OnDropRejected;

        this.UpdateStatus();
    }

    void OnToggleShell(object? sender, EventArgs e) => this.Editor.ShowShell = !this.Editor.ShowShell;

    /// <summary>
    /// A dropped file the editor would not take.
    /// </summary>
    /// <remarks>
    /// Worth wiring in a sample because the alternative is what it looks like when it is not wired: a
    /// drop that appears to have worked and did nothing.
    /// </remarks>
    void OnDropRejected(object? sender, OfficeDropRejected e)
        => this.StatusLabel.Text = e.FileName.Length > 0
            ? $"{e.FileName}: {e.Reason}"
            : e.Reason;

    void OnToggleToolbar(object? sender, EventArgs e) => this.Editor.ShowToolbar = !this.Editor.ShowToolbar;

    /// <summary>
    /// Off is an app that owns its files: no File button or backstage, no Save or AutoSave in the title
    /// bar. Such an app calls Editor.SaveAsync() / OpenAsync(stream, name) from its own UI instead.
    /// </summary>
    void OnToggleFileCommands(object? sender, EventArgs e) => this.Editor.ShowFileCommands = !this.Editor.ShowFileCommands;

    void OnToggleTheme(object? sender, EventArgs e)
    {
        this.dark = !this.dark;
        // null, not DocumentTheme.Light: unset means "follow the app appearance", which is the
        // behaviour worth demoing. Passing Light would pin it and hide that.
        this.Editor.Theme = this.dark ? DocumentTheme.Dark : null;
    }

    void OnToggleSpelling(object? sender, EventArgs e)
    {
        this.Editor.IsSpellCheckEnabled = !this.Editor.IsSpellCheckEnabled;
        this.UpdateStatus();
    }

    /// <summary>
    /// Steps through the margin presets.
    /// </summary>
    /// <remarks>
    /// The toolbar already carries this gallery as an action sheet; the button is here to show the
    /// controller API a host with its own chrome would call, and it is the only route to it when the
    /// toolbar is hidden. The presets come from <c>PageMarginPresets</c> rather than being listed
    /// again, which is the same list the Blazor sample and both toolbars offer.
    /// </remarks>
    void OnCycleMargins(object? sender, EventArgs e)
    {
        if (this.Editor.Controller is not { } controller)
            return;

        this.marginPreset = (this.marginPreset + 1) % PageMarginPresets.All.Count;
        controller.SetPageMargins(PageMarginPresets.All[this.marginPreset].Margins);

        this.UpdateStatus();
    }

    void OnDocumentChanged(object? sender, EventArgs e) => this.UpdateStatus();

    void UpdateStatus()
    {
        var available = SpellCheckers.Default.IsAvailable;

        this.SpellButton.Text = this.Editor.IsSpellCheckEnabled ? "Spelling: on" : "Spelling: off";
        this.SpellButton.IsEnabled = available;

        this.MarginButton.Text = $"Margins: {PageMarginPresets.All[this.marginPreset].Name}";
        this.MarginButton.IsEnabled = this.Editor.Document is not null;

        this.StatusLabel.Text = available
            ? $"Spell checker: {SpellCheckers.Default.GetType().Name} ({SpellCheckers.Default.DefaultLanguage}). Right-click or long-press an underlined word."
            : "No platform spell checker on this target — set DocumentEditorView.SpellChecker to supply one.";
    }

    // No OnHandlerChanged teardown, deliberately. Shell drops this page's handler whenever another
    // flyout item is picked, but keeps the page and gives it a new handler on the way back. Disposing
    // the document there left the editor holding a dead package: the return trip's layout pass read it
    // and threw out of LayoutSubviews, and on iOS 26+ that unwinds through UIKit's observation tracking
    // and leaves it pointing at a dead stack frame - the app then spins and crashes later in
    // setLeftBarButtonItem, nowhere near the cause. The view owns the document it opened, and the page
    // lives as long as the Shell, so the document does too.
}
