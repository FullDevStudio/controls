using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Shiny.Blazor.Controls;
using Shiny.Blazor.Controls.Infrastructure;
using Shiny.Controls.Office.Icons;
using Shiny.Controls.Office.Presentation;
using Shiny.Controls.Office.Shell;
using Shiny.Controls.Office.Skia;
using Shiny.Controls.Office.Theming;

namespace Shiny.Blazor.Controls.Office;

/// <summary>
/// The PowerPoint window around the editor: the title bar, the ribbon's Editing/Viewing mode and Share,
/// the status bar (slide counter, language, Notes, the three views, zoom and fit) and the File
/// backstage — the <see cref="OfficeShell"/> parts wired to the controller through <see cref="SlideShell"/>.
/// </summary>
/// <remarks>
/// <para>
/// On by default (<see cref="ShowShell"/>); every part has its own switch. With the shell off the view is
/// what it was before: the ribbon (File only when <see cref="FileMenuRequested"/> is handled), the
/// slide and the plain status line.
/// </para>
/// <para>
/// Open, Save, Save As, Export (the .pptx, a PDF, a PNG of the slide or a .zip of every slide) and the
/// recent list go to the <see cref="DocumentStore"/> — the browser's own by default — and Print opens a
/// PDF of the deck in the browser's print dialog, so the window works end to end with no host code.
/// </para>
/// </remarks>
public partial class SlideEditorView : IDisposable
{
    [Inject] IJSRuntime Js { get; set; } = default!;
    [Inject] IServiceProvider Services { get; set; } = default!;

    /// <summary>The DI clock (or the system one). Resolved once, in <see cref="OnInitialized"/>.</summary>
    TimeProvider time = TimeProvider.System;

    protected override void OnInitialized() => this.time = ShinyTime.From(this.Services);

    // ---- switches ----

    /// <summary>
    /// Dress the editor in PowerPoint's window — title bar, File backstage, Editing/Viewing and Share on
    /// the ribbon, and the Office status bar. On by default; off gives the ribbon over the slide of before.
    /// </summary>
    [Parameter] public bool ShowShell { get; set; } = true;

    /// <summary>The red title bar: AutoSave, Save / Undo / Redo / From Beginning, the name, command search and account.</summary>
    [Parameter] public bool ShowTitleBar { get; set; } = true;

    /// <summary>The Office status bar: "Slide 3 of 12", language, Notes, the views, zoom and fit. <see cref="ShowStatus"/> off hides it too.</summary>
    [Parameter] public bool ShowStatusBar { get; set; } = true;

    /// <summary>Whether File opens the backstage. Off, File only raises <see cref="FileMenuRequested"/>.</summary>
    [Parameter] public bool ShowBackstage { get; set; } = true;

    /// <summary>
    /// The File surface: the ribbon's File button and backstage, the title bar's Save and AutoSave,
    /// Ctrl+S / Ctrl+P, and Save / Export / Print / New in the command search. Off when the app owns
    /// opening and saving — it calls <c>SaveAsync</c>, <c>OpenAsync(stream, fileName)</c> and the rest
    /// itself, or reads the document directly. File still raises <c>FileMenuRequested</c> when that is
    /// handled. The rest of the shell stays.
    /// </summary>
    [Parameter] public bool ShowFileCommands { get; set; } = true;


    /// <summary>Editing / Viewing mode and Share at the end of the ribbon's tab strip.</summary>
    [Parameter] public bool ShowRibbonActions { get; set; } = true;

    // ---- document ----

    /// <summary>The name in the title bar and backstage. Defaults to the file's name, or "Presentation1". Two-way — the title bar renames.</summary>
    [Parameter] public string? DocumentName { get; set; }

    [Parameter] public EventCallback<string?> DocumentNameChanged { get; set; }

    /// <summary>Where the deck lives, shown under the name's dropdown. Defaults to the deck's path.</summary>
    [Parameter] public string? DocumentLocation { get; set; }

    /// <summary>
    /// Overrides the title bar's save status. Left null the view tracks it: nothing for an untouched deck,
    /// "Unsaved changes" after an edit, "Saving…", then "Saved" / "Saved locally".
    /// </summary>
    [Parameter] public OfficeSaveState? SaveState { get; set; }

    /// <summary>
    /// The title bar's AutoSave switch. On, edits to a <see cref="DocumentStore"/> file are saved two
    /// seconds after the last one. A new deck waits for its first Save, which asks where it goes.
    /// </summary>
    [Parameter] public bool AutoSave { get; set; }

