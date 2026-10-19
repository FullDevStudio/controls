using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Shiny.Controls.Office.Shell;
using Shiny.Controls.Office.Skia;
using Shiny.Controls.Office.Spreadsheet;
using Shiny.Controls.Office.Spreadsheet.View;

namespace Shiny.Blazor.Controls.Office;

/// <summary>
/// The Excel window around the grid: an <see cref="OfficeShell"/> with the title bar, the ribbon, the
/// comments pane, the status bar and the File backstage.
/// </summary>
/// <remarks>
/// On by default (<see cref="ShowShell"/>). Every part is a switch of its own. Open, Save, Save As,
/// Export and the recent list go to the <see cref="DocumentStore"/> — the browser's own by default — so
/// the shell works end to end without any host code; Print opens the sheet as a PDF.
/// </remarks>
public partial class SpreadsheetView
{
    /// <summary>The zoom range the grid supports (10–400%), so the slider cannot ask for 500%.</summary>
    static readonly OfficeZoomModel ShellZoom = new(SpreadsheetController.MinZoom, SpreadsheetController.MaxZoom, 1.0);

    readonly OfficeCommandIndex commands = new();
    readonly OfficeStatusItem modeItem = new("mode", "Ready");
    readonly OfficeStatusItem aggregateItem = new("aggregates") { IsVisible = false };
    readonly List<OfficeStatusItem> statusItems;

    SpreadsheetToolbar? toolbar;
    IDisposable? commandSync;
    bool backstageOpen;
    bool commentsOpen;
    bool simplifiedRibbon;
    OfficeEditMode editMode;
    string? renamedTo;
    OfficeDocumentInfo? documentInfo;

    Workbook? ownedWorkbook;
    long savedRevision = -1;
    readonly OfficeDocumentSession session = new(OfficeApp.Excel);
    (Workbook Workbook, OfficeDocumentRef File)? attaching;
    IReadOnlyList<OfficeRecentFile>? storeRecent;
    OfficeSaveState? transientSaveState;
    OfficeSaveState lastSaved = OfficeSaveState.None;
    CancellationTokenSource? autoSave;

    public SpreadsheetView()
    {
        this.statusItems = [this.modeItem, this.aggregateItem];
        this.AddShellCommands();
    }

    // ---- switches ----

    /// <summary>
    /// Wraps the sheet in Excel's window — title bar, ribbon with File, comments pane, status bar and
    /// backstage. On by default; false gives the bare ribbon + formula bar + grid + tabs of before.
    /// </summary>
    [Parameter] public bool ShowShell { get; set; } = true;

    /// <summary>The green title bar: AutoSave, Save/Undo/Redo, the workbook name and the command search.</summary>
    [Parameter] public bool ShowTitleBar { get; set; } = true;

    /// <summary>Ready/Edit, the selection's Average/Count/Sum, the three views and the zoom slider.</summary>
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


    /// <summary>The Comments button and the pane listing every note in the workbook.</summary>
    [Parameter] public bool ShowCommentsPane { get; set; } = true;

    /// <summary>Comments / Editing mode / Share at the end of the ribbon's tab strip.</summary>
    [Parameter] public bool ShowRibbonActions { get; set; } = true;

    // ---- document ----

    /// <summary>The name in the title bar and backstage. Defaults to the file's name, or "Book1". Two-way.</summary>
    [Parameter] public string? DocumentName { get; set; }

    [Parameter] public EventCallback<string?> DocumentNameChanged { get; set; }

    /// <summary>
    /// Overrides the title bar's save status. Left null the view tracks it itself: nothing for an untouched
    /// workbook, "Unsaved changes" after an edit, "Saving…", then "Saved" / "Saved locally".
    /// </summary>
    [Parameter] public OfficeSaveState? SaveState { get; set; }

    /// <summary>
    /// The title bar's AutoSave switch. On, edits to a <see cref="DocumentStore"/> file are saved two
    /// seconds after the last one. A new workbook waits for its first Save, which asks where it goes.
    /// </summary>
    [Parameter] public bool AutoSave { get; set; }

