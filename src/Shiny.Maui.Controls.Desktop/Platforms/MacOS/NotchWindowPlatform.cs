using AppKit;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Shiny.Maui.Controls.Desktop.TrayIcon;

namespace Shiny.Maui.Controls.Desktop.Notch;

/// <summary>
/// AppKit backing for the notch window.
/// </summary>
/// <remarks>
/// Unlike the quick entry popup this window is genuinely borderless. It never needs keyboard focus, so
/// the title-bar trick that one relies on buys nothing — and a titled window is constrained to sit below
/// the menu bar, which is exactly where the camera notch is not.
/// </remarks>
static class NotchWindowPlatform
{
    /// <summary>
    /// The notch on the screen that has one (the built-in display), measured from the menu-bar areas
    /// either side of it. Falls back to <see cref="NotchGeometry.None"/> on displays without a notch
    /// and on macOS before 12, which predates the API.
    /// </summary>
    public static NotchGeometry GetGeometry() => MacMainThread.Invoke(() =>
    {
        if (!OperatingSystem.IsMacOSVersionAtLeast(12))
            return NotchGeometry.None;

        var screen = NotchScreen();
        if (screen == null)
            return NotchGeometry.None;

        var top = screen.SafeAreaInsets.Top;
        if (top <= 0)
            return NotchGeometry.None;

        var left = screen.AuxiliaryTopLeftArea;
        var right = screen.AuxiliaryTopRightArea;
        var width = screen.Frame.Width - left.Width - right.Width;
        return width > 0 ? new NotchGeometry(true, width, top) : NotchGeometry.None;
    });

    public static void Initialize(object platformWindow, NotchWindowOptions options)
    {
        if (platformWindow is not NSWindow window)
            return;

        MacMainThread.Invoke(() =>
        {
            window.StyleMask = NSWindowStyle.Borderless;
            window.IsOpaque = false;
            window.BackgroundColor = NSColor.Clear;
            window.HasShadow = false;
            window.MovableByWindowBackground = false;
            window.HidesOnDeactivate = false;

            // Above the menu bar (MainMenu), so the notch can sit over it and reach into the cut-out.
            window.Level = NSWindowLevel.Status;

            var behavior = NSWindowCollectionBehavior.Stationary | NSWindowCollectionBehavior.IgnoresCycle;
            if (options.JoinAllSpaces)
                behavior |= NSWindowCollectionBehavior.CanJoinAllSpaces | NSWindowCollectionBehavior.FullScreenAuxiliary;
            window.CollectionBehavior = behavior;
            window.SetExcludedFromWindowsMenu(true);

            if (window.ContentView is { } content)
            {
                content.WantsLayer = true;
                if (content.Layer != null)
                    content.Layer.BackgroundColor = NSColor.Clear.CGColor;
            }

            window.OrderOut(null);
        });
    }

    public static void Place(object platformWindow, double width, double height, bool hugNotch, double topMargin, double durationMs)
    {
        if (platformWindow is not NSWindow window)
            return;

        MacMainThread.Invoke(() =>
        {
            var frame = FrameFor(width, height, hugNotch, topMargin);
            if (durationMs <= 0 || !window.IsVisible)
            {
                window.SetFrame(frame, true);
                return;
            }

            // Animated through the window's animator rather than SetFrame(…, animate: true): that one's
            // duration is fixed by animationResizeTime, which only a subclass can change, and MAUI owns
            // the window class. Every step of the animation is a real resize, which is what makes AppKit
            // lay the content out as the notch grows.
            NSAnimationContext.RunAnimation(ctx =>
            {
                ctx.Duration = durationMs / 1000d;
                ctx.TimingFunction = CAMediaTimingFunction.FromName(CAMediaTimingFunction.EaseOut);
                ((NSWindow)window.Animator).SetFrame(frame, true);
            }, () => { });
        });
    }

    public static void Show(object platformWindow)
    {
        if (platformWindow is NSWindow window)
            MacMainThread.Invoke(() => window.OrderFrontRegardless());
    }

    public static void Hide(object platformWindow)
    {
        if (platformWindow is NSWindow window)
            MacMainThread.Invoke(() => window.OrderOut(null));
    }

    public static void Teardown(object platformWindow) => Hide(platformWindow);

    /// <summary>
    /// Reports the pointer moving on and off the window. Event monitors rather than a tracking area: a
    /// tracking area needs an NSResponder of our own to receive mouseEntered, and the content view is
    /// MAUI's. A global monitor sees movement over other apps and a local one over ours; mouse-moved
    /// events need no accessibility permission (only key events do).
    /// </summary>
    public static IDisposable TrackPointer(object platformWindow, Action<bool> changed)
    {
        if (platformWindow is not NSWindow window)
            return new PointerTracking(null, null);

        const NSEventMask mask = NSEventMask.MouseMoved | NSEventMask.LeftMouseDragged | NSEventMask.RightMouseDragged | NSEventMask.OtherMouseDragged;
        var last = false;

        void Check()
        {
            if (!window.IsVisible)
                return;

            // Inflated a little: the window touches the top of the screen, and a pointer pushed against
            // that edge reports a y on the frame's max edge, which Contains() excludes.
            var frame = window.Frame.Inset(-2, -2);
            var over = frame.Contains(NSEvent.CurrentMouseLocation);
            if (over == last)
                return;

            last = over;
            changed(over);
        }

        return MacMainThread.Invoke(() =>
        {
            var global = NSEvent.AddGlobalMonitorForEventsMatchingMask(mask, _ => MacMainThread.Invoke(Check));
            var local = NSEvent.AddLocalMonitorForEventsMatchingMask(mask, e =>
            {
                Check();
                return e;
            });
            Check();
            return new PointerTracking(global, local);
        });
    }

    sealed class PointerTracking(NSObject? global, NSObject? local) : IDisposable
    {
        public void Dispose() => MacMainThread.Invoke(() =>
        {
            if (global != null)
                NSEvent.RemoveMonitor(global);
            if (local != null)
                NSEvent.RemoveMonitor(local);
        });
    }

    /// <summary>
    /// Top-centre of the notch screen. Hugging the notch, the top edge is the screen's own top edge —
    /// over the menu bar, inside the cut-out; otherwise it sits just below the menu bar. AppKit's origin
    /// is bottom-left, so the y is the top minus the height.
    /// </summary>
    static CGRect FrameFor(double width, double height, bool hugNotch, double topMargin)
    {
        var screen = (hugNotch ? NotchScreen() : null) ?? NSScreen.MainScreen ?? NSScreen.Screens[0];
        var w = (nfloat)width;
        var h = (nfloat)height;
        var x = screen.Frame.X + ((screen.Frame.Width - w) / 2);
        var top = hugNotch
            ? screen.Frame.Y + screen.Frame.Height
            : screen.VisibleFrame.Y + screen.VisibleFrame.Height - (nfloat)topMargin;
        return new CGRect(Math.Round(x), Math.Round(top - h), w, h);
    }

    static NSScreen? NotchScreen()
    {
        if (OperatingSystem.IsMacOSVersionAtLeast(12))
        {
            foreach (var screen in NSScreen.Screens)
            {
                if (screen.SafeAreaInsets.Top > 0)
                    return screen;
            }
        }
        return null;
    }
}