    [Parameter] public EventCallback<bool> AutoSaveChanged { get; set; }

    /// <summary>The account shown at the title bar's right end, and the author on the backstage's Info page.</summary>
    [Parameter] public string? UserName { get; set; }

    /// <summary>The backstage's New page. Null offers <see cref="SlideTemplates.All"/> — Blank, Project update, Pitch deck, Lesson.</summary>
    [Parameter] public IReadOnlyList<OfficeTemplate>? Templates { get; set; }

    /// <summary>
    /// Where Open, Save, Save As, Export and the recent list go. Null uses the registered
    /// <see cref="IOfficeDocumentStore"/> (<c>AddShinyOfficeDocumentStore()</c>), or a
    /// <see cref="BrowserOfficeDocumentStore"/> of the view's own when there is none.
    /// </summary>
    [Parameter] public IOfficeDocumentStore? DocumentStore { get; set; }

    /// <summary>The store file the deck came from or was last saved as. Null for a new deck, or one the host set.</summary>
    public OfficeDocumentRef? CurrentFile => this.session.Current;

    /// <summary>Editing or Viewing (read-only) — the ribbon's mode dropdown. Reviewing edits as Editing does. Two-way.</summary>
    [Parameter] public OfficeEditMode EditMode { get; set; }

    [Parameter] public EventCallback<OfficeEditMode> EditModeChanged { get; set; }

    // ---- events ----

    /// <summary>
    /// A template was picked. Unhandled, the view opens it itself (<see cref="SlideTemplates.OpenAsync"/>),
    /// shows it and raises <see cref="DeckReplaced"/>.
    /// </summary>
    [Parameter] public EventCallback<OfficeTemplate> TemplateSelected { get; set; }

    /// <summary>The view swapped in a deck of its own — a template from the backstage. The host owns it from then on.</summary>
    [Parameter] public EventCallback<SlideDeck> DeckReplaced { get; set; }

    /// <summary>The ribbon's Share button.</summary>
    [Parameter] public EventCallback ShareRequested { get; set; }

    /// <summary>The title bar's command search. Filled from the ribbon as its tabs render, plus PowerPoint's own commands up front.</summary>
    public OfficeCommandIndex Commands => this.commands;

    /// <summary>Whether the backstage is showing.</summary>
    public bool IsBackstageOpen => this.backstageOpen;

    // ---- state ----

    static readonly OfficeZoomModel StatusZoomModel = new(SlideController.MinimumZoom, SlideController.MaximumZoom, 1.0);

    readonly OfficeCommandIndex commands = new();
    readonly OfficeStatusItem slideItem = new("slide", "Slide 1 of 1") { Tooltip = "The slide being edited" };
    readonly OfficeStatusItem languageItem = new("language", WordShell.LanguageText()) { Tooltip = "Proofing language" };
    readonly OfficeStatusItem notesItem = new("notes", "Notes") { IsClickable = true, Tooltip = "Show the speaker notes under the slide" };
    readonly List<OfficeStatusItem> statusItems;
    readonly List<OfficeQuickAccessItem> quickAccess;
    readonly List<OfficeCommand> curated = [];

    Ribbon? ribbonRef;
    Ribbon? syncedRibbon;
    IDisposable? ribbonSync;
    RibbonDisplayMode ribbonMode = RibbonDisplayMode.Expanded;

    bool backstageOpen;
    OfficeBackstagePage backstagePage = OfficeBackstagePage.Home;
    OfficeDocumentInfo? backstageInfo;
    string? printPreview;
    string? nameOverride;
    bool? autoSaveOverride;
    OfficeEditMode? editModeOverride;

    SlideDeck? ownedDeck;
    readonly OfficeDocumentSession session = new(OfficeApp.PowerPoint);
    (SlideDeck Deck, OfficeDocumentRef File)? attaching;
    IReadOnlyList<OfficeRecentFile>? storeRecent;
    SlideDeck? lastDeckParameter;
    SlideDeck? trackedDeck;
    long revision;
    long savedRevision;
    OfficeSaveState lastSaved = OfficeSaveState.None;
    OfficeSaveState? transientSaveState;
    CancellationTokenSource? autoSaveTimer;
    IJSObjectReference? shellModule;

    /// <summary>The built-in templates with a picture of each one's first slide, drawn once per app.</summary>
    static IReadOnlyList<OfficeTemplate>? builtInTemplates;