    [Parameter] public EventCallback<bool> AutoSaveChanged { get; set; }

    /// <summary>The account shown in the title bar's avatar.</summary>
    [Parameter] public string? UserName { get; set; }

    /// <summary>The backstage's New page. Defaults to <see cref="SpreadsheetTemplates.All"/> — blank, budget, invoice, schedule.</summary>
    [Parameter] public IReadOnlyList<OfficeTemplate>? Templates { get; set; }

    /// <summary>
    /// Where Open, Save, Save As, Export and the recent list go. Null uses the registered
    /// <see cref="IOfficeDocumentStore"/> (<c>AddShinyOfficeDocumentStore()</c>), or a
    /// <see cref="BrowserOfficeDocumentStore"/> of the view's own when there is none.
    /// </summary>
    [Parameter] public IOfficeDocumentStore? DocumentStore { get; set; }

    /// <summary>The store file the workbook came from or was last saved as. Null for a new workbook, or one the host set.</summary>
    public OfficeDocumentRef? CurrentFile => this.session.Current;

    // ---- events ----

    /// <summary>
    /// A template was picked. Unhandled, the view builds it (<see cref="SpreadsheetTemplates.Create"/>, or
    /// the template's own <see cref="OfficeTemplate.Open"/> stream), shows it and raises <see cref="WorkbookChanged"/>.
    /// </summary>
    [Parameter] public EventCallback<OfficeTemplate> TemplateSelected { get; set; }

    /// <summary>The view replaced its workbook — a template from the backstage. Use <c>@bind-Workbook</c>.</summary>
    [Parameter] public EventCallback<Workbook?> WorkbookChanged { get; set; }

    /// <summary>The Share button beside the comments toggle.</summary>
    [Parameter] public EventCallback ShareRequested { get; set; }

    /// <summary>The command search behind the title bar — add the app's own commands to it.</summary>
    public OfficeCommandIndex Commands => this.commands;

    /// <summary>Whether the backstage is showing.</summary>
    public bool IsBackstageOpen => this.backstageOpen;

    /// <summary>Viewing mode (from the ribbon's mode menu) turns the workbook read-only.</summary>
    bool EffectiveReadOnly => this.ReadOnly || this.editMode == OfficeEditMode.Viewing;

    string EffectiveDocumentName
        => this.renamedTo
           ?? this.DocumentName
           ?? (this.Workbook?.Path is { } path ? System.IO.Path.GetFileNameWithoutExtension(path) : null)
           ?? OfficeAppInfo.Excel.DefaultDocumentName;

    OfficeSaveState EffectiveSaveState
    {
        get
        {
            if (this.SaveState is { } pinned)
                return pinned;

            if (this.transientSaveState is { } transient)
                return transient;

            if (this.Workbook is not { } book)
                return OfficeSaveState.None;

            return book.Revision != this.savedRevision ? OfficeSaveState.Unsaved : this.lastSaved;
        }
    }


    /// <summary>Opens the File backstage, as the ribbon's File button does.</summary>
    public Task OpenBackstageAsync() => this.OnBackstageOpenChangedAsync(true);


    // ---- shell state ----

    /// <summary>Called when a controller is built for a workbook: that is the "saved" baseline.</summary>
    void OnWorkbookAttached(Workbook workbook)
    {
        this.savedRevision = workbook.Revision;
        this.transientSaveState = null;
        this.lastSaved = OfficeSaveState.None;

        // A workbook the host set is not a store file; one this view opened is.
        this.session.Current = this.attaching is { } a && ReferenceEquals(a.Workbook, workbook) ? a.File : null;
        this.attaching = null;

        // A note says who wrote it; the signed-in name beats the machine's user name.
        if (!string.IsNullOrWhiteSpace(this.UserName) && this.controller is { } c)
            c.NoteAuthor = this.UserName;

        this.UpdateStatus();
    }

