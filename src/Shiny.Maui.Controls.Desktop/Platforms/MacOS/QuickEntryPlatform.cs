using Shiny.Maui.Controls.QuickEntry;
using AppKit;
using CoreGraphics;
using Foundation;
using Shiny.Maui.Controls.Desktop.TrayIcon;

namespace Shiny.Maui.Controls.Desktop.QuickEntry;

/// <summary>
/// AppKit backing for the quick entry popup.
/// </summary>
/// <remarks>
/// The window keeps <see cref="NSWindowStyle.Titled"/> even though it looks borderless. A truly
/// borderless <c>NSWindow</c> answers NO to <c>canBecomeKeyWindow</c> unless it is an
/// <c>NSPanel</c>, and MAUI creates a plain <c>NSWindow</c> we cannot subclass — so a borderless
/// style mask would give us a popup that can never receive a keystroke. Keeping the title bar and
/// then making it transparent, hiding the title, extending content under it and removing the
/// traffic lights gets the same look with a window that still takes focus.
/// </remarks>
static class QuickEntryPlatform
{
    sealed class Hooks
    {
        public IDesktopQuickEntryHost Host = null!;
        public NSObject? KeyMonitor;
        public NSObject? ResignKeyObserver;
    }

    static readonly Dictionary<IntPtr, Hooks> Registry = new();

    public static bool IsSupported => true;

    public static void BeginInvokeOnMainThread(Action action) => MacMainThread.Invoke(action);

    public static void Initialize(IDesktopQuickEntryHost host, object platformWindow, QuickEntryOptions options)
    {
        if (platformWindow is not NSWindow window)
            return;

        MacMainThread.Invoke(() =>
        {
            window.StyleMask |= NSWindowStyle.Titled | NSWindowStyle.FullSizeContentView;
            window.StyleMask &= ~(NSWindowStyle.Resizable | NSWindowStyle.Miniaturizable);
            window.TitlebarAppearsTransparent = true;
            window.TitleVisibility = NSWindowTitleVisibility.Hidden;
            window.MovableByWindowBackground = false;

            foreach (var button in new[] { NSWindowButton.CloseButton, NSWindowButton.MiniaturizeButton, NSWindowButton.ZoomButton })
            {
                var b = window.StandardWindowButton(button);
                if (b != null)
                    b.Hidden = true;
            }

            window.IsOpaque = false;
            window.BackgroundColor = NSColor.Clear;
            window.HasShadow = true;
            window.Level = NSWindowLevel.Floating;
            window.HidesOnDeactivate = false;

            var behavior = NSWindowCollectionBehavior.Transient | NSWindowCollectionBehavior.IgnoresCycle;
            if (options.JoinAllSpaces)
                behavior |= NSWindowCollectionBehavior.CanJoinAllSpaces | NSWindowCollectionBehavior.FullScreenAuxiliary;
            window.CollectionBehavior = behavior;

            if (!options.ShowInTaskbar)
                window.SetExcludedFromWindowsMenu(true);

            if (window.ContentView is { } content)
            {
                content.WantsLayer = true;
                if (content.Layer != null)
                    content.Layer.BackgroundColor = NSColor.Clear.CGColor;
            }

            var hooks = new Hooks { Host = host };

            // Not `window.DidResignKey += …`: MAUI's macOS WindowHandler installs its own
            // MacOSWindowDelegate, and the .NET bindings refuse to let an event and an explicit
            // delegate coexist — the event accessor throws "Event registration is overwriting
            // existing delegate" at runtime. Observing the notification sidesteps the delegate
            // entirely and cannot collide with whatever the handler does next.
            hooks.ResignKeyObserver = NSNotificationCenter.DefaultCenter.AddObserver(
                NSWindow.DidResignKeyNotification,
                _ => host.NotifyDeactivated(),
                window
            );
            Registry[window.Handle] = hooks;

            window.AlphaValue = 0f;
        });
    }