    public SlideEditorView()
    {
        this.statusItems = [this.slideItem, this.languageItem, this.notesItem];
        this.quickAccess =
        [
            new OfficeQuickAccessItem("slides:from-beginning", "Start From Beginning", OfficeShellIcon.SlideShow, () => this.InvokeAsync(() => this.StartPresentingAsync(0)))
            {
                Shortcut = "F5"
            }
        ];

        this.AddShellCommands();
    }

    // ---- derived ----

    bool ShellOn => this.ShowShell;

    OfficeEditMode EditModeValue => this.editModeOverride ?? this.EditMode;

    /// <summary>Viewing mode, from the ribbon's dropdown, makes the deck read-only.</summary>
    bool EffectiveReadOnly => this.ReadOnly || (this.ShellOn && this.EditModeValue == OfficeEditMode.Viewing);

    string EffectiveDocumentName
        => this.nameOverride
           ?? this.DocumentName
           ?? (this.Deck?.Path is { } path ? System.IO.Path.GetFileNameWithoutExtension(path) : null)
           ?? OfficeAppInfo.PowerPoint.DefaultDocumentName;

    OfficeSaveState EffectiveSaveState
    {
        get
        {
            if (this.SaveState is { } pinned)
                return pinned;

            if (this.transientSaveState is { } transient)
                return transient;

            if (this.Deck is null)
                return OfficeSaveState.None;

            return this.revision != this.savedRevision ? OfficeSaveState.Unsaved : this.lastSaved;
        }
    }

    bool EffectiveAutoSave => this.autoSaveOverride ?? this.AutoSave;

    /// <summary>A custom accent overrides the shell's PowerPoint red; the default leaves it alone.</summary>
    string? ShellAccent => this.Accent is { } a && a != OfficeAccent.Presentation ? Css(a.Color) : null;

    // ---- the deck the view shows ----

    /// <summary>
    /// A deck the view opened itself (a template) stays on screen until the host hands in a different
    /// one: a re-render that passes the old parameter back must not undo the swap.
    /// </summary>
    void ApplyDeckParameter()
    {
        if (!ReferenceEquals(this.Deck, this.lastDeckParameter))
        {
            this.lastDeckParameter = this.Deck;

            if (this.ownedDeck is { } mine && !ReferenceEquals(mine, this.Deck))
            {
                this.ownedDeck = null;
                if (!this.DeckReplaced.HasDelegate)
                    mine.Dispose();
            }
        }
        else if (this.ownedDeck is { } mine)
        {
            this.Deck = mine;
        }

        this.TrackDeck(this.Deck);
    }

    void TrackDeck(SlideDeck? deck)
    {
        if (ReferenceEquals(deck, this.trackedDeck))
            return;

        if (this.trackedDeck is { } old)
            old.ContentChanged -= this.OnDeckContentChanged;

        this.trackedDeck = deck;
        this.revision = 0;
        this.savedRevision = 0;
        this.lastSaved = OfficeSaveState.None;
        this.transientSaveState = null;

        // A deck the host set is not a store file; one this view opened is.
        this.session.Current = this.attaching is { } a && ReferenceEquals(a.Deck, deck) ? a.File : null;
        this.attaching = null;

        if (deck is not null)
            deck.ContentChanged += this.OnDeckContentChanged;
    }

    void OnDeckContentChanged(object? sender, EventArgs e)
    {
        this.revision++;
        this.ScheduleAutoSave();
    }

    // ---- lifecycle hooks the main file calls ----

    void AfterShellRender()
    {
        if (!this.ShellOn)
            return;

        // The Blazor ribbon only indexes the tabs it has rendered, so the search follows whichever
        // ribbon instance is on screen and the curated list steps aside as tabs are visited.
        if (!ReferenceEquals(this.ribbonRef, this.syncedRibbon))
        {
            this.ribbonSync?.Dispose();
            this.ribbonSync = this.ribbonRef is { } ribbon ? this.commands.SyncRibbon(ribbon) : null;
            this.syncedRibbon = this.ribbonRef;
        }

        this.PruneCurated();
        this.UpdateShellStatus();
    }

    void UpdateShellStatus()
    {
        this.slideItem.Text = SlideShell.SlideText(this.CurrentSlideIndex, this.SlideCount, this.ViewMode);
        this.notesItem.Tooltip = this.ShowNotes ? "Hide the speaker notes" : "Show the speaker notes under the slide";
        this.notesItem.IsClickable = this.Deck is not null;
    }