    void UpdateStatus()
    {
        var controller = this.controller;
        this.modeItem.Text = SpreadsheetShell.ModeText(controller?.EditMode ?? SheetEditMode.Ready);

        var text = controller is null ? null : SpreadsheetShell.Aggregates(controller.SelectionStatistics);
        this.aggregateItem.Text = text;
        this.aggregateItem.IsVisible = text is not null;
    }

    async Task OnBackstageOpenChangedAsync(bool open)
    {
        if (open && this.Workbook is { } book)
            this.documentInfo = SpreadsheetShell.DocumentInfo(book, this.EffectiveDocumentName);

        // The built-in templates get a picture of their first sheet, drawn once per app on the first visit.
        if (open && this.Templates is null)
            TemplateThumbnailCache.EnsureSpreadsheet();

        this.backstageOpen = open;
        this.StateHasChanged();

        if (open)
            await this.LoadRecentFilesAsync();
        else
            await this.FocusGridAsync();
    }

    async Task OnFileMenuAsync()
    {
        if (this.ShowBackstage && this.ShowFileCommands)
            await this.OnBackstageOpenChangedAsync(true);

        await this.FileMenuRequested.InvokeAsync();
    }

    Task OnCommentsOpenChangedAsync(bool open)
    {
        this.commentsOpen = open;
        this.StateHasChanged();
        return Task.CompletedTask;
    }

    void OnShellLayoutChanged(OfficeShellLayout layout)
    {
        this.simplifiedRibbon = layout.SimplifiedRibbon;
        this.StateHasChanged();
    }

    void OnEditModeChanged(OfficeEditMode mode)
    {
        this.editMode = mode;
        this.StateHasChanged();
    }

    async Task OnDocumentNameChangedAsync(string? name)
    {
        this.renamedTo = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        await this.DocumentNameChanged.InvokeAsync(this.renamedTo);
    }

    Task OnAutoSaveChangedAsync(bool on)
    {
        this.AutoSave = on;
        return this.AutoSaveChanged.InvokeAsync(on);
    }

    void OnViewModeChanged(string? id)
    {
        if (this.controller is { } c)
            c.ViewMode = SpreadsheetShell.ParseViewMode(id);

        this.canvas?.Invalidate();
    }

    void OnStatusZoomChanged(double zoom)
    {
        // The controller clamps and raises ZoomChanged, which is what reports it to a @bind-Zoom host.
        if (this.controller is { } c)
            c.Zoom = zoom;
        else
            this.Zoom = zoom;
    }

    void UndoFromShell()
    {
        this.controller?.Undo();
        this.canvas?.Invalidate();
    }

    void RedoFromShell()
    {
        this.controller?.Redo();
        this.canvas?.Invalidate();
    }

    void OnSearchSubmitted(string text)
    {
        if (this.controller is { } c && SpreadsheetShell.Search(c, text))
            this.canvas?.Invalidate();
    }

    async Task GoToNote(SpreadsheetNoteEntry note)
    {
        if (this.controller is not { } c)
            return;

        SpreadsheetShell.GoTo(c, note);
        this.canvas?.Invalidate();
        await this.FocusGridAsync();
    }


    // ---- backstage: new ----

    async Task OnTemplateSelectedAsync(OfficeTemplate template)
    {
        if (this.TemplateSelected.HasDelegate)
        {
            await this.TemplateSelected.InvokeAsync(template);
            return;
        }

        Workbook next;
        if (template.Open is { } open)
        {
            await using var stream = await open(CancellationToken.None);
            next = await Workbook.OpenAsync(stream);
        }
        else
        {
            next = SpreadsheetTemplates.Create(template);
        }

        await this.ReplaceWorkbookAsync(next, template.IsBlank ? NextBookName() : template.Name, null);
    }