    public static void Show(object platformWindow, QuickEntryOptions options, double width, double height)
    {
        if (platformWindow is not NSWindow window)
            return;

        MacMainThread.Invoke(() =>
        {
            window.SetFrame(FrameFor(window, options, width, height), true);
            window.AlphaValue = 1f;

            if (options.ActivateOnShow)
            {
                NSApplication.SharedApplication.ActivateIgnoringOtherApps(true);
                window.MakeKeyAndOrderFront(null);

                // Activation is asynchronous, and as it settles AppKit restores whichever window was
                // key for this app last — which is the main window, not this one. Without the second
                // assert on the next run-loop turn the popup is visible but every keystroke goes to
                // whatever the user was actually in.
                MacMainThread.Post(() =>
                {
                    window.MakeKeyAndOrderFront(null);
                    window.OrderFrontRegardless();
                });
            }
            else
            {
                window.OrderFrontRegardless();
            }

            InstallKeyMonitor(window);
        });
    }

    public static void Hide(object platformWindow)
    {
        if (platformWindow is not NSWindow window)
            return;

        MacMainThread.Invoke(() =>
        {
            RemoveKeyMonitor(window);
            window.AlphaValue = 0f;
            window.OrderOut(null);
        });
    }

    public static void Resize(object platformWindow, QuickEntryOptions options, double width, double height)
    {
        if (platformWindow is not NSWindow window)
            return;

        MacMainThread.Invoke(() => window.SetFrame(FrameFor(window, options, width, height), true, true));
    }

    public static void Teardown(object platformWindow)
    {
        if (platformWindow is not NSWindow window)
            return;

        MacMainThread.Invoke(() =>
        {
            RemoveKeyMonitor(window);
            if (Registry.Remove(window.Handle, out var hooks) && hooks.ResignKeyObserver != null)
                NSNotificationCenter.DefaultCenter.RemoveObserver(hooks.ResignKeyObserver);
        });
    }

    /// <summary>Strips the focus ring, border and opaque fill AppKit gives an <c>NSTextField</c>, so a BorderlessEntry sits flush on its card or table row.</summary>
    public static void PolishEntry(object? platformView)
    {
        if (platformView is not NSTextField field)
            return;

        MacMainThread.Invoke(() =>
        {
            field.Bordered = false;
            field.Bezeled = false;
            field.DrawsBackground = false;
            field.BackgroundColor = NSColor.Clear;
            field.FocusRingType = NSFocusRingType.None;

            // Without its bezel an NSTextField lays out like a label and wraps - a placeholder wider
            // than the field broke onto a second line inside a table row. One line that scrolls
            // sideways under the caret is what an entry is.
            field.UsesSingleLineMode = true;
            if (field.Cell is { } cell)
            {
                cell.Wraps = false;
                cell.Scrollable = true;
            }
        });
    }

    // -------------------------------------------------------------------------------------
    // Keyboard
    // -------------------------------------------------------------------------------------

    const ushort KeyReturn = 36;
    const ushort KeyTab = 48;
    const ushort KeyEscape = 53;
    const ushort KeyKeypadEnter = 76;
    const ushort KeyArrowUp = 126;
    const ushort KeyArrowDown = 125;

    static void InstallKeyMonitor(NSWindow window)
    {
        if (!Registry.TryGetValue(window.Handle, out var hooks) || hooks.KeyMonitor != null)
            return;

        // A *local* monitor only sees events already routed to this process, which is exactly the
        // scope wanted: it needs no accessibility permission, and returning null swallows the key
        // so AppKit does not also beep at an unhandled Escape.
        hooks.KeyMonitor = NSEvent.AddLocalMonitorForEventsMatchingMask(NSEventMask.KeyDown, ev =>
        {
            if (ev.Window != window)
                return ev;

            var key = ev.KeyCode switch
            {
                KeyEscape => (QuickEntryKey?)QuickEntryKey.Escape,
                KeyReturn or KeyKeypadEnter => QuickEntryKey.Enter,
                KeyArrowUp => QuickEntryKey.ArrowUp,
                KeyArrowDown => QuickEntryKey.ArrowDown,
                KeyTab => QuickEntryKey.Tab,
                _ => null
            };

            if (key == null)
                return ev;

            // Enter is left to flow through as well as being reported: the prompt's own Completed
            // handler is what commits the text, and swallowing it here would break that.
            var handled = hooks.Host.NotifyKey(key.Value);
            return handled && key != QuickEntryKey.Enter ? null! : ev;
        });
    }

    static void RemoveKeyMonitor(NSWindow window)
    {
        if (!Registry.TryGetValue(window.Handle, out var hooks) || hooks.KeyMonitor == null)
            return;

        NSEvent.RemoveMonitor(hooks.KeyMonitor);
        hooks.KeyMonitor = null;
    }

