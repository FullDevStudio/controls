using AppKit;
using CoreGraphics;
using Foundation;
using ObjCRuntime;
using WebKit;
using Shiny.Maui.Controls.Desktop.TrayIcon;
using Shiny.Maui.Controls.Infrastructure;
using Microsoft.Maui.Graphics;
using MauiWindow = Microsoft.Maui.Controls.Window;

namespace Shiny.Maui.Controls.Desktop.FileDrop;

/// <summary>
/// AppKit backing for window-level file drop.
/// </summary>
/// <remarks>
/// <para>
/// AppKit finds a drop's destination by hit-testing the view under the cursor and then walking
/// <em>up</em> the superview chain for the first view registered for the dragged types. That rules
/// out the obvious implementation — a transparent overlay on top — twice over: an overlay that
/// returns <c>nil</c> from <c>hitTest:</c> so clicks pass through is never found by the search, and
/// one that does not swallows every click in the app.
/// </para>
/// <para>
/// So the drop view is installed as an <b>ancestor</b> instead: it becomes the window's
/// <c>contentView</c> and MAUI's real content becomes its only subview. It draws nothing, hit-tests
/// like the plain view it replaces, and sits on the chain above every view in the window — which is
/// exactly where the search ends up.
/// </para>
/// <para>
/// Being an ancestor is still not enough on its own, because <see cref="WKWebView"/> registers for
/// file drags itself and is found first, deeper down. <see cref="FileDropOptions.SuppressWebViewDrop"/>
/// unregisters it, which is what makes a drop land on the app rather than navigating a
/// <c>BlazorWebView</c> to the dropped file.
/// </para>
/// </remarks>
static class FileDropPlatform
{
    public static bool IsSupported => true;

    public static IDisposable? Attach(IFileDropHost host, MauiWindow window, object platformWindow)
        => platformWindow is NSWindow native ? new MacDropTarget(host, window, native) : null;
}


sealed class MacDropTarget : IDisposable
{
    /// <summary>
    /// The only pasteboard type asked for. Everything a file manager drags carries it, and asking
    /// for it alone means a drag of plain text or a colour swatch is refused by AppKit before any of
    /// this runs.
    /// </summary>
    internal const string FileUrlType = "public.file-url";

    /// <summary>
    /// The window is re-checked at most this often. <c>NSWindowDidUpdateNotification</c> fires after
    /// every event batch, which makes it a free timer as long as the work behind it is throttled.
    /// </summary>
    static readonly TimeSpan recheckInterval = TimeSpan.FromSeconds(1);

    readonly IFileDropHost host;
    readonly MauiWindow window;
    readonly NSWindow native;
    readonly List<NSObject> observers = new();
    readonly TimeProvider time;

    MacFileDropView? view;
    NSView? originalContent;
    long? lastCheck; // a TimeProvider timestamp; null until the first check
    bool disposed;

    public MacDropTarget(IFileDropHost host, MauiWindow window, NSWindow native)
    {
        this.host = host;
        this.window = window;
        this.native = native;
        this.time = ShinyTime.For(window);

        MacMainThread.Invoke(() =>
        {
            this.EnsureInstalled();

            // MAUI replaces the window's content view as it builds and rebuilds pages, and a
            // BlazorWebView appears long after the first attach. Both notifications land on the main
            // thread and the work behind them is throttled, so re-checking is cheaper than trying to
            // predict when either happens.
            this.observers.Add(NSNotificationCenter.DefaultCenter.AddObserver(
                NSWindow.DidUpdateNotification, _ => this.Recheck(), this.native));
            this.observers.Add(NSNotificationCenter.DefaultCenter.AddObserver(
                NSWindow.DidBecomeKeyNotification, _ => this.Recheck(force: true), this.native));
        });
    }

    void Recheck(bool force = false)
    {
        if (this.disposed)
            return;

        if (!force && this.lastCheck is { } last && this.time.GetElapsedTime(last) < recheckInterval)
            return;

        this.lastCheck = this.time.GetTimestamp();
        this.EnsureInstalled();
    }

    void EnsureInstalled()
    {
        try
        {
            if (this.native.ContentView is not MacFileDropView installed)
            {
                var content = this.native.ContentView;
                if (content == null)
                    return;

                var drop = new MacFileDropView(this)
                {
                    Frame = content.Frame,
                    AutoresizesSubviews = true
                };

                this.originalContent ??= content;

                // Order matters: pull the old view out by assigning the new content view first,
                // otherwise AddSubview re-parents a view the window still believes it owns.
                this.native.ContentView = drop;
                content.Frame = drop.Bounds;
                content.AutoresizingMask = NSViewResizingMask.WidthSizable | NSViewResizingMask.HeightSizable;
                drop.AddSubview(content);

                drop.RegisterForDraggedTypes([FileUrlType]);
                this.view = drop;
                installed = drop;
            }

            if (this.host.Options.SuppressWebViewDrop)
                SuppressWebViews(installed);
        }
        catch (Exception ex)
        {
            this.host.LogError("Installing the AppKit file drop view failed", ex);
        }
    }

