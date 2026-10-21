using System.Collections;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shiny.Maui.Controls.Cells;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;
using TvTableSection = Shiny.Maui.Controls.Sections.TableSection;
using TvTableRoot = Shiny.Maui.Controls.Sections.TableRoot;

namespace Shiny.Maui.Controls;

[ContentProperty(nameof(Root))]
public partial class TableView : ContentView
{
    /// <summary>Appended to the table's <see cref="Element.AutomationId"/>, with the visible section's index, to name each rendered section.</summary>
    public const string SectionAutomationIdSuffix = "_Section";

    /// <summary>
    /// Appended to the table's <see cref="Element.AutomationId"/> to name a cell that has no id of its own,
    /// as <c>&lt;id&gt;_Cell_&lt;section&gt;_&lt;row&gt;</c> in visible order. The cell's own parts then follow it.
    /// </summary>
    public const string CellAutomationIdSuffix = "_Cell";

    ScrollView scrollView = default!;
    VerticalStackLayout rootLayout = default!;
    TvTableRoot root = default!;
    bool isRendering;
    bool renderPending;
    // What the pending update has to do: draw everything, or only follow the generated sections and the
    // sections whose generated cells changed. See ScheduleRender.
    bool fullRenderRequested;
    bool generatedSectionsChanged;
    readonly List<TvTableSection> sectionsWithChangedRows = new();
    IDisposable? viewItemsSourceSubscription;
    readonly List<TvTableSection> generatedSections = new();
    // False while an item made no section: the generated sections no longer line up with the items by index,
    // so a collection change cannot be applied to them row by row.
    bool generatedSectionsMatchItems;
    DragSortController? dragSort;
    internal bool SuppressRender { get; set; }

    // What the last render put on screen, so a change of the table's id can rename it without a re-render,
    // and a change to the generated rows can put in or take out just those rows.
    readonly List<RenderedSection> renderedSections = new();
    // The ids this table handed out. A cell whose id is one of these is ours to rename; any other id
    // was the consumer's and is left alone.
    readonly ConditionalWeakTable<CellBase, string> generatedCellIds = new();