    // -------------------------------------------------------------------------------------
    // Placement. AppKit screen coordinates have a bottom-left origin, so every vertical value
    // below is flipped out of the top-left space the options are expressed in.
    // -------------------------------------------------------------------------------------

    /// <summary>
    /// The window frame for a given <em>content</em> height.
    /// </summary>
    /// <remarks>
    /// The window keeps its title bar (see the note on this class — a borderless NSWindow can never
    /// become key), and MAUI lays its content out below that bar rather than under it, whatever the
    /// FullSizeContentView style bit says. So a window made exactly as tall as its content loses the
    /// last title-bar's-worth of it off the bottom — which showed up as a popup that clipped its own
    /// final suggestion row. The frame is grown by that inset instead. Cocoa's origin is bottom-left,
    /// so adding to Height extends the window upwards and the content lands exactly where the
    /// placement asked; the strip above it is transparent and never seen.
    /// </remarks>
    static CGRect FrameFor(NSWindow window, QuickEntryOptions options, double width, double height)
    {
        var frame = ComputeFrame(options, width, height);
        var inset = TitleBarInset(window);
        return new CGRect(frame.X, frame.Y, frame.Width, frame.Height + inset);
    }

    /// <summary>
    /// How much of the window's height MAUI's content does not get: the title bar strip.
    /// </summary>
    /// <remarks>
    /// From <c>ContentLayoutRect</c>, not <c>ContentRectFor</c>. With FullSizeContentView in the
    /// style mask the latter reports the content rect as the whole frame — technically true, the
    /// content view *is* allowed under the title bar — while MAUI still lays its view out below it,
    /// so that call answers zero for a gap that is really there. ContentLayoutRect is the rect
    /// excluding the title bar, which is the one that matches where the content actually lands.
    /// </remarks>
    static nfloat TitleBarInset(NSWindow window)
    {
        var inset = window.Frame.Height - window.ContentLayoutRect.Height;
        if (inset <= 0)
            inset = window.Frame.Height - window.ContentRectFor(window.Frame).Height;

        return inset > 0 ? inset : 0;
    }

    static CGRect ComputeFrame(QuickEntryOptions options, double width, double height)
    {
        var screen = ScreenForPointer();
        var visible = screen.VisibleFrame;
        var w = (nfloat)width;
        var h = (nfloat)height;

        switch (options.Placement)
        {
            case QuickEntryPlacement.BottomCenter:
            {
                var x = visible.X + (visible.Width - w) / 2;
                var y = visible.Y + (nfloat)(visible.Height * options.BottomMarginRatio);
                return new CGRect(x, Clamp(y, visible.Y, visible.Y + visible.Height - h), w, h);
            }

            case QuickEntryPlacement.Center:
            {
                var x = visible.X + (visible.Width - w) / 2;
                var y = visible.Y + (visible.Height - h) / 2;
                return new CGRect(x, y, w, h);
            }

            case QuickEntryPlacement.NearCursor:
            {
                var mouse = NSEvent.CurrentMouseLocation;
                var x = Clamp(mouse.X, visible.X, visible.X + visible.Width - w);
                var y = Clamp(mouse.Y - h - 12, visible.Y, visible.Y + visible.Height - h);
                return new CGRect(x, y, w, h);
            }

            case QuickEntryPlacement.Manual:
            {
                var primary = NSScreen.Screens.Length > 0 ? NSScreen.Screens[0].Frame : visible;
                var x = (nfloat)options.X;
                var y = primary.Height - (nfloat)options.Y - h;
                return new CGRect(x, y, w, h);
            }

            default:
            {
                var x = visible.X + (visible.Width - w) / 2;
                var top = visible.Y + visible.Height - (nfloat)(visible.Height * options.TopMarginRatio);
                var y = Clamp(top - h, visible.Y, visible.Y + visible.Height - h);
                return new CGRect(x, y, w, h);
            }
        }
    }

    internal static NSScreen ScreenForPointer()
    {
        var mouse = NSEvent.CurrentMouseLocation;
        foreach (var screen in NSScreen.Screens)
        {
            if (screen.Frame.Contains(mouse))
                return screen;
        }
        return NSScreen.MainScreen ?? NSScreen.Screens[0];
    }

    static nfloat Clamp(nfloat value, nfloat min, nfloat max)
        => max < min ? min : (value < min ? min : (value > max ? max : value));
}
