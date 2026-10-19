using System.ComponentModel;
using System.Text.RegularExpressions;
using Shiny.Controls.Office.Document;
using Shiny.Controls.Office.Shell;
using Shiny.Controls.Office.Skia;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Ribbons;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.Office;

/// <summary>
/// Word's window around the editor: the title bar, ribbon actions, ruler, navigation and comments
/// panes, status bar and the File backstage — the <see cref="OfficeShell"/> parts, fed from the
/// controller through <see cref="WordShell"/>.
/// </summary>
/// <remarks>
/// <para>
/// On by default (<see cref="ShowShell"/>). Every part is created with the view and switched with
/// <c>IsVisible</c>, never added later: the AppKit head does not realise a child added after the first
/// layout. With the shell off the view is exactly the ribbon over the page, as it was before.
/// </para>
/// <para>
/// Open, Save, Save As, Export, Print and the recent list go to the <see cref="DocumentStore"/>; the
/// same actions are public methods (<see cref="SaveAsync"/>, <see cref="OpenAsync(OfficeDocumentRef?, CancellationToken)"/>…)
/// for an app that drives files itself. A template picked with nobody handling
/// <see cref="NewDocumentRequested"/> is opened here.
/// </para>
/// </remarks>
public partial class DocumentEditorView
{
    readonly OfficeTitleBar titleBar = new() { DocumentName = "Document1" };
    readonly OfficeDocumentSession session = new(OfficeApp.Word);
    (WordDocument Document, OfficeDocumentRef File)? attaching;
    WordDocument? ownedDocument;
    readonly OfficeRibbonActions ribbonActions = new();
    readonly OfficeRuler ruler = new() { HeightRequest = 24 };
    readonly OfficeNavigationPane navigationPaneView = new();
    readonly VerticalStackLayout commentsList = new() { Spacing = 8, Padding = new Thickness(12, 4, 12, 12) };
    readonly OfficeSidePane commentsPane = new() { Title = "Comments" };
    // The status bar's range is the editor's own clamp (25-400%), not the model's 10-500% default -
    // otherwise the slider ran to 500% and the page stopped growing at 400%.
    readonly OfficeStatusBar statusBar = new() { ZoomModel = new OfficeZoomModel(DocumentController.MinimumZoom, DocumentController.MaximumZoom, 1.0) };
    readonly OfficeBackstage backstage = new();
    readonly OfficeStyleGallery styleGallery = new() { Columns = 5 };
    readonly Image printPreview = new() { Aspect = Aspect.AspectFit, HeightRequest = 520 };
    readonly OfficeStatusItem pageItem = new("page", "Page 1 of 1") { IsClickable = true, Tooltip = "Open the navigation pane" };
    readonly OfficeStatusItem wordsItem = new("words", "0 words") { IsClickable = true, Tooltip = "Word count" };
    readonly OfficeStatusItem languageItem = new("language", WordShell.LanguageText());

    string headingSignature = string.Empty;
    string commentSignature = string.Empty;
    bool syncingShell;
    bool shellBuilt;
    OfficeIndents? pendingIndents;
    IReadOnlyList<OfficeTabStop>? pendingTabs;
    (double Left, double Right)? pendingMargins;
    IDispatcherTimer? rulerTimer;

    /// <summary>The command list the title bar's search reads: every ribbon command plus a few of Word's own.</summary>
    public OfficeCommandIndex CommandIndex { get; } = new();

    // ---- parts, for hosts and tests ----

    /// <summary>The Office window this view is dressed in.</summary>
    public OfficeShell Shell => this.shell;

    public OfficeTitleBar TitleBar => this.titleBar;

    public OfficeStatusBar StatusBar => this.statusBar;

    public OfficeRuler Ruler => this.ruler;

    public OfficeNavigationPane NavigationPane => this.navigationPaneView;

    public OfficeSidePane CommentsPane => this.commentsPane;

    public OfficeBackstage Backstage => this.backstage;

    public OfficeRibbonActions RibbonActions => this.ribbonActions;

    public OfficeStyleGallery StyleGallery => this.styleGallery;

    // ---- bindable properties ----

    static BindableProperty ShellFlag(string name, bool value, BindingMode mode = BindingMode.OneWay)
        => BindableProperty.Create(name, typeof(bool), typeof(DocumentEditorView), value, mode,
            propertyChanged: (b, _, _) => ((DocumentEditorView)b).ApplyShell());

    /// <summary>
    /// The File surface: the ribbon's File button and backstage, the title bar's Save and AutoSave, and
    /// Export / Print in the command search. Off when the app owns opening and saving — it calls
    /// <see cref="SaveAsync"/>, <see cref="OpenAsync(Stream, string, CancellationToken)"/> and the rest
    /// itself, or reads <see cref="Document"/> directly. <see cref="ShowFileButton"/> still shows File as a
    /// bare <see cref="FileMenuRequested"/> hook. The rest of the shell stays.
    /// </summary>
    public static readonly BindableProperty ShowFileCommandsProperty = BindableProperty.Create(
        nameof(ShowFileCommands), typeof(bool), typeof(DocumentEditorView), true,
        propertyChanged: (b, _, _) =>
        {
            var view = (DocumentEditorView)b;
            using var _ = view.ribbon.DeferRebuild();
            view.ApplyShell();
            view.ApplyFileButton(view.ShowFileButton);
        });