    /// <summary>Shows a workbook this view built or opened, taking ownership of it and disposing the one it replaces.</summary>
    async Task ReplaceWorkbookAsync(Workbook next, string name, OfficeDocumentRef? file)
    {
        var previous = this.ownedWorkbook;
        this.ownedWorkbook = next;
        this.renamedTo = name;
        this.attaching = file is null ? null : (next, file);
        this.Workbook = next;
        this.SheetName = null;
        this.backstageOpen = false;

        this.OnParametersSet();
        this.canvas?.Invalidate();
        this.StateHasChanged();

        await this.WorkbookChanged.InvokeAsync(next);
        await this.DocumentNameChanged.InvokeAsync(this.renamedTo);

        // Only a workbook this view built is its to dispose; the host's are the host's.
        if (previous is not null && !ReferenceEquals(previous, next))
            previous.Dispose();
    }


    // ---- backstage: open ----

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
    /// picks when it is null — and shows it. The view owns the workbook it opens.
    /// </summary>
    /// <returns>False when the user cancelled or the file would not open.</returns>
    public async Task<bool> OpenAsync(OfficeDocumentRef? file = null, CancellationToken cancellationToken = default)
    {
        var store = this.Store;
        try
        {
            var opened = await this.session.OpenAsync(store, file, (s, ct) => Workbook.OpenAsync(s, cancellationToken: ct), cancellationToken);
            if (opened is not { } result)
                return false;

            await this.ReplaceWorkbookAsync(result.Document, result.File.DisplayName, result.File);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Console.Error.WriteLine($"[Shiny.Office] Could not open '{file?.Name}': {ex.Message}");
            return false;
        }
    }