    public void Dispose()
    {
        this.ribbonSync?.Dispose();
        this.ribbonSync = null;
        this.autoSaveTimer?.Cancel();

        if (this.trackedDeck is { } deck)
            deck.ContentChanged -= this.OnDeckContentChanged;

        // A host handed the deck through DeckReplaced may outlive this view with it.
        if (!this.DeckReplaced.HasDelegate)
            this.ownedDeck?.Dispose();

        _ = this.ownStore?.DisposeAsync();

        if (this.shellModule is { } module)
            _ = DisposeModuleAsync(module);

        static async Task DisposeModuleAsync(IJSObjectReference module)
        {
            try { await module.DisposeAsync(); }
            catch (JSDisconnectedException) { }
            catch (ObjectDisposedException) { }
        }
    }

    // ---- title bar, ribbon actions ----

    async Task OnDocumentNameChangedAsync(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name == this.EffectiveDocumentName)
            return;

        this.nameOverride = name.Trim();
        await this.DocumentNameChanged.InvokeAsync(this.nameOverride);
    }

    async Task OnAutoSaveChangedAsync(bool value)
    {
        this.autoSaveOverride = value;
        await this.AutoSaveChanged.InvokeAsync(value);
        this.ScheduleAutoSave();
    }

    async Task SetEditModeAsync(OfficeEditMode mode)
    {
        this.editModeOverride = mode;

        // Viewing: nothing selected, nothing half-typed - the deck is only looked at.
        if (mode == OfficeEditMode.Viewing)
            this.Controller?.ClearSelection();

        this.editor?.Invalidate();
        await this.EditModeChanged.InvokeAsync(mode);
        this.StateHasChanged();
    }

    Task OnShareAsync() => this.ShareRequested.InvokeAsync();

    /// <summary>A title bar search that matched no command finds the text across the slides.</summary>
    async Task OnCommandSearchSubmittedAsync(string query)
    {
        if (this.Controller is not { } controller)
            return;

        if (!SlideShell.Search(controller, query))
        {
            // Nothing found: the Home tab's Find box shows the query and its "No results".
            this.selectedTab = "home";
            this.StateHasChanged();
            return;
        }

        // The hit is selected on its slide; the keyboard goes with it, as PowerPoint's search does.
        if (this.editor is { } surface)
        {
            surface.Invalidate();
            await surface.FocusAsync();
        }

        await this.OnEditorChanged();
    }

    // ---- status bar ----

    async Task OnStatusItemClickedAsync(OfficeStatusItem item)
    {
        if (item == this.notesItem)
            await this.ToggleNotesAsync();

        this.StateHasChanged();
    }

    async Task OnViewModeSelectedAsync(string? id)
    {
        if (SlideShell.ParseViewMode(id) is { } mode)
        {
            await this.SetViewModeAsync(mode);
            return;
        }

        // Reading View: the deck plays from the slide being edited.
        await this.StartPresentingAsync();
    }

    Task OnShellLayoutChanged(OfficeShellLayout layout)
    {
        var mode = layout.SimplifiedRibbon ? RibbonDisplayMode.Simplified : RibbonDisplayMode.Expanded;

        // A collapsed ribbon stays collapsed; only the dense/full choice follows the width.
        if (this.ribbonMode != RibbonDisplayMode.Collapsed)
            this.ribbonMode = mode;

        return Task.CompletedTask;
    }

    // ---- backstage ----

    /// <summary>Opens the File backstage, refreshing its Info page.</summary>
    public Task OpenBackstageAsync(OfficeBackstagePage? page = null)
    {
        this.backstageInfo = this.Deck is { } deck ? SlideShell.DocumentInfo(deck, this.EffectiveDocumentName, this.UserName) : null;
        this.EnsureTemplateThumbnails();

        if (page is { } p)
            this.backstagePage = p;

        this.backstageOpen = true;
        this.StateHasChanged();
        return this.LoadRecentFilesAsync();
    }

    async Task OnFileClicked()
    {
        if (this.ShellOn && this.ShowBackstage && this.ShowFileCommands)
            await this.OpenBackstageAsync();

        if (this.FileMenuRequested.HasDelegate)
            await this.FileMenuRequested.InvokeAsync();
    }

    Task OnBackstageOpenChanged(bool open)
    {
        this.backstageOpen = open;

        if (open)
            return this.LoadRecentFilesAsync();

        if (this.editor is { } surface)
            return surface.FocusAsync();

        return Task.CompletedTask;
    }

    async Task OnBackstagePageChangedAsync(OfficeBackstagePage page)
    {
        this.backstagePage = page;

        if (page == OfficeBackstagePage.Info && this.Deck is { } deck)
            this.backstageInfo = SlideShell.DocumentInfo(deck, this.EffectiveDocumentName, this.UserName);

        if (page == OfficeBackstagePage.Print && this.Deck is { Slides.Count: > 0 } printed)
        {
            // Rendered once per visit: the slide being edited, the way PowerPoint's preview opens on it.
            await Task.Yield();
            try
            {
                var png = SlideExporter.ToPng(printed, Math.Clamp(this.CurrentSlideIndex, 0, printed.Slides.Count - 1), 960, this.Watermark);
                this.printPreview = "data:image/png;base64," + Convert.ToBase64String(png);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[Shiny.Office] Print preview failed: {ex.Message}");
                this.printPreview = null;
            }

            this.StateHasChanged();
        }
    }

    /// <summary>
    /// Gives the built-in templates a picture of their title slide, as PowerPoint's New page has. Drawn
    /// once per app, on the first visit to the backstage — never at start-up.
    /// </summary>
    void EnsureTemplateThumbnails()
    {
        if (this.Templates is not null || builtInTemplates is not null)
            return;

        try
        {
            builtInTemplates = OfficeTemplateThumbnails.Slides();
        }
        catch (Exception ex)
        {
            // A picture is a nicety; the named tiles still work without one.
            Console.Error.WriteLine($"[Shiny.Office] Template thumbnails failed: {ex.Message}");
            builtInTemplates = SlideTemplates.All;
        }
    }

    async Task NewFromTemplateAsync(OfficeTemplate template)
    {
        if (this.TemplateSelected.HasDelegate)
        {
            await this.TemplateSelected.InvokeAsync(template);
            return;
        }

        SlideDeck next;
        try
        {
            next = await SlideTemplates.OpenAsync(template);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Shiny.Office] Could not open template '{template.Id}': {ex.Message}");
            return;
        }

        await this.ReplaceDeckAsync(next, template.IsBlank ? "Presentation" + (++untitled + 1) : template.Name, null);
    }

    /// <summary>Shows a deck this view built or opened, taking ownership of it and disposing the one it replaces.</summary>
    async Task ReplaceDeckAsync(SlideDeck next, string name, OfficeDocumentRef? file)
    {
        var previous = this.ownedDeck;
        this.ownedDeck = next;
        this.attaching = file is null ? null : (next, file);
        this.Deck = next;
        this.SlideIndex = 0;
        this.nameOverride = name;
        this.backstageOpen = false;
        this.TrackDeck(next);
        this.StateHasChanged();

        await this.DeckReplaced.InvokeAsync(next);
        await this.DocumentNameChanged.InvokeAsync(this.nameOverride);
        await this.OnSlideIndexChanged(0);

        // Only a deck this view opened is its to dispose; the host's are the host's.
        if (previous is not null && !ReferenceEquals(previous, next) && !this.DeckReplaced.HasDelegate)
            previous.Dispose();
    }

    static int untitled;

    // ---- open ----

    BrowserOfficeDocumentStore? ownStore;

    IOfficeDocumentStore Store
        => this.DocumentStore
           ?? this.Services.GetService<IOfficeDocumentStore>()
           ?? (this.ownStore ??= new BrowserOfficeDocumentStore(this.Js));

    async Task OpenRecentAsync(OfficeRecentFile file)
    {
        if (file.Document is { } document)
            await this.OpenAsync(document);
    }

    /// <summary>
    /// Opens a file from the <see cref="DocumentStore"/> — <paramref name="file"/>, or the one the user
    /// picks when it is null — and shows it. The view owns the deck it opens.
    /// </summary>
    /// <returns>False when the user cancelled or the file would not open.</returns>
    public async Task<bool> OpenAsync(OfficeDocumentRef? file = null, CancellationToken cancellationToken = default)
    {
        var store = this.Store;
        try
        {
            var opened = await this.session.OpenAsync(store, file, (s, ct) => SlideDeck.OpenAsync(s, editable: true, cancellationToken: ct), cancellationToken);
            if (opened is not { } result)
                return false;

            await this.ReplaceDeckAsync(result.Document, result.File.DisplayName, result.File);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"[Shiny.Office] Could not open '{file?.Name}': {ex.Message}");
            return false;
        }
    }

    /// <summary>Opens a deck from a stream and shows it, named after <paramref name="fileName"/>. The view owns it.</summary>
    public async Task OpenAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var deck = await SlideDeck.OpenAsync(stream, editable: true, cancellationToken: cancellationToken);
        await this.ReplaceDeckAsync(deck, System.IO.Path.GetFileNameWithoutExtension(fileName), null);
    }

    /// <summary>Fills the backstage's recent list from the store.</summary>
    async Task LoadRecentFilesAsync()
    {
        try
        {
            this.storeRecent = await this.Store.GetRecentAsync(OfficeApp.PowerPoint);
            this.StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Shiny.Office] Could not read the recent list: {ex.Message}");
        }
    }

    /// <summary>AutoSave only overwrites: a deck with no file yet would have to ask where it goes.</summary>
    bool CanAutoSave => this.session.Current is not null;

    // ---- save, save as, export, print ----

    /// <summary>Saves the deck as pptx through the <see cref="DocumentStore"/>, back over <see cref="CurrentFile"/> when there is one.</summary>
    public Task SaveAsync() => this.WriteFileAsync(OfficeFileFormats.Pptx, OfficeFileAction.Save);

    /// <summary>Writes the deck as a new file in <paramref name="format"/>; a pptx becomes <see cref="CurrentFile"/>.</summary>
    public Task SaveAsAsync(OfficeFileFormat format) => this.WriteFileAsync(format, OfficeFileAction.SaveAs);

    /// <summary>Writes the deck (or, for a picture, the current slide) as a new file in <paramref name="format"/>.</summary>
    public Task ExportAsync(OfficeFileFormat format) => this.WriteFileAsync(format, OfficeFileAction.Export);

    /// <summary>Print: the browser's print dialog over a PDF of the deck.</summary>
    public Task PrintAsync() => this.WriteFileAsync(OfficeFileFormats.Pdf, OfficeFileAction.Print);

    List<RibbonMenuEntry> ExportPictureMenu =>
    [
        new RibbonMenuEntry { Text = "This slide as PNG", OnClick = EventCallback.Factory.Create(this, () => this.ExportAsync(OfficeFileFormats.Png)) },
        new RibbonMenuEntry { Text = "This slide as JPEG", OnClick = EventCallback.Factory.Create(this, () => this.ExportAsync(OfficeFileFormats.Jpeg)) },
        new RibbonMenuEntry { Text = "All slides as PNG (.zip)", OnClick = EventCallback.Factory.Create(this, () => this.ExportAsync(SlideExport.AllSlidesPng)) }
    ];

    async Task WriteFileAsync(OfficeFileFormat format, OfficeFileAction action)
    {
        if (this.Deck is not { } deck)
            return;

        var slide = this.CurrentSlideIndex;
        var watermark = this.Watermark;
        var saving = action is OfficeFileAction.Save or OfficeFileAction.SaveAs && format.IsNative;
        var at = this.revision;

        if (saving)
        {
            this.transientSaveState = OfficeSaveState.Saving;
            this.StateHasChanged();
        }

        try
        {
            if (action == OfficeFileAction.Print)
            {
                // The browser's print is a dialog over the PDF, not a file anywhere.
                using var buffer = new MemoryStream();
                await SlideExport.WriteAsync(deck, slide, format, buffer, watermark);
                buffer.Position = 0;

                if (await this.ShellModuleAsync() is { } module)
                {
                    using var stream = new DotNetStreamReference(buffer, leaveOpen: true);
                    await module.InvokeVoidAsync("printPdf", stream);
                }

                return;
            }

            var file = await this.session.SaveAsync(this.Store, format, action, this.EffectiveDocumentName,
                (s, ct) => SlideExport.WriteAsync(deck, slide, format, s, watermark, ct));

            if (file is null)
                return;

            if (saving)
            {
                this.savedRevision = at;
                this.lastSaved = OfficeSaveState.Saved;
                this.nameOverride = file.DisplayName;
            }

            this.backstageOpen = false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Shiny.Office] {action} failed: {ex.Message}");
            if (saving)
                this.lastSaved = OfficeSaveState.Error;
        }
        finally
        {
            if (saving)
                this.transientSaveState = null;

            this.StateHasChanged();
        }
    }

    /// <summary>AutoSave: two seconds after the last edit, when the host is there to take the file.</summary>
    void ScheduleAutoSave()
    {
        if (!this.EffectiveAutoSave || !this.CanAutoSave || this.Deck is null || this.revision == this.savedRevision)
            return;

        this.autoSaveTimer?.Cancel();
        var cts = this.autoSaveTimer = new CancellationTokenSource();

        _ = this.InvokeAsync(async () =>
        {
            try
            {
                await Task.Delay(2000, cts.Token);
                await this.SaveAsync();
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    async Task<IJSObjectReference?> ShellModuleAsync()
    {
        try
        {
            return this.shellModule ??= await this.Js.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Shiny.Blazor.Controls.Office/officeShell.js");
        }
        catch (JSException ex)
        {
            Console.Error.WriteLine($"[Shiny.Office] The shell script could not load: {ex.Message}");
            return null;
        }
    }

    // ---- command search ----

    void AddShellCommands()
    {
        this.commands.Add(new OfficeCommand("Save", () => this.InvokeAsync(this.SaveAsync)) { Id = "slides:save", Category = "File", IsAvailable = () => this.ShowFileCommands, Shortcut = "Ctrl+S", Keywords = ["store", "download", "pptx"] });
        this.commands.Add(new OfficeCommand("New Presentation", () => this.InvokeAsync(() => this.OpenBackstageAsync(OfficeBackstagePage.New))) { Id = "slides:new", Category = "File", IsAvailable = () => this.ShowFileCommands, Keywords = ["template", "blank", "deck"] });
        this.commands.Add(new OfficeCommand("Export to PDF", () => this.InvokeAsync(() => this.ExportAsync(OfficeFileFormats.Pdf))) { Id = "slides:pdf", Category = "File › Export", IsAvailable = () => this.ShowFileCommands, Keywords = ["pdf", "download"] });
        this.commands.Add(new OfficeCommand("Export Slide as PNG", () => this.InvokeAsync(() => this.ExportAsync(OfficeFileFormats.Png))) { Id = "slides:png", Category = "File › Export", IsAvailable = () => this.ShowFileCommands, Keywords = ["picture", "image", "png"] });
        this.commands.Add(new OfficeCommand("Export All Slides as PNG", () => this.InvokeAsync(() => this.ExportAsync(SlideExport.AllSlidesPng))) { Id = "slides:png-all", Category = "File › Export", IsAvailable = () => this.ShowFileCommands, Keywords = ["pictures", "images", "zip"] });
        this.commands.Add(new OfficeCommand("Print", () => this.InvokeAsync(this.PrintAsync)) { Id = "slides:print", Category = "File", IsAvailable = () => this.ShowFileCommands, Shortcut = "Ctrl+P" });
        this.commands.Add(new OfficeCommand("Reading View", () => this.InvokeAsync(() => this.StartPresentingAsync())) { Id = "slides:reading", Category = "View › Presentation Views", Keywords = ["read", "present"] });
        this.commands.Add(new OfficeCommand("Fit Slide to Window", () => this.InvokeAsync(() => this.SetZoomAsync(null))) { Id = "slides:fit", Category = "View › Zoom", Keywords = ["zoom", "fit"] });
        this.commands.Add(new OfficeCommand("Zoom 100%", () => this.InvokeAsync(() => this.SetZoomAsync(1.0))) { Id = "slides:zoom100", Category = "View › Zoom", Keywords = ["actual size", "reset zoom"] });
        this.commands.Add(new OfficeCommand("Find", () => this.InvokeAsync(() => this.OnTabChanged("home"))) { Id = "slides:find", Category = "Home › Editing", Shortcut = "Ctrl+F", Keywords = ["search"] });

        // The Blazor ribbon indexes a tab only once it has rendered, so the commands people look for on
        // the other tabs are listed up front. Each steps aside once the ribbon has indexed its own button
        // of the same name (PruneCurated), so a visited tab is not listed twice.
        this.Curate("New Slide", () => this.Run(c => c.NewSlide()), "Home › Slides", "Ctrl+M", ["add slide", "insert slide"]);
        this.Curate("Text Box", this.AddTextBox, "Insert › Text", keywords: ["insert text"]);
        this.Curate("Chart", () => this.Tab("insert"), "Insert › Illustrations", keywords: ["graph", "insert chart"], viewOnly: true);
        this.Curate("Shapes", () => this.Tab("insert"), "Insert › Illustrations", keywords: ["rectangle", "arrow", "insert shape"], viewOnly: true);
        this.Curate("Link", () => { this.OpenLinkDialog(); return Task.CompletedTask; }, "Insert › Links", "Ctrl+K", ["hyperlink", "url"]);
        this.Curate("Header & Footer", () => { this.OpenHeaderFooter(); return Task.CompletedTask; }, "Insert › Text", keywords: ["footer", "slide number", "date"]);
        this.Curate("Themes", () => this.Tab("design"), "Design › Themes", keywords: ["design", "colours", "colors", "theme"], viewOnly: true);
        this.Curate("Format Background", () => { this.OpenBackground(master: false); return Task.CompletedTask; }, "Design › Customize", keywords: ["background"]);
        this.Curate("Slide Size", () => { this.OpenSlideSize(); return Task.CompletedTask; }, "Design › Customize", keywords: ["widescreen", "4:3", "16:9"]);
        this.Curate("Transitions", () => this.Tab("transitions"), "Transitions", keywords: ["fade", "push", "morph"], viewOnly: true);
        this.Curate("Apply To All", () => this.Run(c => c.ApplyTransitionToAll()), "Transitions › Timing", keywords: ["transition all slides"]);
        this.Curate("Animations", () => this.Tab("animations"), "Animations", keywords: ["animate", "effect", "fly in"], viewOnly: true);
        this.Curate("Animation Pane", () => { this.showAnimationPane = !this.showAnimationPane; this.StateHasChanged(); return Task.CompletedTask; }, "Animations › Advanced Animation", viewOnly: true);
        this.Curate("From Beginning", () => this.StartPresentingAsync(0), "Slide Show › Start Slide Show", "F5", ["present", "slideshow", "play"], viewOnly: true);
        this.Curate("From Current Slide", () => this.StartPresentingAsync(), "Slide Show › Start Slide Show", "Shift+F5", ["present", "slideshow", "play"], viewOnly: true);
        this.Curate("Presenter View", () => this.StartPresentingAsync(null, presenterView: true), "Slide Show › Start Slide Show", keywords: ["speaker", "notes", "timer"], viewOnly: true);
        this.Curate("Hide Slide", () => this.Run(c => c.ToggleHideSlide()), "Slide Show › Set Up", keywords: ["skip"]);
        this.Curate("Normal", () => this.SetViewModeAsync(SlideEditorViewMode.Normal), "View › Presentation Views", keywords: ["normal view"], viewOnly: true);
        this.Curate("Outline View", () => this.SetViewModeAsync(SlideEditorViewMode.Outline), "View › Presentation Views", keywords: ["outline"], viewOnly: true);
        this.Curate("Slide Sorter", () => this.SetViewModeAsync(SlideEditorViewMode.SlideSorter), "View › Presentation Views", keywords: ["sorter", "thumbnails", "reorder"], viewOnly: true);
        this.Curate("Notes Page", () => this.SetViewModeAsync(SlideEditorViewMode.NotesPage), "View › Presentation Views", viewOnly: true);
        this.Curate("Slide Master", () => this.SetViewModeAsync(SlideEditorViewMode.SlideMaster), "View › Master Views", keywords: ["master", "layouts"], viewOnly: true);
        this.Curate("Notes", this.ToggleNotesAsync, "View › Show", keywords: ["speaker notes"], viewOnly: true);
        this.Curate("Ruler", () => { this.Toggle(c => c.ShowRuler = !c.ShowRuler); this.StateHasChanged(); return Task.CompletedTask; }, "View › Show", viewOnly: true);
        this.Curate("Gridlines", () => { this.Toggle(c => c.ShowGridlines = !c.ShowGridlines); this.StateHasChanged(); return Task.CompletedTask; }, "View › Show", viewOnly: true);
        this.Curate("Guides", () => { this.Toggle(c => c.ShowGuides = !c.ShowGuides); this.StateHasChanged(); return Task.CompletedTask; }, "View › Show", viewOnly: true);
    }

    Task Tab(string key)
    {
        this.StateHasChanged();
        return this.OnTabChanged(key);
    }

    void Curate(string label, Func<Task> action, string category, string? shortcut = null, string[]? keywords = null, bool viewOnly = false)
    {
        var command = new OfficeCommand(label, () => this.InvokeAsync(async () =>
        {
            await action();
            this.StateHasChanged();
        }))
        {
            Id = "curated:" + label,
            Category = category,
            Shortcut = shortcut,
            Keywords = keywords ?? [],
            CanExecute = () => this.Deck is not null && (viewOnly || !this.Disabled)
        };

        this.curated.Add(command);
        this.commands.Add(command);
    }

    /// <summary>Drops a curated command once the ribbon has indexed a button of the same name.</summary>
    void PruneCurated()
    {
        if (this.curated.Count == 0)
            return;

        var harvested = this.commands.Commands
            .Where(x => x.Id.StartsWith(OfficeRibbonCommands.IdPrefix, StringComparison.Ordinal))
            .Select(x => x.Label)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var command in this.curated.Where(x => harvested.Contains(x.Label)).ToList())
        {
            this.commands.Remove(command.Id);
            this.curated.Remove(command);
        }
    }
}