    static void SuppressWebViews(NSView root)
    {
        foreach (var subview in root.Subviews)
        {
            // WKWebView answers file drags by navigating to the dropped file, which looks like the
            // app losing its UI. Unregistering is the only supported way to decline: there is no
            // AllowExternalDrop equivalent on this platform.
            if (subview is WKWebView web && web.RegisteredDragTypes().Length > 0)
                web.UnregisterDraggedTypes();

            SuppressWebViews(subview);
        }
    }

    internal NSDragOperation DraggingEntered(NSView sender, INSDraggingInfo info)
    {
        var files = ReadFiles(info);
        if (!this.host.WouldAccept(files))
            return NSDragOperation.None;

        this.host.NotifyEnter(this.window, files, Position(sender, info));
        return NSDragOperation.Copy;
    }

    internal NSDragOperation DraggingUpdated(NSView sender, INSDraggingInfo info)
    {
        var files = ReadFiles(info);
        if (!this.host.WouldAccept(files))
            return NSDragOperation.None;

        this.host.NotifyOver(this.window, files, Position(sender, info));
        return NSDragOperation.Copy;
    }

    internal void DraggingExited() => this.host.NotifyLeave(this.window);

    internal bool PerformDragOperation(NSView sender, INSDraggingInfo info)
    {
        var files = ReadFiles(info);
        if (files.Count == 0)
            return false;

        this.host.NotifyDrop(this.window, files, Position(sender, info));
        return true;
    }

    static IReadOnlyList<DroppedFile> ReadFiles(INSDraggingInfo info)
    {
        var pasteboard = info.DraggingPasteboard;
        if (pasteboard?.PasteboardItems is not { } items)
            return [];

        var files = new List<DroppedFile>(items.Length);

        foreach (var item in items)
        {
            // The item carries the URL as a string rather than an NSURL, so it has to be parsed
            // back. A file:// URL is percent-encoded, which is why NSUrl does the decoding and the
            // raw string is never used as a path.
            var raw = item.GetStringForType(MacDropTarget.FileUrlType);
            if (String.IsNullOrEmpty(raw))
                continue;

            using var url = new NSUrl(raw);
            if (url.Path is { Length: > 0 } path)
                files.Add(DroppedFile.FromPath(path));
        }

        return files;
    }

    static Point Position(NSView view, INSDraggingInfo info)
    {
        // DraggingLocation is in the window's base coordinates, and AppKit measures from the
        // bottom-left. Everything above this line thinks in MAUI's top-left units.
        var local = view.ConvertPointFromView(info.DraggingLocation, null);
        return new Point(local.X, view.Bounds.Height - local.Y);
    }

    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;

        MacMainThread.Invoke(() =>
        {
            foreach (var observer in this.observers)
                NSNotificationCenter.DefaultCenter.RemoveObserver(observer);
            this.observers.Clear();

            if (this.view != null && ReferenceEquals(this.native.ContentView, this.view))
            {
                this.view.UnregisterDraggedTypes();

                if (this.originalContent != null)
                    this.native.ContentView = this.originalContent;
            }

            this.view = null;
            this.originalContent = null;
        });
    }
}


/// <summary>
/// The window's content view, standing in for the one MAUI made so that it can sit on the superview
/// chain above every view in the window.
/// </summary>
/// <remarks>
/// The <c>NSDraggingDestination</c> methods are exported by selector rather than overridden: the
/// .NET bindings put them on a separate <c>NSDraggingDestination</c> model class, not on
/// <see cref="NSView"/>, even though the Objective-C <c>NSView</c> declares conformance. Exporting
/// them directly is what the binding's own generated code does for the same reason.
/// </remarks>
sealed class MacFileDropView : NSView
{
    readonly MacDropTarget owner;

    public MacFileDropView(MacDropTarget owner) => this.owner = owner;

    /// <summary>Draws nothing, so it cannot change how the window looks.</summary>
    public override bool IsOpaque => false;

    [Export("draggingEntered:")]
    public NSDragOperation DraggingEntered(IntPtr sender)
        => this.With(sender, info => this.owner.DraggingEntered(this, info), NSDragOperation.None);

    [Export("draggingUpdated:")]
    public NSDragOperation DraggingUpdated(IntPtr sender)
        => this.With(sender, info => this.owner.DraggingUpdated(this, info), NSDragOperation.None);

    [Export("draggingExited:")]
    public void DraggingExited(IntPtr sender) => this.owner.DraggingExited();

    [Export("prepareForDragOperation:")]
    public bool PrepareForDragOperation(IntPtr sender) => true;

    [Export("performDragOperation:")]
    public bool PerformDragOperation(IntPtr sender)
        => this.With(sender, info => this.owner.PerformDragOperation(this, info), false);

    T With<T>(IntPtr sender, Func<INSDraggingInfo, T> work, T fallback)
    {
        // The parameter is taken as a raw handle rather than as INSDraggingInfo: an exported method
        // has to marshal what Objective-C actually passes, and a protocol interface is not something
        // the generated stub can construct on its own.
        var info = Runtime.GetINativeObject<INSDraggingInfo>(sender, false);
        return info == null ? fallback : work(info);
    }
}