    /// <summary>Word's window — title bar, ruler, panes, status bar, backstage. On by default; off is the bare ribbon over the page.</summary>
    public static readonly BindableProperty ShowShellProperty = BindableProperty.Create(
        nameof(ShowShell), typeof(bool), typeof(DocumentEditorView), true,
        propertyChanged: (b, _, _) =>
        {
            var view = (DocumentEditorView)b;

            // Several of these are ribbon Redraw properties; rebuild the ribbon once, not per property.
            using var _ = view.ribbon.DeferRebuild();
            view.ApplyShell();
            view.ApplyNavigationPane();
            view.ApplyAccent();
            view.ApplyFileButton(view.ShowFileButton);
            view.ApplyAccent();
        });

    public static readonly BindableProperty ShowTitleBarProperty = ShellFlag(nameof(ShowTitleBar), true);
    public static readonly BindableProperty ShowStatusBarProperty = ShellFlag(nameof(ShowStatusBar), true);
    public static readonly BindableProperty ShowRulerProperty = ShellFlag(nameof(ShowRuler), true);

    /// <summary>The comments pane down the right. Two-way: the ribbon's Comments button and the pane's close move it.</summary>
    public static readonly BindableProperty ShowCommentsPaneProperty = ShellFlag(nameof(ShowCommentsPane), false, BindingMode.TwoWay);

    public static readonly BindableProperty DocumentNameProperty = BindableProperty.Create(
        nameof(DocumentName), typeof(string), typeof(DocumentEditorView), "Document1", BindingMode.TwoWay,
        propertyChanged: (b, _, n) =>
        {
            var view = (DocumentEditorView)b;
            view.titleBar.DocumentName = (string?)n;
            view.backstage.DocumentName = (string?)n;
        });

    public static readonly BindableProperty DocumentLocationProperty = BindableProperty.Create(
        nameof(DocumentLocation), typeof(string), typeof(DocumentEditorView), null,
        propertyChanged: (b, _, n) => ((DocumentEditorView)b).titleBar.DocumentLocation = (string?)n);

    /// <summary>The title bar's save status. Null (the default) follows the document: Unsaved while dirty, else Saved.</summary>
    public static readonly BindableProperty SaveStateProperty = BindableProperty.Create(
        nameof(SaveState), typeof(OfficeSaveState?), typeof(DocumentEditorView), null,
        propertyChanged: (b, _, _) => ((DocumentEditorView)b).RefreshTitle());

    public static readonly BindableProperty AutoSaveProperty = BindableProperty.Create(
        nameof(AutoSave), typeof(bool), typeof(DocumentEditorView), false, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => ((DocumentEditorView)b).titleBar.AutoSave = (bool)n);

    public static readonly BindableProperty UserNameProperty = BindableProperty.Create(
        nameof(UserName), typeof(string), typeof(DocumentEditorView), null,
        propertyChanged: (b, _, n) =>
        {
            var view = (DocumentEditorView)b;
            view.titleBar.UserName = (string?)n;
            if (!string.IsNullOrWhiteSpace((string?)n) && view.editor.Controller is { } controller)
                controller.Author = (string)n!;
        });

    /// <summary>The backstage's templates. Null (the default) is <see cref="WordTemplates.All"/>: Blank, Report, Letter.</summary>
    public static readonly BindableProperty TemplatesProperty = BindableProperty.Create(
        nameof(Templates), typeof(IEnumerable<OfficeTemplate>), typeof(DocumentEditorView), null,
        propertyChanged: (b, _, n) => ((DocumentEditorView)b).backstage.Templates = (IEnumerable<OfficeTemplate>?)n ?? TemplateThumbnailCache.WordReady ?? WordTemplates.All);

    /// <summary>The backstage's recent files — the host's list.</summary>

    public static readonly BindableProperty DocumentStoreProperty = BindableProperty.Create(
        nameof(DocumentStore), typeof(IOfficeDocumentStore), typeof(DocumentEditorView), null);

    /// <summary>Editing / Reviewing (track changes on) / Viewing (read-only). Two-way; the ribbon's mode dropdown sets it.</summary>
    public static readonly BindableProperty EditModeProperty = BindableProperty.Create(
        nameof(EditMode), typeof(OfficeEditMode), typeof(DocumentEditorView), OfficeEditMode.Editing, BindingMode.TwoWay,
        propertyChanged: (b, _, _) => ((DocumentEditorView)b).ApplyEditMode());

    public bool ShowShell { get => (bool)this.GetValue(ShowShellProperty); set => this.SetValue(ShowShellProperty, value); }
    public bool ShowFileCommands { get => (bool)this.GetValue(ShowFileCommandsProperty); set => this.SetValue(ShowFileCommandsProperty, value); }
    public bool ShowTitleBar { get => (bool)this.GetValue(ShowTitleBarProperty); set => this.SetValue(ShowTitleBarProperty, value); }
    public bool ShowStatusBar { get => (bool)this.GetValue(ShowStatusBarProperty); set => this.SetValue(ShowStatusBarProperty, value); }
    public bool ShowRuler { get => (bool)this.GetValue(ShowRulerProperty); set => this.SetValue(ShowRulerProperty, value); }
    public bool ShowCommentsPane { get => (bool)this.GetValue(ShowCommentsPaneProperty); set => this.SetValue(ShowCommentsPaneProperty, value); }
    public string? DocumentName { get => (string?)this.GetValue(DocumentNameProperty); set => this.SetValue(DocumentNameProperty, value); }
    public string? DocumentLocation { get => (string?)this.GetValue(DocumentLocationProperty); set => this.SetValue(DocumentLocationProperty, value); }
    public OfficeSaveState? SaveState { get => (OfficeSaveState?)this.GetValue(SaveStateProperty); set => this.SetValue(SaveStateProperty, value); }
    public bool AutoSave { get => (bool)this.GetValue(AutoSaveProperty); set => this.SetValue(AutoSaveProperty, value); }
    public string? UserName { get => (string?)this.GetValue(UserNameProperty); set => this.SetValue(UserNameProperty, value); }
    public IEnumerable<OfficeTemplate>? Templates { get => (IEnumerable<OfficeTemplate>?)this.GetValue(TemplatesProperty); set => this.SetValue(TemplatesProperty, value); }

