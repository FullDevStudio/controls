namespace Sample.UITests;

/// <summary>
/// Pages a head cannot build because a control has no handler there — product gaps, not test
/// failures. Listed once so the route sweep and the per-control tests skip with the same reason,
/// and a gap that gets closed shows up as one line to delete.
/// </summary>
public static class KnownGaps
{
    public const string SkiaNoAppKitHandler =
        "SkiaSharp's SKCanvasView has no AppKit handler (throws NotImplementedException) — the page can't build";

    public const string CarouselGalleryNoHandler =
        "CarouselGallery has handlers for iOS/Android/Mac Catalyst/Windows only — the page can't build";

    public const string StaggeredGridNoHandler =
        "StaggeredGrid has handlers for iOS/Android/Mac Catalyst/Windows only — the page can't build";

    public const string VirtualizedGridNoHandler =
        "VirtualizedGrid has handlers for iOS/Android/Windows only — the page can't build";

    /// <summary>
    /// The page builds and works, but the route sweep can't reach it: it is the second ShellContent of a
    /// multi-content FlyoutItem, and the sweep arrives from its sibling. Reproduced with devflow on a fresh
    /// app: //datagrid then //treedatagrid moves Shell's route to treedatagrid but DataGridPage stays on screen
    /// and TreeDataGridPage is never created; arriving from another FlyoutItem presents it fine. The
    /// per-control tests reach these pages from another section. Only the later siblings are listed, so the
    /// first of each group is never reached from a sibling in the sweep.
    /// </summary>
    public const string AppKitSiblingShellContentSwitch =
        "BUG: AppKit Shell never presents a page reached straight from its sibling ShellContent in the same FlyoutItem (route changes, page doesn't)";

    /// <summary>
    /// Behaviour gaps (the page builds, one interaction can't work) for per-test SkipOn reasons.
    /// Verified with devflow on the macOS head: change a layout property, positions stay put; `ui resize`
    /// the window and everything snaps to the correct place at once.
    /// </summary>
    public const string AppKitNoRelayoutOnChange =
        "BUG: AppKit runs no layout pass when an already-realized layout's properties or size change - it only re-lays out on a window resize";

    /// <summary>
    /// GTK4 head (maui-labs preview.13 and main): once the AutoComplete dropdown is open, a text change plus
    /// fast polling (the tests' WaitForSuggestions) can leave the main thread doing nothing but GDK frame-clock
    /// cycles. The only managed frame is the backend's root layout tick (LayoutHandler.StartRootLayoutTick via a
    /// Gtk tick callback). The frame clock's source (GDK_PRIORITY_REDRAW) outranks the idle sources the MAUI
    /// dispatcher uses, so every dispatched callback - DevFlow's included - starves; that is the "hang".
    /// Instrumentation shows no managed layout loop (zero layout passes / invalidations during the wedge), idle
    /// CPU is 0-4% before the trigger, and frames are normally paced until then. Why the frame clock stops
    /// pacing is inside GTK and not yet known (needs GTK debug symbols). Not AutoCompleteEntry's logic: a plain
    /// DevFlow fill with the dropdown open triggers it too.
    /// </summary>
    public const string LinuxAutoCompleteGtkWedge =
        "BUG: GTK4 head - the GDK frame clock starves the MAUI dispatcher once the AutoComplete dropdown is open and the text changes";

    /// <summary>
    /// GTK4 head (maui-labs preview.13): content that was invisible at first layout is never arranged once
    /// it becomes visible. After Expander.IsExpanded=True the Expander keeps its header-only bounds (58px)
    /// and the revealed label stays at Width/Height -1, and unlike AppKit a window resize does not fix it.
    /// Shiny's Expander only does standard MAUI (contentClip.IsVisible = true, animate HeightRequest
    /// 0 → measured, then -1). In the backend, GtkViewHandler.MapVisibility only calls SetVisible and
    /// GtkLayoutPanel skips invisible children, so the likely cause is there - not yet proven upstream.
    /// </summary>
    public const string LinuxRevealedContentNeverArranged =
        "BUG: GTK4 head never arranges content that was invisible at first layout once it becomes visible (Expander body stays unlaid-out)";

    static readonly Dictionary<(UIPlatform, string), string> pages = new()
    {
        [(UIPlatform.MacOS, "floorplan")] = SkiaNoAppKitHandler,
        [(UIPlatform.MacOS, "seatingchart")] = SkiaNoAppKitHandler,
        [(UIPlatform.MacOS, "spreadsheet")] = SkiaNoAppKitHandler,
        [(UIPlatform.MacOS, "documentviewer")] = SkiaNoAppKitHandler,
        [(UIPlatform.MacOS, "documenteditor")] = SkiaNoAppKitHandler,
        [(UIPlatform.MacOS, "slideviewer")] = SkiaNoAppKitHandler,
        [(UIPlatform.MacOS, "slideeditor")] = SkiaNoAppKitHandler,
        [(UIPlatform.MacOS, "notebook")] = SkiaNoAppKitHandler,
        [(UIPlatform.MacOS, "carouselgallery")] = CarouselGalleryNoHandler,
        [(UIPlatform.Linux, "carouselgallery")] = CarouselGalleryNoHandler,
        [(UIPlatform.MacOS, "staggeredgrid")] = StaggeredGridNoHandler,
        [(UIPlatform.Linux, "staggeredgrid")] = StaggeredGridNoHandler,
        [(UIPlatform.MacOS, "virtualizedgrid")] = VirtualizedGridNoHandler,
        [(UIPlatform.MacOS, "treedatagrid")] = AppKitSiblingShellContentSwitch,
        [(UIPlatform.MacOS, "passwordstrength")] = AppKitSiblingShellContentSwitch,
        [(UIPlatform.MacOS, "musicbrowse")] = AppKitSiblingShellContentSwitch,
        [(UIPlatform.MacOS, "musiclibrary")] = AppKitSiblingShellContentSwitch,
        [(UIPlatform.Linux, "virtualizedgrid")] = VirtualizedGridNoHandler,
    };

    /// <summary>Why <paramref name="route"/> can't build on the current head, or null.</summary>
    public static string? ForRoute(string route)
        => pages.GetValueOrDefault((UITestSettings.Platform, route));
}