    public TableView()
    {
        root = new TvTableRoot();
        root.SetParentTableView(this);
        root.RootChanged += OnRootChanged;

        scrollView = new ScrollView();
        rootLayout = new VerticalStackLayout();
        scrollView.Content = rootLayout;
        Content = scrollView;
        AutomationIds.Track(this, NameRenderedParts);

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(TableView));
    }


    public TvTableRoot Root
    {
        get => root;
        set
        {
            if (root != null)
            {
                root.RootChanged -= OnRootChanged;
                root.SetParentTableView(null);
            }

            root = value ?? new TvTableRoot();
            root.SetParentTableView(this);
            root.RootChanged += OnRootChanged;
            RenderSections();
        }
    }



    public static readonly BindableProperty ShowSectionSeparatorProperty = BindableProperty.Create(
        nameof(ShowSectionSeparator), typeof(bool), typeof(TableView), true,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(TableView), () =>
            {
                ((TableView)b).RenderSections();
            }));

    public static readonly BindableProperty SectionSeparatorHeightProperty = BindableProperty.Create(
        nameof(SectionSeparatorHeight), typeof(double), typeof(TableView), 8d,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(TableView), () =>
            {
                ((TableView)b).RenderSections();
            }));

    public static readonly BindableProperty SectionSeparatorColorProperty = BindableProperty.Create(
        nameof(SectionSeparatorColor), typeof(Color), typeof(TableView), null,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(TableView), () =>
            {
                ((TableView)b).RenderSections();
            }));

    public static readonly BindableProperty SeparatorColorProperty = BindableProperty.Create(
        nameof(SeparatorColor), typeof(Color), typeof(TableView), null,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(TableView), () =>
            {
                ((TableView)b).RenderSections();
            }));

    public static readonly BindableProperty SeparatorHeightProperty = BindableProperty.Create(
        nameof(SeparatorHeight), typeof(double), typeof(TableView), -1d,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(TableView), () =>
            {
                ((TableView)b).RenderSections();
            }));

    public static readonly BindableProperty SeparatorPaddingProperty = BindableProperty.Create(
        nameof(SeparatorPadding), typeof(double), typeof(TableView), -1d,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(TableView), () =>
            {
                ((TableView)b).RenderSections();
            }));

    public static readonly BindableProperty ItemDroppedCommandProperty = BindableProperty.Create(
        nameof(ItemDroppedCommand), typeof(ICommand), typeof(TableView), null);

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable), typeof(TableView), null,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(TableView), () => OnViewItemsSourceChanged(b, o, n)));

    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(TableView), null,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(TableView), () =>
            {
                ((TableView)b).RegenerateTemplatedSections();
            }));

    public static readonly BindableProperty TemplateStartIndexProperty = BindableProperty.Create(
        nameof(TemplateStartIndex), typeof(int), typeof(TableView), 0,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(TableView), () =>
            {
                ((TableView)b).RegenerateTemplatedSections();
            }));

    public static readonly BindableProperty ScrollToTopProperty = BindableProperty.Create(
        nameof(ScrollToTop), typeof(bool), typeof(TableView), false,
        // Deliberately not an async lambda: that compiles to async void, so a fault here
        // becomes an unobserved task exception rather than something a caller can handle.
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(TableView), () =>
        {
            if ((bool)n)
                _ = ((TableView)b).ScrollToTopAndResetAsync();
        }));

    public static readonly BindableProperty ScrollToBottomProperty = BindableProperty.Create(
        nameof(ScrollToBottom), typeof(bool), typeof(TableView), false,
        // Deliberately not an async lambda: that compiles to async void, so a fault here
        // becomes an unobserved task exception rather than something a caller can handle.
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(TableView), () =>
        {
            if ((bool)n)
                _ = ((TableView)b).ScrollToBottomAndResetAsync();
        }));



    public bool ShowSectionSeparator
    {
        get => (bool)GetValue(ShowSectionSeparatorProperty);
        set => SetValue(ShowSectionSeparatorProperty, value);
    }

    public double SectionSeparatorHeight
    {
        get => (double)GetValue(SectionSeparatorHeightProperty);
        set => SetValue(SectionSeparatorHeightProperty, value);
    }

    public Color? SectionSeparatorColor
    {
        get => (Color?)GetValue(SectionSeparatorColorProperty);
        set => SetValue(SectionSeparatorColorProperty, value);
    }

    public Color? SeparatorColor
    {
        get => (Color?)GetValue(SeparatorColorProperty);
        set => SetValue(SeparatorColorProperty, value);
    }

    public double SeparatorHeight
    {
        get => (double)GetValue(SeparatorHeightProperty);
        set => SetValue(SeparatorHeightProperty, value);
    }

    public double SeparatorPadding
    {
        get => (double)GetValue(SeparatorPaddingProperty);
        set => SetValue(SeparatorPaddingProperty, value);
    }

    public ICommand? ItemDroppedCommand
    {
        get => (ICommand?)GetValue(ItemDroppedCommandProperty);
        set => SetValue(ItemDroppedCommandProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public int TemplateStartIndex
    {
        get => (int)GetValue(TemplateStartIndexProperty);
        set => SetValue(TemplateStartIndexProperty, value);
    }

    public bool ScrollToTop
    {
        get => (bool)GetValue(ScrollToTopProperty);
        set => SetValue(ScrollToTopProperty, value);
    }

    public bool ScrollToBottom
    {
        get => (bool)GetValue(ScrollToBottomProperty);
        set => SetValue(ScrollToBottomProperty, value);
    }



    public event EventHandler<ItemDroppedEventArgs>? ItemDropped;
    public event EventHandler? ModelChanged;
    public event EventHandler<CellPropertyChangedEventArgs>? CellPropertyChanged;



    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        if (root != null)
            SetInheritedBindingContext(root, BindingContext);
    }



    void OnRootChanged(object? sender, EventArgs e) => OnSectionChanged(e);

    // A change to a section's generated cells alone arrives with SectionRowsChangedEventArgs and is followed
    // row by row; anything else re-renders.
    void OnSectionChanged(EventArgs e)
    {
        if (e is Sections.SectionRowsChangedEventArgs rows)
            ScheduleRowsUpdate(rows.Section);
        else
            ScheduleRender();
    }

    /// <summary>
    /// Re-renders once the change that asked for it has finished, and once however many asked.
    /// </summary>
    /// <remarks>
    /// <para>⚠️ <b>Never synchronously from a section or root change.</b> Those arrive from inside a
    /// property write — most often a <see cref="Cells.SwitchCell"/> whose <c>On</c> shows or hides a
    /// section. <see cref="RenderSections"/> detaches every cell, and a detached cell loses its
    /// <i>inherited</i> BindingContext, so a bound <c>On</c> fell to <c>false</c> and was written into
    /// the very <c>Switch</c> still mid-<c>IsToggled</c>. MAUI queues a re-entrant write and replays it
    /// when the first returns: the switch went back off, which re-rendered, which queued another off/on
    /// pair — an endless loop that hung the app the moment a section-revealing switch was turned on.
    /// Run afterwards, the detach and re-attach settle before anything is written back.</para>
    /// <para>Coalesced as well: one write that shows or hides several sections is one re-render, not
    /// one each. With no dispatcher (a bare unit test), it renders inline as it always did.</para>
    /// <para>A change to an <c>ItemsSource</c> - a section's, or the table's own - takes the same path
    /// but puts in, takes out or moves only the rows it names (<see cref="ScheduleRowsUpdate"/>,
    /// <see cref="ScheduleSectionsUpdate"/>). Anything else pending in the same pass still re-renders it
    /// all.</para>
    /// </remarks>
    void ScheduleRender()
    {
        fullRenderRequested = true;
        DispatchUpdate();
    }

    /// <summary>
    /// Brings only <paramref name="section"/>'s rows up to date - the cells its <c>ItemsSource</c> added,
    /// removed or moved - on the same schedule as <see cref="ScheduleRender"/>. Every other row stays where
    /// it is, and so do the cells in it.
    /// </summary>
    void ScheduleRowsUpdate(TvTableSection section)
    {
        if (!sectionsWithChangedRows.Contains(section))
            sectionsWithChangedRows.Add(section);
        DispatchUpdate();
    }

    /// <summary>
    /// Puts in, takes out or moves only the sections the table's own <c>ItemsSource</c> added, removed or
    /// moved, on the same schedule as <see cref="ScheduleRender"/>.
    /// </summary>
    void ScheduleSectionsUpdate()
    {
        generatedSectionsChanged = true;
        DispatchUpdate();
    }

    void DispatchUpdate()
    {
        var dispatcher = this.Dispatcher;
        if (dispatcher is null)
        {
            ApplyUpdate();
            return;
        }

        if (renderPending)
            return;

        renderPending = true;
        dispatcher.Dispatch(() =>
        {
            renderPending = false;
            ApplyUpdate();
        });
    }

    void ApplyUpdate()
    {
        var renderAll = fullRenderRequested;
        var sections = generatedSectionsChanged;
        var rows = new List<TvTableSection>(sectionsWithChangedRows);
        fullRenderRequested = false;
        generatedSectionsChanged = false;
        sectionsWithChangedRows.Clear();

        if (renderAll || !TryUpdateInPlace(sections, rows))
            RenderSections();

        ModelChanged?.Invoke(this, EventArgs.Empty);
    }

    // ---------- Drag sort plumbing (see DragSortController) ----------

    internal DragSortController DragSort => dragSort ??= new DragSortController(this);

    internal double ScrollOffsetY => scrollView.ScrollY;

    internal double ViewportHeight => scrollView.Height;

    internal double ContentHeight => rootLayout.Height;

    internal VisualElement ScrollContent => rootLayout;

    internal void ScrollToY(double y) => _ = scrollView.ScrollToAsync(0, y, false);

    // Note: suppressing the scroller during a drag via ScrollView.Orientation = Neither looks
    // like the obvious lever, but on iOS toggling Orientation snaps the offset back to the top,
    // which drops the drag geometry on the floor the instant it starts. Locking the scroller is
    // left to the DragSortRow platform hooks, which do it natively without moving the content.


    internal void RenderSections()
    {
        if (isRendering || SuppressRender)
            return;

        // Rebuilding the tree invalidates every row the drag is tracking.
        dragSort?.Abort();

        isRendering = true;

        try
        {
            // Detach all cells from their current parent views before clearing.
            // Android requires native views to be removed from their parent ViewGroup
            // before they can be re-added to a new one.
            //
            // Every cell, not only the visible ones. A cell hidden by a binding was rendered while it was
            // visible, so it is still a child of the outgoing section, and the Clear below orphans that
            // section - which clears the cell's inherited BindingContext with it, its bound IsVisible
            // falls back to true, and this same render adds it to the new section while Android still
            // has its view in the old one: "The specified child already has a parent", which crashed a
            // settings page the first time it re-rendered on screen.
            //
            // And the section's BindingContext is handed straight back. A detached cell has no parent
            // to inherit from, so without it every binding on the cell reads its default until the
            // cell is re-attached - a hidden row reads as visible to GetVisibleCells below, and a row
            // the render leaves out keeps no context at all until the next one.
            foreach (var section in GetAllSections())
                DetachRenderedParts(section);

            rootLayout.Children.Clear();
            renderedSections.Clear();

            // Only the visible sections, and the filter has to happen here rather than in the loop.
            // A hidden section renders as a zero-height placeholder, so counting it would put a
            // separator on both sides of nothing — two rules together where one section is hidden
            // between two visible ones, and a rule under the last visible section when the hidden
            // ones are trailing. Both read as a rendering fault rather than as a hidden section.
            var sections = GetVisibleSections();

            for (var i = 0; i < sections.Count; i++)
            {
                var (sectionView, firstRow, cells) = SectionRenderer.Render(sections[i], this);
                rootLayout.Children.Add(sectionView);
                renderedSections.Add(new RenderedSection(sections[i], sectionView, firstRow, cells));

                // Section separator
                if (ShowSectionSeparator && i < sections.Count - 1)
                    rootLayout.Children.Add(CreateSectionSeparator());
            }
        }
        finally
        {
            isRendering = false;
        }
        NameRenderedParts();
    }

    // Takes a section's cells, HeaderView and FooterView out of whatever layout drew them - see RenderSections.
    void DetachRenderedParts(TvTableSection section)
    {
        foreach (var cell in section.GetAllCells())
        {
            (cell.Parent as Layout)?.Remove(cell);
            SetInheritedBindingContext(cell, section.BindingContext);
        }

        // The same goes for a section's HeaderView and FooterView: SectionRenderer adds the
        // consumer's instance to a brand-new section layout on every render, so left in the
        // outgoing one it crashes the next render — Android's "The specified child already has
        // a parent", and a COMException out of UIElementCollection.Add on Windows.
        if (section.HeaderView is { Parent: Layout headerParent } header)
            headerParent.Remove(header);
        if (section.FooterView is { Parent: Layout footerParent } footer)
            footerParent.Remove(footer);
    }

    BoxView CreateSectionSeparator()
    {
        var gap = new BoxView { HeightRequest = SectionSeparatorHeight };

        // Bound, not assigned: a literal captured here is read once, at render time, so
        // it survives an appearance flip and leaves a near-black band sitting between the
        // sections of a light table.
        if (SectionSeparatorColor is { } explicitColor)
            gap.Color = explicitColor;
        else if (BackgroundColor is { } ownBackground && ownBackground != Colors.Transparent)
            gap.Color = ownBackground;
        else
            gap.SetDynamicResource(BoxView.ColorProperty, ShinyThemeKeys.Color.Background);

        return gap;
    }

    /// <summary>
    /// Follows a change to the generated sections, or to the generated cells of <paramref name="rowsChanged"/>,
    /// by putting in, taking out or moving only those rows. The rest of the table - its other sections, the
    /// cells already drawn and the separators between them - stays as it is.
    /// </summary>
    /// <returns>False when the screen is not what the last render left, so the caller renders it all.</returns>
    bool TryUpdateInPlace(bool sectionsChanged, IReadOnlyList<TvTableSection> rowsChanged)
    {
        // Skipped as RenderSections skips them: mid-render, or while a drag reorders cells (it renders
        // itself once it is done).
        if (isRendering || SuppressRender)
            return true;

        isRendering = true;
        bool updated;
        try
        {
            updated = !sectionsChanged || UpdateRenderedSections();
            for (var i = 0; updated && i < rowsChanged.Count; i++)
                updated = UpdateRenderedCells(rowsChanged[i]);
        }
        finally
        {
            isRendering = false;
        }

        // Renumbered, since an insert or a move shifts every row after it.
        if (updated)
            NameRenderedParts();
        return updated;
    }

    bool UpdateRenderedSections()
    {
        var shown = new List<TvTableSection>(renderedSections.Count);
        foreach (var rendered in renderedSections)
            shown.Add(rendered.Section);

        Func<View>? separator = ShowSectionSeparator ? CreateSectionSeparator : null;

        // A section that only moves keeps the view it was drawn into. The ones still here at the end left.
        var taken = new Dictionary<TvTableSection, RenderedSection>();
        var updated = TableRows.Reconcile(shown, GetVisibleSections(),
            remove: index =>
            {
                var rendered = renderedSections[index];
                if (!TableRows.RemoveAt(rootLayout, 0, index, renderedSections.Count, rendered.View, separator != null))
                    return false;

                dragSort?.Abort();
                renderedSections.RemoveAt(index);
                taken[rendered.Section] = rendered;
                return true;
            },
            insert: (index, section) =>
            {
                if (!taken.Remove(section, out var rendered))
                {
                    // Nothing of it may still sit in an old layout: Android and WinUI both end the app over a
                    // view added to a second parent.
                    DetachRenderedParts(section);
                    var (view, firstRow, cells) = SectionRenderer.Render(section, this);
                    rendered = new RenderedSection(section, view, firstRow, cells);
                }

                if (!TableRows.Insert(rootLayout, 0, index, renderedSections.Count, rendered.View, separator))
                    return false;

                dragSort?.Abort();
                renderedSections.Insert(index, rendered);
                return true;
            });

        // Their cells and HeaderView/FooterView are freed, as a full render frees them, so nothing is left
        // inside a layout that is no longer on screen.
        foreach (var section in taken.Keys)
            DetachRenderedParts(section);

        return updated;
    }

    bool UpdateRenderedCells(TvTableSection section)
    {
        var index = renderedSections.FindIndex(x => ReferenceEquals(x.Section, section));
        if (index < 0)
        {
            // A hidden section draws no rows, and one that left the table has none here. A visible
            // section of this table that the last render did not draw means the screen is out of step.
            return !section.IsVisible || !GetAllSections().Contains(section);
        }

        var (_, view, firstRow, cells) = renderedSections[index];
        if (!section.IsVisible || view is not Layout layout)
            return false;

        return TableRows.Reconcile(cells, section.GetVisibleCells(),
            remove: row =>
            {
                var cell = cells[row];
                if (!TableRows.RemoveAt(layout, firstRow, row, cells.Count, RowView(cell), separated: true))
                    return false;

                dragSort?.Abort();

                // Out of its DragSortRow too, and with the section's context to read while it is detached,
                // as RenderSections leaves it.
                (cell.Parent as Layout)?.Remove(cell);
                SetInheritedBindingContext(cell, section.BindingContext);
                return true;
            },
            insert: (row, cell) =>
            {
                dragSort?.Abort();

                // Never into a second parent: Android and WinUI both end the app over a view that is
                // still a child of another one.
                (cell.Parent as Layout)?.Remove(cell);
                var rowView = SectionRenderer.CreateRow(section, cell, this);
                return TableRows.Insert(layout, firstRow, row, cells.Count, rowView, () => SectionRenderer.CreateSeparator(this));
            });
    }

    // The view a drawn cell takes up in its section's layout.
    static View RowView(CellBase cell) => cell.Parent is DragSortRow row ? row : cell;

    void NameRenderedParts()
    {
        for (var s = 0; s < renderedSections.Count; s++)
        {
            var (_, view, _, cells) = renderedSections[s];
            AutomationIds.Set(view, AutomationIds.For(this, SectionAutomationIdSuffix, s));

            var cellBase = AutomationIds.For(this, CellAutomationIdSuffix, s);
            for (var c = 0; c < cells.Count; c++)
            {
                var cell = cells[c];
                var hasGenerated = generatedCellIds.TryGetValue(cell, out var generated);
                if (!String.IsNullOrEmpty(cell.AutomationId) && (!hasGenerated || cell.AutomationId != generated))
                    continue;

                var id = cellBase is null ? null : $"{cellBase}_{c}";
                AutomationIds.Set(cell, id);
                if (id is null)
                    generatedCellIds.Remove(cell);
                else
                    generatedCellIds.AddOrUpdate(cell, id);
            }
        }
    }

    /// <summary>
    /// The sections that will actually draw something, in order. What the separator logic counts.
    /// </summary>
    internal IReadOnlyList<TvTableSection> GetVisibleSections()
    {
        var all = GetAllSections();

        var visible = new List<TvTableSection>(all.Count);
        foreach (var section in all)
        {
            if (section.IsVisible)
                visible.Add(section);
        }
        return visible;
    }

    internal bool IsGeneratedSection(TvTableSection section) => generatedSections.Contains(section);

    internal IReadOnlyList<TvTableSection> GetAllSections()
    {
        var staticSections = root.Sections;

        if (generatedSections.Count == 0)
            return staticSections;

        var result = new List<TvTableSection>();
        var insertIndex = Math.Min(TemplateStartIndex, staticSections.Count);

        for (var i = 0; i < insertIndex; i++)
            result.Add(staticSections[i]);

        result.AddRange(generatedSections);

        for (var i = insertIndex; i < staticSections.Count; i++)
            result.Add(staticSections[i]);

        return result;
    }



    static void OnViewItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var tv = (TableView)bindable;

        // Weak: the source is usually a view model's collection, which outlives the page.
        tv.viewItemsSourceSubscription?.Dispose();
        tv.viewItemsSourceSubscription = WeakEventSubscription.CollectionChanged(newValue, tv.OnViewItemsSourceCollectionChanged);

        tv.RegenerateTemplatedSections();
    }

    void OnViewItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Sections for the items that came in, and out of the way for the ones that left - not a new section
        // for every item on every change. A Reset, or a change that does not line up with the sections, still
        // builds them all again.
        if (ItemTemplate is { } template && generatedSectionsMatchItems
            && TableRows.TryApply(generatedSections, e, item => CreateSection(template, item), ReleaseSection)
            && (ItemsSource is not ICollection source || source.Count == generatedSections.Count))
            ScheduleSectionsUpdate();
        else
            RegenerateTemplatedSections();
    }

    void RegenerateTemplatedSections()
    {
        foreach (var section in generatedSections)
            ReleaseSection(section);

        generatedSections.Clear();
        generatedSectionsMatchItems = false;

        var template = ItemTemplate;
        if (ItemsSource == null || template == null)
        {
            RenderSections();
            return;
        }

        generatedSectionsMatchItems = true;
        foreach (var item in ItemsSource)
        {
            if (CreateSection(template, item) is { } section)
                generatedSections.Add(section);
            else
                generatedSectionsMatchItems = false;
        }

        RenderSections();
    }

    TvTableSection? CreateSection(DataTemplate template, object? item)
    {
        if (template is DataTemplateSelector selector)
            template = selector.SelectTemplate(item, null);

        if (template.CreateContent() is not TvTableSection section)
            return null;

        section.BindingContext = item;
        section.ParentTableView = this;
        section.SectionChanged += OnGeneratedSectionChanged;
        return section;
    }

    void ReleaseSection(TvTableSection section)
    {
        section.SectionChanged -= OnGeneratedSectionChanged;
        section.ParentTableView = null;
    }

    void OnGeneratedSectionChanged(object? sender, EventArgs e) => OnSectionChanged(e);



    internal void RaiseItemDropped(TvTableSection section, CellBase cell, int fromIndex, int toIndex)
    {
        var args = new ItemDroppedEventArgs(section, cell, fromIndex, toIndex);
        ItemDropped?.Invoke(this, args);
        if (ItemDroppedCommand?.CanExecute(args) == true)
            ItemDroppedCommand.Execute(args);
    }



    internal void RaiseCellPropertyChanged(TvTableSection section, CellBase cell, string propertyName)
    {
        CellPropertyChanged?.Invoke(this, new CellPropertyChangedEventArgs(section, cell, propertyName));
    }



    async Task ScrollToTopAndResetAsync()
    {
        await this.ScrollToTopAsync();
        this.ScrollToTop = false;
    }


    async Task ScrollToBottomAndResetAsync()
    {
        await this.ScrollToBottomAsync();
        this.ScrollToBottom = false;
    }


    public Task ScrollToTopAsync(bool animated = true)
        => scrollView.ScrollToAsync(0, 0, animated);

    public Task ScrollToBottomAsync(bool animated = true)
        => scrollView.ScrollToAsync(0, rootLayout.Height, animated);

    public double VisibleContentHeight => scrollView.ContentSize.Height;


    // One section on screen: the view it was drawn into, the child index its first row sits at, and the
    // cells it shows as rows, in order.
    sealed record RenderedSection(TvTableSection Section, View View, int FirstRow, List<CellBase> Cells);
}

public class ItemDroppedEventArgs : EventArgs
{
    public TvTableSection Section { get; }
    public CellBase Cell { get; }

    /// <summary>Position the cell was dragged from, among the section's rendered rows.</summary>
    public int FromIndex { get; }

    /// <summary>Position the cell was dropped at, among the section's rendered rows.</summary>
    public int ToIndex { get; }

    /// <summary>
    /// The moved cell's binding context. This is the item to reorder when the section's rows
    /// come from ItemsSource/ItemTemplate - the control cannot reorder a templated section for
    /// you, because the order lives in your collection.
    /// </summary>
    public object? Item => Cell.BindingContext;

    public ItemDroppedEventArgs(TvTableSection section, CellBase cell, int fromIndex, int toIndex)
    {
        Section = section;
        Cell = cell;
        FromIndex = fromIndex;
        ToIndex = toIndex;
    }
}

public class CellPropertyChangedEventArgs : EventArgs
{
    public TvTableSection Section { get; }
    public CellBase Cell { get; }
    public string PropertyName { get; }

    public CellPropertyChangedEventArgs(TvTableSection section, CellBase cell, string propertyName)
    {
        Section = section;
        Cell = cell;
        PropertyName = propertyName;
    }
}