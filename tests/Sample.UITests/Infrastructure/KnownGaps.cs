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
    /// The WinUI agent reads the UI by capturing a settled tree. A page that animates continuously never
    /// settles, so every query, tree dump and property read comes back
    /// "The UI kept changing while DevFlow was reading it" — and <see cref="MauiDevFlowDriver.Query"/>
    /// turns that into an empty result, so it looks exactly like a page that never opened.
    ///
    /// Measured against the running app (agent 0.1.0-preview.12, CLI matched): a static page answers
    /// every time (autocomplete 5/5, slider 5/5); marquee, motionicons, keyframe, badge and skeleton
    /// answer 0/5 and raise the error 5/5. `ui tree` fails the same way, and `set-property` cannot be
    /// used to stop the animation first because it has to resolve the element through a query.
    ///
    /// Nothing is wrong with these controls on Windows — they cannot be *observed* there.
    /// </summary>
    public const string WinUiAnimationBlocksCapture =
        "DevFlow cannot read the UI on WinUI while the page animates continuously - every capture returns " +
        "'The UI kept changing while DevFlow was reading it', so the page looks absent";

    static readonly Dictionary<(UIPlatform, string), string> pages = new()
    {
        [(UIPlatform.Windows, "marquee")] = WinUiAnimationBlocksCapture,
        [(UIPlatform.Windows, "motionicons")] = WinUiAnimationBlocksCapture,
        [(UIPlatform.Windows, "keyframe")] = WinUiAnimationBlocksCapture,
        [(UIPlatform.Windows, "badge")] = WinUiAnimationBlocksCapture,
        [(UIPlatform.Windows, "skeleton")] = WinUiAnimationBlocksCapture,
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