    /// <summary>
    /// Where Open, Save, Save As, Export, Print and the recent list go. Null uses the app's registered
    /// <see cref="IOfficeDocumentStore"/> (<c>UseOfficeDocumentStore()</c>), or a
    /// <see cref="MauiOfficeDocumentStore"/> over its default folder when there is none.
    /// </summary>
    public IOfficeDocumentStore? DocumentStore { get => (IOfficeDocumentStore?)this.GetValue(DocumentStoreProperty); set => this.SetValue(DocumentStoreProperty, value); }

    /// <summary>The store file the document came from or was last saved as. Null for a new document, or one the host set.</summary>
    public OfficeDocumentRef? CurrentFile => this.session.Current;
    public OfficeEditMode EditMode { get => (OfficeEditMode)this.GetValue(EditModeProperty); set => this.SetValue(EditModeProperty, value); }

    /// <summary>What the title bar is showing as the save status right now.</summary>
    public OfficeSaveState EffectiveSaveState
        => this.SaveState ?? (this.Document?.IsDirty == true ? OfficeSaveState.Unsaved : OfficeSaveState.Saved);

    // ---- events ----

    /// <summary>A template was picked in the backstage. With no handler the view opens it itself and raises <see cref="DocumentOpened"/>.</summary>
    public event EventHandler<OfficeTemplate>? NewDocumentRequested;

    /// <summary>
    /// The view opened a document itself — a template picked with no <see cref="NewDocumentRequested"/>
    /// handler, or a file from the <see cref="DocumentStore"/>. Handled, the host owns it from then on;
    /// unhandled, the view disposes it when it is replaced.
    /// </summary>
    public event EventHandler<WordDocument>? DocumentOpened;

    public event EventHandler? ShareRequested;

    public event EventHandler<string>? DocumentRenamed;

    // ---- actions ----

    /// <summary>Writes the document to <paramref name="output"/> as a PDF, one page per printed page. Returns the page count.</summary>
    public int ExportPdf(Stream output)
    {
        if (this.Document is not { } document)
            throw new InvalidOperationException("There is no document to export.");

        return DocumentPdfExporter.Export(document, output, new DocumentPdfOptions
        {
            Title = this.DocumentName,
            Author = this.editor.Controller?.Author,
            Time = ShinyTime.For(this)
        });
    }

    /// <summary>Applies ruler drags that are waiting out their debounce now. Test seam; a drag commits on its own a moment after it stops.</summary>
    public void CommitRulerEdits()
    {
        this.rulerTimer?.Stop();
        var controller = this.editor.Controller;
        var changed = false;

        if (this.pendingIndents is { } indents)
            changed |= WordShell.ApplyIndents(controller, indents);

        if (this.pendingTabs is { } tabs)
            changed |= WordShell.ApplyTabStops(controller, tabs);

        if (this.pendingMargins is { } margins)
            changed |= WordShell.ApplyMargins(controller, margins.Left, margins.Right);

        this.pendingIndents = null;
        this.pendingTabs = null;
        this.pendingMargins = null;

        if (changed)
            this.AfterCommand();
    }

    // ---- building ----

