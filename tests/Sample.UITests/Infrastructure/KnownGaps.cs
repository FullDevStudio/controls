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
    /// UPSTREAM maui-labs bug (Microsoft.Maui.Platforms.Linux.Gtk4 0.1.0-preview.12.26479.1): a double free in
    /// GtkViewHandler.ApplyTransform, which runs for any view with Scale/Rotation != 1 at a non-zero position:
    ///   var point = Graphene.Point.Alloc(); point.Init(x, y); transform = transform.Translate(point); point.Free();
    /// Point.Alloc returns a Gir.Core OwnedHandle, so the explicit Free() is followed by the handle's finalizer
    /// freeing the same pointer. Crash dump (createdump, dotnet-dump clrstack -f on the .NET Finalizer thread):
    ///   GObject BoxedFree &lt;- Graphene.Internal.PointOwnedHandle.ReleaseHandle &lt;- SafeHandle.Finalize &lt;- GC.RunFinalizers
    /// glibc then aborts ("double free or corruption (!prev)", "free(): double free detected in tcache 2", ...)
    /// whenever the GC gets to it, so it hits pages at random: the Badge page (every badge scales in, and the
    /// pulse loops) dies ~2-19s after opening with no input; Expander and Dialogs died the same way under test.
    /// The fix is upstream: drop point.Free(). Nothing in Shiny's src is at fault.
    /// </summary>
    public const string LinuxBadgeHeapCorruption =
        "BUG: upstream maui-labs: GtkViewHandler.ApplyTransform double-frees a Graphene.Point (Free() then the OwnedHandle finalizer)";

    /// <summary>
    /// GTK4 head: the UI thread spins at 100% CPU inside a native GTK call and never returns (the agent
    /// stops answering; the watchdog kills the app). dotnet-stack, taken twice ~1 min apart during the hang:
    ///   [native] ← Gtk4.Handlers.EntryHandler.MapText (PlatformView.SetText) ← BindableObject.SetValue
    ///   ← InputView.set_Text ← AutoCompleteEntry.OnItemTapped ← TapGestureRecognizer.SendTapped
    ///   ← DevFlow HandleTap ← GtkDispatcher ← Gio.Application.Run
    /// MapText itself is a plain compare-and-SetText (decompiled), so the loop is inside gtk_editable_set_text.
    /// Reached from the suggestion tap; tests that never tap also wedge a few seconds after typing once
    /// the dropdown has shown (5/6 AutoComplete tests; Typing_Filters_Local_Items passes).
    /// </summary>
    public const string LinuxAutoCompleteGtkWedge =
        "BUG: GTK4 head wedges (UI thread spinning in native gtk_editable_set_text) once the AutoComplete dropdown is used";

    /// <summary>
    /// GTK4 head: the second ShellContent of a multi-content FlyoutItem can't be reached at all. Worse than
    /// AppKit: there the route changes and the page doesn't; here Shell never even changes the route. Seen in
    /// the targeted run for every ChipGroup test ("Shell did not reach '//chipgroup' within 30s (current route:
    /// '//tagentry')") and both TreeDataGrid tests ("... '//treedatagrid' ... (current route: '//datagrid')"),
    /// including after navigating to //home first. ChipGroupPage still gets created (the page-type wait passes
    /// when the route check is skipped), but TagEntry's content stays on screen.
    /// </summary>
    public const string LinuxSiblingShellContentUnreachable =
        "BUG: GTK4 Shell never navigates to the second ShellContent of a FlyoutItem - the route stays on the first";

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
        [(UIPlatform.Linux, "badge")] = LinuxBadgeHeapCorruption,
        [(UIPlatform.Linux, "chipgroup")] = LinuxSiblingShellContentUnreachable,
        [(UIPlatform.Linux, "treedatagrid")] = LinuxSiblingShellContentUnreachable,
        [(UIPlatform.Linux, "passwordstrength")] = LinuxSiblingShellContentUnreachable,
    };

    /// <summary>Why <paramref name="route"/> can't build on the current head, or null.</summary>
    public static string? ForRoute(string route)
        => pages.GetValueOrDefault((UITestSettings.Platform, route));
}