    /// <summary>Opens a workbook from a stream and shows it, named after <paramref name="fileName"/>. The view owns it.</summary>
    public async Task OpenAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var workbook = await Workbook.OpenAsync(stream, cancellationToken: cancellationToken);
        await this.ReplaceWorkbookAsync(workbook, System.IO.Path.GetFileNameWithoutExtension(fileName), null);
    }

    /// <summary>Fills the backstage's recent list from the store.</summary>
    async Task LoadRecentFilesAsync()
    {
        try
        {
            this.storeRecent = await this.Store.GetRecentAsync(OfficeApp.Excel);
            this.StateHasChanged();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Shiny.Office] Could not read the recent list: {ex.Message}");
        }
    }

    /// <summary>AutoSave only overwrites: a workbook with no file yet would have to ask where it goes.</summary>
    bool CanAutoSave => this.session.Current is not null;

    static int bookNumber = 1;

    static string NextBookName() => $"Book{Interlocked.Increment(ref bookNumber)}";


    // ---- backstage: save, save as, export, print ----

    /// <summary>Saves the workbook as xlsx through the <see cref="DocumentStore"/>, back over <see cref="CurrentFile"/> when there is one.</summary>
    public Task SaveAsync() => this.WriteFileAsync(OfficeFileFormats.Xlsx, OfficeFileAction.Save);

    /// <summary>Writes the workbook (or, for CSV and PDF, the active sheet) as a new file; an xlsx becomes <see cref="CurrentFile"/>.</summary>
    public Task SaveAsAsync(OfficeFileFormat format) => this.WriteFileAsync(format, OfficeFileAction.SaveAs);

    /// <summary>Writes the workbook (or, for CSV and PDF, the active sheet) as a new file in <paramref name="format"/>.</summary>
    public Task ExportAsync(OfficeFileFormat format) => this.WriteFileAsync(format, OfficeFileAction.Export);

    /// <summary>Opens a PDF of the active sheet in a new tab, for the browser to print.</summary>
    public Task PrintAsync() => this.WriteFileAsync(OfficeFileFormats.Pdf, OfficeFileAction.Print);

    async Task WriteFileAsync(OfficeFileFormat format, OfficeFileAction action)
    {
        if (this.controller is not { } c)
            return;

        var workbook = c.Workbook;
        var sheet = c.Sheet;
        var revision = workbook.Revision;
        var saving = action is OfficeFileAction.Save or OfficeFileAction.SaveAs && format.IsNative;

        if (saving)
        {
            this.transientSaveState = OfficeSaveState.Saving;
            this.StateHasChanged();
        }

        try
        {
            if (action == OfficeFileAction.Print)
            {
                // The browser's print is a tab with the PDF in it, not a file anywhere.
                if (this.script is { } s)
                    await s.InvokeAsync("openFile", format.FileNameFor(this.EffectiveDocumentName), format.MimeType,
                        await SpreadsheetExport.ToBytesAsync(workbook, sheet, format));

                return;
            }

            var file = await this.session.SaveAsync(this.Store, format, action, this.EffectiveDocumentName,
                (stream, ct) => SpreadsheetExport.WriteAsync(workbook, sheet, format, stream, ct));

            if (file is null)
                return;

            if (saving)
            {
                this.savedRevision = revision;
                this.lastSaved = OfficeSaveState.Saved;
                this.renamedTo = file.DisplayName;
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
        if (!this.AutoSave || !this.CanAutoSave || this.Workbook is not { } book || book.Revision == this.savedRevision)
            return;

        this.autoSave?.Cancel();
        var cts = this.autoSave = new CancellationTokenSource();

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


    // ---- command search ----

    void AddShellCommands()
    {
        this.commands.Add(new OfficeCommand("Save", this.SaveAsync) { Id = "sheet:save", Category = "File", IsAvailable = () => this.ShowFileCommands, Shortcut = "Ctrl+S" });
        this.commands.Add(new OfficeCommand("New Workbook", () => this.OnBackstageOpenChangedAsync(true)) { Id = "sheet:new", Category = "File", IsAvailable = () => this.ShowFileCommands, Keywords = ["template", "blank"] });
        this.commands.Add(new OfficeCommand("Export to PDF", () => this.WriteFileAsync(OfficeFileFormats.Pdf, OfficeFileAction.Export)) { Id = "sheet:pdf", Category = "File › Export", IsAvailable = () => this.ShowFileCommands });
        this.commands.Add(new OfficeCommand("Export to CSV", () => this.WriteFileAsync(OfficeFileFormats.Csv, OfficeFileAction.Export)) { Id = "sheet:csv", Category = "File › Export", IsAvailable = () => this.ShowFileCommands, Keywords = ["comma"] });
        this.commands.Add(new OfficeCommand("Print", this.PrintAsync) { Id = "sheet:print", Category = "File", IsAvailable = () => this.ShowFileCommands, Shortcut = "Ctrl+P" });
        this.commands.Add(new OfficeCommand("Comments", () => this.OnCommentsOpenChangedAsync(!this.commentsOpen)) { Id = "sheet:comments", Category = "Review", Keywords = ["notes"] });
        this.commands.Add(new OfficeCommand("Normal View", () => this.OnViewModeChanged(OfficeViewModes.Normal.Id)) { Id = "sheet:normal", Category = "View" });
        this.commands.Add(new OfficeCommand("Page Layout", () => this.OnViewModeChanged(OfficeViewModes.PageLayout.Id)) { Id = "sheet:pagelayout", Category = "View", Keywords = ["print", "pages"] });
        this.commands.Add(new OfficeCommand("Page Break Preview", () => this.OnViewModeChanged(OfficeViewModes.PageBreak.Id)) { Id = "sheet:pagebreak", Category = "View", Keywords = ["print", "pages"] });
    }

    /// <summary>Indexes the ribbon once it exists — its tabs add their commands as they render.</summary>
    void SyncCommands()
    {
        if (this.commandSync is null && this.toolbar?.Ribbon is { } ribbon)
            this.commandSync = this.commands.SyncRibbon(ribbon);
    }

    void DisposeShell()
    {
        this.commandSync?.Dispose();
        this.autoSave?.Cancel();
        _ = this.ownStore?.DisposeAsync();

        // A host bound to WorkbookChanged was handed the workbook and may outlive this view with it.
        if (!this.WorkbookChanged.HasDelegate)
            this.ownedWorkbook?.Dispose();
    }
}