    void BuildShell()
    {
        this.shell.TitleBar = this.titleBar;
        this.shell.Ribbon = this.ribbon;
        this.shell.Ruler = this.ruler;
        this.shell.LeftPane = this.navigationPaneView;
        this.shell.ShellContent = this.root;
        this.shell.RightPane = this.commentsPane;
        this.shell.StatusBar = this.statusBar;
        this.shell.Backstage = this.backstage;
        this.ribbon.HeaderEndContent = this.ribbonActions;
        this.ribbon.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Ribbon.DisplayMode))
                this.ApplyStylesPicker();
        };

        // Title bar
        this.titleBar.CommandIndex = this.CommandIndex;
        this.titleBar.SaveRequested += (_, _) => _ = this.SaveAsync();
        this.titleBar.UndoRequested += (_, _) => this.Run(c => c.Undo());
        this.titleBar.RedoRequested += (_, _) => this.Run(c => c.Redo());
        this.titleBar.DocumentRenamed += (_, name) => this.DocumentRenamed?.Invoke(this, name);
        this.titleBar.SearchSubmitted += (_, query) => this.SearchDocument(query);
        this.titleBar.PropertyChanged += this.OnTitleBarPropertyChanged;

        // Ribbon actions
        this.ribbonActions.PropertyChanged += this.OnRibbonActionsPropertyChanged;
        this.ribbonActions.ShareClicked += (_, _) => this.ShareRequested?.Invoke(this, EventArgs.Empty);

        // Ruler: a drag reports every step, so the edit waits for the drag to settle - one undo step, not fifty.
        this.ruler.IndentsChanged += (_, indents) => { this.pendingIndents = indents; this.ScheduleRulerCommit(); };
        this.ruler.TabStopsChanged += (_, tabs) => { this.pendingTabs = tabs; this.ScheduleRulerCommit(); };
        this.ruler.MarginsChanged += (_, margins) => { this.pendingMargins = margins; this.ScheduleRulerCommit(); };

        // Navigation pane
        this.navigationPaneView.HeadingSelected += (_, heading) =>
        {
            if (WordShell.GoTo(this.editor.Controller, heading))
                this.editor.RestoreFocus();
        };
        this.navigationPaneView.SearchRequested += (_, text) =>
            this.navigationPaneView.SearchResults = WordShell.Search(this.editor.Controller, text);
        this.navigationPaneView.ResultSelected += (_, result) => WordShell.GoTo(this.editor.Controller, result);

        // Comments pane
        var newComment = ShellChrome.TextButton("New", () =>
        {
            if (this.editor.Controller is { } controller)
                _ = this.NewCommentAsync(controller);
        }, Shiny.Controls.Office.Icons.OfficeShellIcon.Comments, null, out _);
        this.commentsPane.HeaderContent = newComment;
        this.commentsPane.PaneContent = new ScrollView { Content = this.commentsList };

        // Status bar
        this.statusBar.Items.Add(this.pageItem);
        this.statusBar.Items.Add(this.wordsItem);
        this.statusBar.Items.Add(this.languageItem);
        this.statusBar.Zoom = this.Zoom;
        this.statusBar.ItemClicked += this.OnStatusItemClicked;
        this.statusBar.ViewModeChanged += (_, mode) => this.ApplyViewMode(mode.Id);
        this.statusBar.PropertyChanged += this.OnStatusBarPropertyChanged;

        // Backstage
        this.backstage.Templates = this.Templates ?? TemplateThumbnailCache.WordReady ?? WordTemplates.All;
        this.backstage.DocumentName = this.DocumentName;
        this.backstage.PrintPreview = this.printPreview;
        this.backstage.PropertyChanged += this.OnBackstagePropertyChanged;
        this.backstage.TemplateSelected += (_, template) => _ = this.NewFromTemplateAsync(template);
        this.backstage.OpenRequested += (_, _) => _ = this.OpenAsync();
        this.backstage.RecentFileSelected += (_, file) => _ = this.OpenRecentAsync(file);
        this.backstage.SaveRequested += (_, _) => _ = this.SaveAsync();
        this.backstage.SaveAsRequested += (_, format) => _ = this.SaveAsAsync(format);
        this.backstage.ExportRequested += (_, format) => _ = this.ExportAsync(format);
        this.backstage.PrintRequested += (_, _) => _ = this.PrintAsync();

        // Style gallery
        this.styleGallery.StyleSelected += (_, style) => this.Run(c => c.ApplyStyle(style.Id));

        // Shell
        this.shell.PropertyChanged += this.OnShellPropertyChanged;

        this.shellBuilt = true;
        this.ApplyFileButton(this.ShowFileButton);
        this.ApplyAccent();
        this.ApplyShell();
        this.ApplyNavigationPane();
        this.RefreshShell();
    }

    /// <summary>Runs after every <c>BuildBar</c>: shortcut text moves off tooltips, and the command search is refilled.</summary>
    void AfterBarBuilt()
    {
        foreach (var item in this.ribbon.QuickAccessItems)
            MoveShortcut(item);

        foreach (var tab in this.ribbon.Tabs)
        foreach (var group in tab.Groups)
        foreach (var item in group.Items)
            MoveShortcut(item);

        this.CommandIndex.Clear();
        this.CommandIndex.AddRibbon(this.ribbon);

        this.CommandIndex.Add(new OfficeCommand("Navigation Pane", () => this.ShowNavigationPane = true)
        {
            Category = "View › Show",
            Keywords = ["go to", "headings", "outline", "jump"]
        });
        this.CommandIndex.Add(new OfficeCommand("Comments", () => this.ShowCommentsPane = true)
        {
            Category = "Review › Comments",
            Keywords = ["comment", "pane", "review"]
        });
        this.CommandIndex.Add(new OfficeCommand("Word Count", () =>
        {
            if (this.editor.Controller is { } controller)
                _ = this.ShowWordCountAsync(controller);
        })
        {
            Category = "Review › Proofing",
            Shortcut = "Ctrl+Shift+G",
            Keywords = ["statistics", "count", "characters"]
        });
        this.CommandIndex.Add(new OfficeCommand("Export to PDF", () => this.ExportAsync(OfficeFileFormats.Pdf))
        {
            Category = "File › Export",
            IsAvailable = () => this.ShowFileCommands,
            Keywords = ["pdf", "save as", "print"]
        });
        this.CommandIndex.Add(new OfficeCommand("Print", this.PrintAsync)
        {
            Category = "File",
            Shortcut = "Ctrl+P",
            IsAvailable = () => this.ShowFileCommands
        });
    }

    static readonly Regex ShortcutInTooltip = new(@"^(?<name>.+?) \((?<keys>(?:Ctrl|Shift|Alt|Cmd|Esc|F\d{1,2})[^()]*)\)(?<rest>.*)$", RegexOptions.Compiled);

    /// <summary>"Bold (Ctrl+B)" becomes Tooltip "Bold" + Shortcut "Ctrl+B", so the search can show the keys too.</summary>
    static void MoveShortcut(RibbonItem item)
    {
        if (item is RibbonRow row)
        {
            foreach (var child in row.Items)
                MoveShortcut(child);

            return;
        }

        if (!string.IsNullOrWhiteSpace(item.Shortcut) || item.Tooltip is not { } tooltip)
            return;

        var match = ShortcutInTooltip.Match(tooltip);
        if (!match.Success)
            return;

        item.Shortcut = match.Groups["keys"].Value;
        item.Tooltip = match.Groups["name"].Value + match.Groups["rest"].Value;
    }

    // ---- applying ----

    void ApplyShell()
    {
        if (!this.shellBuilt)
            return;

        var on = this.ShowShell;
        this.titleBar.IsVisible = on && this.ShowTitleBar;
        this.statusBar.IsVisible = on && this.ShowStatusBar;
        this.ribbonActions.IsVisible = on;

        var files = this.ShowFileCommands;
        this.backstage.IsVisible = on && files;
        this.titleBar.ShowSave = files;
        this.titleBar.ShowAutoSave = files;

        if (!(on && files))
            this.shell.IsBackstageOpen = false;

        if (!on)
            this.shell.IsFocusMode = false;

        this.shell.IsRightPaneOpen = on && this.ShowCommentsPane;

        // The title bar owns undo and redo while it shows; otherwise they are the ribbon's quick access.
        this.ribbon.ShowQuickAccess = !(on && this.ShowTitleBar);

        this.ApplyStylesPicker();

        this.syncingShell = true;
        this.ribbonActions.IsCommentsOpen = this.ShowCommentsPane;
        this.syncingShell = false;

        this.RefreshRuler();
    }

    /// <summary>
    /// The gallery in the shell, the plain dropdown otherwise - and in the simplified (phone) ribbon,
    /// whose one-line rows are ~32pt: the 60pt gallery was cut down to a strip of clipped previews.
    /// </summary>
    void ApplyStylesPicker()
    {
        var gallery = this.ShowShell && this.ribbon.DisplayMode != RibbonDisplayMode.Simplified;
        this.styleGallery.IsVisible = gallery;
        if (this.stylesMenu is not null)
            this.stylesMenu.IsVisible = !gallery;
    }

        void ApplyEditorReadOnly()
        => this.editor.IsReadOnly = this.IsReadOnly || this.ReadMode || this.EditMode == OfficeEditMode.Viewing;

    void ApplyEditMode()
    {
        this.syncingShell = true;
        this.ribbonActions.EditMode = this.EditMode;
        this.syncingShell = false;

        if (this.editor.Controller is { } controller && this.EditMode == OfficeEditMode.Reviewing && !controller.IsTrackingChanges)
            controller.IsTrackingChanges = true;

        this.ApplyEditorReadOnly();
        this.RefreshBar();
    }

    void ApplyViewMode(string? id)
    {
        var (readMode, layout) = WordShell.FromViewModeId(id, this.editor.PageLayout);

        if (readMode)
        {
            this.ReadMode = true;
        }
        else
        {
            this.ReadMode = false;
            this.editor.PageLayout = layout;
        }

        this.RefreshShell();
    }

    // ---- refreshing ----

    void OnShellControllerAttached()
    {
        if (!this.shellBuilt)
            return;

        if (this.editor.Controller is { } controller)
        {
            if (!string.IsNullOrWhiteSpace(this.UserName))
                controller.Author = this.UserName!;

            if (this.EditMode == OfficeEditMode.Reviewing)
                controller.IsTrackingChanges = true;
        }

        this.headingSignature = string.Empty;
        this.commentSignature = string.Empty;
        this.navigationPaneView.SearchResults = null;
        this.RefreshShell();
    }

    /// <summary>Everything the shell shows that follows the controller.</summary>
    void RefreshShell()
    {
        if (!this.shellBuilt)
            return;

        this.RefreshTitle();
        this.RefreshStatus();
        this.RefreshRuler();
        this.RefreshNavigation();
        this.RefreshComments();

        if (this.editor.Controller is { } controller)
        {
            this.styleGallery.Styles = WordShell.Styles(controller);
            this.styleGallery.SelectedStyleId = controller.CurrentStyleId;
        }
    }

    void RefreshTitle()
    {
        if (!this.shellBuilt)
            return;

        var controller = this.editor.Controller;
        this.titleBar.CanUndo = controller?.CanUndo ?? false;
        this.titleBar.CanRedo = controller?.CanRedo ?? false;
        this.titleBar.SaveState = this.EffectiveSaveState;
    }

    void RefreshStatus()
    {
        if (!this.shellBuilt)
            return;

        var controller = this.editor.Controller;
        var statistics = controller?.Statistics ?? DocumentStatistics.Empty;

        this.pageItem.Text = WordShell.PageText(statistics);
        this.wordsItem.Text = WordShell.WordsText(statistics);
        this.pageItem.IsVisible = this.editor.PageLayout == DocumentPageLayout.Print && !this.ReadMode;

        this.syncingShell = true;
        this.statusBar.SelectedViewMode = WordShell.ViewModeId(this.ReadMode, this.editor.PageLayout);

        if (Math.Abs(this.statusBar.Zoom - this.editor.Zoom) > 0.0001)
            this.statusBar.Zoom = this.editor.Zoom;

        this.syncingShell = false;

        if (this.Document is { } document)
        {
            this.statusBar.PageWidth = document.Page.Width;
            this.statusBar.PageHeight = document.Page.Height;
            this.statusBar.TextWidth = document.Page.ContentWidth;
        }

        this.statusBar.ViewportWidth = this.editor.Width > 0 ? this.editor.Width : 0;
        this.statusBar.ViewportHeight = this.editor.Height > 0 ? this.editor.Height : 0;
    }

    void RefreshRuler()
    {
        if (!this.shellBuilt)
            return;

        var controller = this.editor.Controller;
        var print = this.editor.PageLayout == DocumentPageLayout.Print;
        this.ruler.IsVisible = this.ShowShell && this.ShowRuler && !this.ReadMode && print && controller is not null;

        if (controller is null || this.Document is not { } document)
            return;

        var scale = controller.ViewScale;
        this.ruler.PageWidth = WordShell.ToPoints(document.Page.Width);
        this.ruler.Zoom = scale;

        // The ruler sits over the content column, so the page's left edge in the editor's own pixels is
        // where the ruler's zero goes - less the hand-built navigation pane, which is gone with the shell on.
        this.ruler.PageOffset = controller.PageX * scale;

        // A drag in progress owns the markers until it is committed.
        if (this.pendingMargins is null)
        {
            this.ruler.LeftMargin = WordShell.ToPoints(controller.PageMargins.Left);
            this.ruler.RightMargin = WordShell.ToPoints(controller.PageMargins.Right);
        }

        if (this.pendingIndents is null)
            this.ruler.Indents = WordShell.Indents(controller.CaretFormat);

        if (this.pendingTabs is null)
            this.ruler.TabStops = WordShell.TabStops(controller);

        this.ruler.IsEnabled = !this.IsReadOnly && !this.ReadMode && this.EditMode != OfficeEditMode.Viewing;
    }

    void RefreshNavigation()
    {
        if (!this.shellBuilt)
            return;

        var controller = this.editor.Controller;
        var headings = WordShell.Headings(controller);
        var signature = string.Join("\n", headings.Select(x => $"{x.Id}|{x.Level}|{x.Text}"));

        if (signature != this.headingSignature)
        {
            this.headingSignature = signature;
            this.navigationPaneView.Headings = headings;
        }

        this.navigationPaneView.CurrentHeadingId = WordShell.CurrentHeadingId(controller, headings);
    }

    void RefreshComments()
    {
        var controller = this.editor.Controller;
        var comments = controller?.Comments ?? [];
        var current = controller?.CurrentComment?.Id;
        var signature = string.Join("\n", comments.Select(x => $"{x.Id}|{x.Author}|{x.Date:O}|{x.Text}")) + "#" + current + "#" + this.IsReadOnly;

        if (signature == this.commentSignature)
            return;

        this.commentSignature = signature;
        this.commentsList.Children.Clear();

        if (controller is null || comments.Count == 0)
        {
            var empty = ShellChrome.Text("No comments yet. Select some text and choose New to add one.", 13);
            empty.LineBreakMode = LineBreakMode.WordWrap;
            empty.Opacity = 0.75;
            this.commentsList.Children.Add(empty);
            return;
        }

        var now = controller.Time.GetLocalNow();
        foreach (var comment in comments)
            this.commentsList.Children.Add(this.CommentCard(controller, comment, comment.Id == current, now));
    }

    View CommentCard(DocumentEditorController controller, DocumentComment comment, bool isCurrent, DateTimeOffset now)
    {
        var id = comment.Id;
        var author = ShellChrome.Text(comment.Author, 13, attributes: FontAttributes.Bold);
        var date = ShellChrome.Text(WordShell.CommentDate(comment, now), 11);
        date.Opacity = 0.7;

        var quote = ShellChrome.Text(controller.CommentedText(comment), 12);
        quote.FontAttributes = FontAttributes.Italic;
        quote.Opacity = 0.75;
        quote.LineBreakMode = LineBreakMode.TailTruncation;
        quote.MaxLines = 2;

        var text = ShellChrome.Text(comment.Text, 13);
        text.LineBreakMode = LineBreakMode.WordWrap;

        var delete = ShellChrome.IconButton(Shiny.Controls.Office.Icons.OfficeShellIcon.Close, "Delete comment", () =>
        {
            if (this.IsReadOnly || this.ReadMode || this.editor.Controller is not { } c)
                return;

            c.DeleteComment(id);
            this.AfterCommand();
        }, size: 12);
        delete.IsVisible = !this.IsReadOnly;

        var head = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        head.Add(new VerticalStackLayout { Spacing = 0, Children = { author, date } }, 0);
        head.Add(delete, 1);

        var card = new Border
        {
            Padding = new Thickness(10, 8),
            StrokeThickness = isCurrent ? 2 : 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            AutomationId = $"DocComment{id}",
            Content = new VerticalStackLayout { Spacing = 4, Children = { head, quote, text } }
        };
        card.SetDynamicResource(BackgroundColorProperty, ShinyThemeKeys.Color.SurfaceContainerHighest);
        card.SetDynamicResource(Border.StrokeProperty, isCurrent ? ShinyThemeKeys.Brush.Primary : ShinyThemeKeys.Brush.OutlineVariant);

        card.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                if (this.editor.Controller?.GoToComment(id) == true)
                    this.editor.RestoreFocus();
            })
        });

        return card;
    }

    void RefreshPrintPreview()
    {
        if (this.Document is not { } document)
        {
            this.printPreview.Source = null;
            return;
        }

        try
        {
            var png = DocumentPdfExporter.RenderPagePng(document, 0, 1f);
            this.printPreview.Source = ImageSource.FromStream(() => new MemoryStream(png));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Shiny.Office] Print preview failed: {ex.Message}");
            this.printPreview.Source = null;
        }
    }

    // ---- reacting ----

    // ---- files ----

    /// <summary>Saves the document as .docx through the <see cref="DocumentStore"/>, back over <see cref="CurrentFile"/> when there is one.</summary>
    public Task SaveAsync() => this.WriteToStoreAsync(OfficeFileFormats.Docx, OfficeFileAction.Save);

    /// <summary>Writes the document as a new file in <paramref name="format"/>; a .docx becomes <see cref="CurrentFile"/>.</summary>
    public Task SaveAsAsync(OfficeFileFormat format) => this.WriteToStoreAsync(format, OfficeFileAction.SaveAs);

    /// <summary>Writes the document as a new file in <paramref name="format"/> (.pdf, .txt, .html, .docx).</summary>
    public Task ExportAsync(OfficeFileFormat format) => this.WriteToStoreAsync(format, OfficeFileAction.Export);

    /// <summary>Hands the store a PDF of the document to print — MauiOfficeDocumentStore opens it in the system viewer.</summary>
    public Task PrintAsync() => this.WriteToStoreAsync(OfficeFileFormats.Pdf, OfficeFileAction.Print);

    async Task WriteToStoreAsync(OfficeFileFormat format, OfficeFileAction action)
    {
        if (this.Document is not { } document)
            return;

        var store = this.ResolveDocumentStore(this.DocumentStore);
        if (!DocumentExport.CanWrite(format))
        {
            System.Diagnostics.Debug.WriteLine($"[Shiny.Office] No built-in writer for {format.Extension}.");
            return;
        }

        var name = this.DocumentName ?? OfficeAppInfo.Word.DefaultDocumentName;
        var options = new DocumentPdfOptions { Title = name, Author = this.editor.Controller?.Author, Time = ShinyTime.For(this) };
        var saving = action is OfficeFileAction.Save or OfficeFileAction.SaveAs && format.IsNative;

        try
        {
            if (saving)
                this.titleBar.SaveState = OfficeSaveState.Saving;

            var written = await this.session.SaveAsync(store, format, action, name,
                (s, ct) => DocumentExport.WriteAsync(document, format, s, options, ct));

            if (saving && written is not null)
            {
                document.MarkClean();
                this.DocumentName = written.DisplayName;
                this.DocumentLocation = written.Location;
            }

            if (written is not null && action != OfficeFileAction.Print)
                this.shell.IsBackstageOpen = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Shiny.Office] {action} failed: {ex.Message}");
            if (saving)
                this.titleBar.SaveState = OfficeSaveState.Error;

            return;
        }

        this.RefreshTitle();
    }

    async Task OpenRecentAsync(OfficeRecentFile file)
    {
        if (file.Document is { } document)
            await this.OpenAsync(document);
    }

    /// <summary>
    /// Opens a file from the <see cref="DocumentStore"/> — <paramref name="file"/>, or the one the user
    /// picks when it is null — and shows it. The view owns the document it opens (see <see cref="DocumentOpened"/>).
    /// </summary>
    /// <returns>False when the user cancelled or the file would not open.</returns>
    public async Task<bool> OpenAsync(OfficeDocumentRef? file = null, CancellationToken cancellationToken = default)
    {
        var store = this.ResolveDocumentStore(this.DocumentStore);
        try
        {
            var opened = await this.session.OpenAsync(store, file, (s, ct) => WordDocument.OpenAsync(s, editable: true, cancellationToken: ct), cancellationToken);
            if (opened is not { } result)
                return false;

            this.ReplaceDocument(result.Document, result.File.DisplayName, result.File);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[Shiny.Office] Could not open '{file?.Name}': {ex.Message}");
            return false;
        }
    }

    /// <summary>Opens a .docx from a stream and shows it, named after <paramref name="fileName"/>. The view owns it.</summary>
    public async Task OpenAsync(Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var document = await WordDocument.OpenAsync(stream, editable: true, cancellationToken: cancellationToken);
        this.ReplaceDocument(document, Path.GetFileNameWithoutExtension(fileName), null);
    }

    /// <summary>Shows a document this view opened, taking ownership of it and disposing the one it replaces.</summary>
    void ReplaceDocument(WordDocument document, string name, OfficeDocumentRef? file)
    {
        var previous = this.ownedDocument;
        this.ownedDocument = document;
        this.attaching = file is null ? null : (document, file);

        this.Document = document;
        this.DocumentName = name;
        this.DocumentLocation = file?.Location;
        this.shell.IsBackstageOpen = false;
        this.DocumentOpened?.Invoke(this, document);

        if (previous is not null && !ReferenceEquals(previous, document) && this.DocumentOpened is null)
            previous.Dispose();
    }

    /// <summary>The Document property moved: a document the host set is not a store file; one this view opened is.</summary>
    void TrackFile(WordDocument? document)
    {
        this.session.Current = this.attaching is { } a && ReferenceEquals(a.Document, document) ? a.File : null;
        this.attaching = null;
    }

    /// <summary>Fills the backstage's recent list from the store.</summary>
    async Task LoadRecentFilesAsync()
    {
        try
        {
            var recent = await this.ResolveDocumentStore(this.DocumentStore).GetRecentAsync(OfficeApp.Word);
            if (this.Dispatcher is { IsDispatchRequired: true } dispatcher)
                dispatcher.Dispatch(() => this.backstage.RecentFiles = recent);
            else
                this.backstage.RecentFiles = recent;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Shiny.Office] Could not read the recent list: {ex.Message}");
        }
    }

    void SearchDocument(string query)
    {
        this.ShowNavigationPane = true;
        this.navigationPaneView.SearchText = query;
        this.navigationPaneView.SearchResults = WordShell.Search(this.editor.Controller, query);
        this.navigationPaneView.SelectedTab = OfficeNavigationTab.Results;
    }

    async Task NewFromTemplateAsync(OfficeTemplate template)
    {
        if (this.NewDocumentRequested is { } handler)
        {
            handler(this, template);
            return;
        }

        try
        {
            var document = await WordTemplates.OpenAsync(template, time: ShinyTime.For(this));
            this.ReplaceDocument(document, template.IsBlank ? "Document1" : template.Name, null);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Shiny.Office] Could not open template '{template.Id}': {ex.Message}");
        }
    }

    void OnStatusItemClicked(object? sender, OfficeStatusItem item)
    {
        if (item == this.pageItem)
        {
            this.ShowNavigationPane = true;
            this.navigationPaneView.SelectedTab = OfficeNavigationTab.Headings;
        }
        else if (item == this.wordsItem && this.editor.Controller is { } controller)
        {
            _ = this.ShowWordCountAsync(controller);
        }
    }

    void OnStatusBarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (this.syncingShell || e.PropertyName != nameof(OfficeStatusBar.Zoom))
            return;

        if (Math.Abs(this.Zoom - this.statusBar.Zoom) > 0.0001)
            this.Zoom = this.statusBar.Zoom;
    }

    void OnTitleBarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(OfficeTitleBar.DocumentName) when this.titleBar.DocumentName != this.DocumentName:
                this.DocumentName = this.titleBar.DocumentName;
                break;

            case nameof(OfficeTitleBar.AutoSave) when this.titleBar.AutoSave != this.AutoSave:
                this.AutoSave = this.titleBar.AutoSave;
                break;
        }
    }

    void OnRibbonActionsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (this.syncingShell)
            return;

        switch (e.PropertyName)
        {
            case nameof(OfficeRibbonActions.IsCommentsOpen):
                this.ShowCommentsPane = this.ribbonActions.IsCommentsOpen;
                break;

            case nameof(OfficeRibbonActions.EditMode):
                this.EditMode = this.ribbonActions.EditMode;
                break;
        }
    }

    void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            // The navigation pane's own close, or the compact layout, moved the pane.
            case nameof(OfficeShell.IsLeftPaneOpen) when this.ShowShell && this.shell.IsLeftPaneOpen != this.ShowNavigationPane:
                this.ShowNavigationPane = this.shell.IsLeftPaneOpen;
                break;

            case nameof(OfficeShell.IsRightPaneOpen) when this.ShowShell && this.shell.IsRightPaneOpen != this.ShowCommentsPane:
                this.ShowCommentsPane = this.shell.IsRightPaneOpen;
                break;

            case nameof(OfficeShell.IsBackstageOpen) when this.shell.IsBackstageOpen:
                if (!this.ShowShell || !this.ShowFileCommands)
                {
                    this.shell.IsBackstageOpen = false;
                    break;
                }

                this.backstage.DocumentInfo = WordShell.DocumentInfo(this.editor.Controller, this.DocumentName, this.DocumentLocation);
                break;
        }
    }

    void OnBackstagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OfficeBackstage.IsOpen) && this.backstage.IsOpen)
        {
            _ = this.LoadTemplateThumbnailsAsync();
            _ = this.LoadRecentFilesAsync();
        }

        if (e.PropertyName == nameof(OfficeBackstage.SelectedPage) && this.backstage.SelectedPage == OfficeBackstagePage.Print)
            this.RefreshPrintPreview();
    }

    /// <summary>
    /// Gives the built-in templates a picture of their first page, as PowerPoint's have of their first
    /// slide. Drawn once per process the first time a backstage opens; a host's own list is left alone.
    /// </summary>
    async Task LoadTemplateThumbnailsAsync()
    {
        if (this.Templates is not null)
            return;

        var templates = await TemplateThumbnailCache.Word;
        if (this.Dispatcher is { IsDispatchRequired: true } dispatcher)
            dispatcher.Dispatch(() => this.backstage.Templates = this.Templates ?? templates);
        else
            this.backstage.Templates = this.Templates ?? templates;
    }

    void ScheduleRulerCommit()
    {
        if (this.Dispatcher is not { } dispatcher)
        {
            this.CommitRulerEdits();
            return;
        }

        if (this.rulerTimer is null)
        {
            this.rulerTimer = dispatcher.CreateTimer();
            this.rulerTimer.Interval = TimeSpan.FromMilliseconds(350);
            this.rulerTimer.IsRepeating = false;
            this.rulerTimer.Tick += (_, _) => this.CommitRulerEdits();
        }

        this.rulerTimer.Stop();
        this.rulerTimer.Start();
    }
}
