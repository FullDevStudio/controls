using System.Runtime.InteropServices;
using CoreGraphics;
using ObjCRuntime;
using static Shiny.Maui.Controls.Desktop.TrayIcon.AppKitInterop;

namespace Shiny.Maui.Controls.Desktop.TrayIcon;

sealed class MacCatalystTrayIcon : TrayIconBase
{
    static readonly Dictionary<long, Action> Callbacks = new();
    static readonly Dictionary<long, WeakReference<MacCatalystTrayIcon>> ButtonOwners = new();
    static long NextTag = 1;
    static IntPtr CallbackClass;
    static IntPtr ActionSel;
    static IntPtr ButtonActionSel;
    static IntPtr SharedCallback;
    static IntPtr ProgressViewClass;
    static readonly Dictionary<IntPtr, TrayProgressItem> ProgressViewItems = new();

    [Flags]
    enum NSEventModifierFlagsRaw : ulong
    {
        Shift = 1UL << 17,
        Control = 1UL << 18,
        Option = 1UL << 19,
        Command = 1UL << 20
    }

    static MacCatalystTrayIcon()
    {
        EnsureLoaded();
        var nsObject = GetClass("NSObject");
        CallbackClass = AllocateClassPair(nsObject, "ShinyTrayCB", IntPtr.Zero);
        ActionSel = Sel("handleAction:");
        ButtonActionSel = Sel("handleButton:");
        unsafe
        {
            AddMethod(CallbackClass, ActionSel, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&HandleAction, "v@:@");
            AddMethod(CallbackClass, ButtonActionSel, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, IntPtr, void>)&HandleButton, "v@:@");
        }
        RegisterClassPair(CallbackClass);
        var alloc = MsgSend(CallbackClass, Sel("alloc"));
        SharedCallback = MsgSend(alloc, Sel("init"));

        // Progress rows are an NSView subclass registered at runtime, the same way as the
        // callback target above — there is no AppKit binding to subclass from on Catalyst.
        ProgressViewClass = AllocateClassPair(GetClass("NSView"), "ShinyTrayProgressView", IntPtr.Zero);
        unsafe
        {
            AddMethod(ProgressViewClass, Sel("drawRect:"), (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, NSRect, void>)&DrawProgressView, "v@:{CGRect={CGPoint=dd}{CGSize=dd}}");
            AddMethod(ProgressViewClass, Sel("isFlipped"), (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, byte>)&ProgressViewIsFlipped, "B@:");
        }
        RegisterClassPair(ProgressViewClass);
    }

    [UnmanagedCallersOnly]
    static byte ProgressViewIsFlipped(IntPtr self, IntPtr sel) => 1;

    [UnmanagedCallersOnly]
    static void DrawProgressView(IntPtr self, IntPtr sel, NSRect dirtyRect)
    {
        try
        {
            TrayProgressItem? item;
            lock (ProgressViewItems) ProgressViewItems.TryGetValue(self, out item);
            if (item == null) return;

            var gc = MsgSend(GetClass("NSGraphicsContext"), Sel("currentContext"));
            if (gc == IntPtr.Zero) return;
            var ctx = Runtime.GetINativeObject<CGContext>(MsgSend(gc, Sel("CGContext")), false);
            if (ctx == null) return;

            // Resolved inside drawRect:, where AppKit has set the drawing appearance, so the
            // semantic colours come back as their light or dark variant
            var enabled = item.IsEnabled;
            var fill = AppleTrayDrawing.ToneColor(item.Tone, NSColorCG("controlAccentColor"));
            var colors = new AppleTrayDrawing.ProgressColors(
                NSColorCG(enabled ? "labelColor" : "tertiaryLabelColor") ?? new CGColor(0, 0, 0, 1),
                NSColorCG(enabled ? "secondaryLabelColor" : "tertiaryLabelColor") ?? new CGColor(0.5f, 0.5f, 0.5f, 1),
                NSColorCG("quaternaryLabelColor") ?? new CGColor(0.5f, 0.5f, 0.5f, 0.2f),
                enabled ? fill : new CGColor(fill, 0.4f));

            var b = GetRect(self, Sel("bounds"));
            AppleTrayDrawing.DrawProgress(ctx, new CGRect(b.X, b.Y, b.Width, b.Height), item, colors, ProgressLeading);
        }
        catch
        {
            // an exception escaping an UnmanagedCallersOnly method takes the process down
        }
    }

    /// <summary>Where a plain menu item's title starts, so the row lines up with the items around it.</summary>
    const double ProgressLeading = 14;

    static CGColor? NSColorCG(string selector)
    {
        var color = MsgSend(GetClass("NSColor"), Sel(selector));
        if (color == IntPtr.Zero) return null;
        var cg = MsgSend(color, Sel("CGColor"));
        return cg == IntPtr.Zero ? null : Runtime.GetINativeObject<CGColor>(cg, false);
    }

    [UnmanagedCallersOnly]
    static void HandleAction(IntPtr self, IntPtr sel, IntPtr sender)
    {
        var tag = (long)MsgSend(sender, Sel("tag"));
        Action? action;
        lock (Callbacks) Callbacks.TryGetValue(tag, out action);
        action?.Invoke();
    }

    [UnmanagedCallersOnly]
    static void HandleButton(IntPtr self, IntPtr sel, IntPtr sender)
    {
        var tag = (long)MsgSend(sender, Sel("tag"));
        WeakReference<MacCatalystTrayIcon>? wr;
        lock (ButtonOwners) ButtonOwners.TryGetValue(tag, out wr);
        if (wr != null && wr.TryGetTarget(out var owner))
            owner.OnButtonClicked();
    }

    readonly IntPtr statusItem;
    readonly IntPtr button;
    readonly long buttonTag;
    IntPtr menu = IntPtr.Zero;
    readonly List<long> ownedTags = new();
    readonly Dictionary<TrayProgressItem, IntPtr> progressViews = new();

    public MacCatalystTrayIcon()
    {
        var nsStatusBar = GetClass("NSStatusBar");
        var systemBar = MsgSend(nsStatusBar, Sel("systemStatusBar"));
        this.statusItem = MsgSendDouble(systemBar, Sel("statusItemWithLength:"), -1.0);
        this.button = MsgSend(this.statusItem, Sel("button"));
        this.buttonTag = Interlocked.Increment(ref NextTag);
        lock (ButtonOwners) ButtonOwners[this.buttonTag] = new WeakReference<MacCatalystTrayIcon>(this);
        if (this.button != IntPtr.Zero)
        {
            MsgSendLong(this.button, Sel("setTag:"), this.buttonTag);
            MsgSend(this.button, Sel("setTarget:"), SharedCallback);
            MsgSend(this.button, Sel("setAction:"), ButtonActionSel);
            MsgSendLong(this.button, Sel("sendActionOn:"),
                (long)(NSEventMask.LeftMouseUp | NSEventMask.RightMouseUp));
        }
    }

    [Flags]
    enum NSEventMask : long
    {
        LeftMouseUp = 1L << 2,
        RightMouseUp = 1L << 4
    }

    void OnButtonClicked()
    {
        var nsApp = MsgSend(GetClass("NSApplication"), Sel("sharedApplication"));
        var currentEvent = MsgSend(nsApp, Sel("currentEvent"));
        var typeRaw = currentEvent == IntPtr.Zero ? 0L : (long)MsgSend(currentEvent, Sel("type"));
        if (typeRaw == 4)
        {
            this.RaiseSecondary(0, 0);
            this.ShowMenu();
        }
        else
        {
            this.RaisePrimary(0, 0);
        }
    }

    protected override void OnIconChanged(Func<Stream> factory)
    {
        if (this.button == IntPtr.Zero) return;
        using var ms = new MemoryStream();
        using (var src = factory()) src.CopyTo(ms);
        var data = NSDataFromBytes(ms.ToArray());
        var imgAlloc = MsgSend(GetClass("NSImage"), Sel("alloc"));
        var image = MsgSend(imgAlloc, Sel("initWithData:"), data);
        MsgSendBool(image, Sel("setTemplate:"), this.IsTemplateImage);
        MsgSend(this.button, Sel("setImage:"), image);
        // setImage: retained the image (and released any prior one); release the +1
        // from our alloc so swapping the icon doesn't leak an NSImage each time.
        MsgSend(image, Sel("release"));
    }

    protected override void OnTooltipChanged(string? value)
    {
        if (this.button == IntPtr.Zero) return;
        MsgSend(this.button, Sel("setToolTip:"), NSString(value ?? string.Empty));
    }

    protected override void OnTitleChanged(string? value) => this.UpdateButtonTitle();

    protected override void OnBadgeChanged(string? value) => this.UpdateButtonTitle();

    void UpdateButtonTitle()
    {
        if (this.button == IntPtr.Zero) return;
        var t = this.Title;
        var b = this.Badge;
        string combined;
        if (string.IsNullOrEmpty(t) && string.IsNullOrEmpty(b)) combined = string.Empty;
        else if (string.IsNullOrEmpty(b)) combined = t ?? string.Empty;
        else if (string.IsNullOrEmpty(t)) combined = b!;
        else combined = $"{t} {b}";
        MsgSend(this.button, Sel("setTitle:"), NSString(combined));
    }

    protected override void OnVisibilityChanged(bool visible)
        => MsgSendBool(this.statusItem, Sel("setVisible:"), visible);

    public override void ShowNotification(string title, string message)
    {
        var center = MsgSend(GetClass("NSUserNotificationCenter"), Sel("defaultUserNotificationCenter"));
        if (center == IntPtr.Zero) return;
        var notifAlloc = MsgSend(GetClass("NSUserNotification"), Sel("alloc"));
        var notif = MsgSend(notifAlloc, Sel("init"));
        if (notif == IntPtr.Zero) return;
        MsgSend(notif, Sel("setTitle:"), NSString(title ?? string.Empty));
        MsgSend(notif, Sel("setInformativeText:"), NSString(message ?? string.Empty));
        MsgSend(center, Sel("deliverNotification:"), notif);
        // The center retains the notification for async display; release our alloc's +1.
        MsgSend(notif, Sel("release"));
    }

    protected override void OnMenuChanged(object? sender, EventArgs e)
    {
        this.ClearOwnedTags();
        this.ClearProgressViews();
        if (this.Menu == null) return;

        // BuildMenu hands back an alloc/init'd NSMenu we own a +1 on; releasing the
        // previous one balances that alloc so rebuilds (e.g. on a poll/timer) don't
        // leak a native menu tree each time. See the MacTrayIcon note for why GC won't
        // reclaim these promptly on its own.
        var previous = this.menu;
        this.menu = this.BuildMenu(this.Menu.Items);
        if (previous != IntPtr.Zero)
            MsgSend(previous, Sel("release"));
    }

    protected override void OnMenuItemUpdated(TrayMenuItemBase item)
    {
        if (item is not TrayProgressItem progress)
        {
            base.OnMenuItemUpdated(item);
            return;
        }
        // Repaint in place: the view draws from the item, and a rebuild would replace the menu
        // AppKit is tracking. The main-queue hop is serviced while the menu is open.
        void Repaint()
        {
            if (this.progressViews.TryGetValue(progress, out var view))
                MsgSendBool(view, Sel("setNeedsDisplay:"), true);
        }
        if (Foundation.NSThread.IsMain) Repaint();
        else Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(Repaint);
    }

    void ClearProgressViews()
    {
        lock (ProgressViewItems)
            foreach (var view in this.progressViews.Values)
                ProgressViewItems.Remove(view);
        this.progressViews.Clear();
    }

    public override void ShowMenu()
    {
        if (this.menu != IntPtr.Zero)
            MsgSend(this.statusItem, Sel("popUpStatusItemMenu:"), this.menu);
    }

    IntPtr BuildMenu(IEnumerable<TrayMenuItemBase> items)
    {
        var menuAlloc = MsgSend(GetClass("NSMenu"), Sel("alloc"));
        var m = MsgSend(menuAlloc, Sel("init"));
        MsgSendBool(m, Sel("setAutoenablesItems:"), false);

        foreach (var item in items)
        {
            if (!item.IsVisible) continue;
            switch (item)
            {
                case TraySeparator:
                {
                    var sep = MsgSend(GetClass("NSMenuItem"), Sel("separatorItem"));
                    MsgSend(m, Sel("addItem:"), sep);
                    break;
                }
                case TraySubmenu sub:
                {
                    var nsi = this.NewItem(sub.Label, string.Empty, 0, null);
                    var childMenu = this.BuildMenu(sub.Items);
                    MsgSend(nsi, Sel("setSubmenu:"), childMenu);
                    MsgSendBool(nsi, Sel("setEnabled:"), sub.IsEnabled);
                    MsgSend(m, Sel("addItem:"), nsi);
                    // setSubmenu:/addItem: each retained; release the +1 we own from
                    // BuildMenu's alloc and NewItem's alloc so they don't leak.
                    MsgSend(childMenu, Sel("release"));
                    MsgSend(nsi, Sel("release"));
                    break;
                }
                case TrayCheckMenuItem check:
                {
                    var tag = this.RegisterCallback(() => check.RaiseToggled(!check.IsChecked));
                    var nsi = this.NewItem(check.Label, string.Empty, 0, tag);
                    MsgSendLong(nsi, Sel("setState:"), check.IsChecked ? 1 : 0);
                    MsgSendBool(nsi, Sel("setEnabled:"), check.IsEnabled);
                    MsgSend(m, Sel("addItem:"), nsi);
                    MsgSend(nsi, Sel("release"));
                    break;
                }
                case TrayMenuItem mi:
                {
                    var tag = this.RegisterCallback(() => mi.RaiseClicked());
                    var (keyEq, mods) = ParseAccelerator(mi.Accelerator);
                    var nsi = this.NewItem(mi.Label, keyEq, mods, tag);
                    MsgSendBool(nsi, Sel("setEnabled:"), mi.IsEnabled);
                    if (mi.Pill != null)
                    {
                        try { SetPillTitle(nsi, mi.Label, mi.Pill); }
                        catch { /* best-effort: the plain title stays */ }
                    }
                    if (mi.Icon != null)
                    {
                        try
                        {
                            using var ms = new MemoryStream();
                            using (var src = mi.Icon()) src.CopyTo(ms);
                            var data = NSDataFromBytes(ms.ToArray());
                            var imgAlloc = MsgSend(GetClass("NSImage"), Sel("alloc"));
                            var image = MsgSend(imgAlloc, Sel("initWithData:"), data);
                            MsgSendCGSize(image, Sel("setSize:"), new NSSize { Width = 16, Height = 16 });
                            MsgSend(nsi, Sel("setImage:"), image);
                            // setImage: retained the image; release our alloc's +1.
                            // (data is an autoreleased dataWithBytes: — don't release.)
                            MsgSend(image, Sel("release"));
                        }
                        catch { /* best-effort */ }
                    }
                    MsgSend(m, Sel("addItem:"), nsi);
                    MsgSend(nsi, Sel("release"));
                    break;
                }
                case TrayProgressItem progress:
                {
                    var viewAlloc = MsgSend(ProgressViewClass, Sel("alloc"));
                    var view = MsgSendRect(viewAlloc, Sel("initWithFrame:"),
                        new NSRect(0, 0, AppleTrayDrawing.ProgressWidth, AppleTrayDrawing.ProgressHeight(progress.HasCaption)));
                    MsgSendULong(view, Sel("setAutoresizingMask:"), 2); // NSViewWidthSizable
                    lock (ProgressViewItems) ProgressViewItems[view] = progress;
                    this.progressViews[progress] = view;

                    var nsi = this.NewItem(progress.Label, string.Empty, 0, null);
                    MsgSend(nsi, Sel("setView:"), view);
                    MsgSendBool(nsi, Sel("setEnabled:"), progress.IsEnabled);
                    MsgSend(m, Sel("addItem:"), nsi);
                    // setView:/addItem: retained; the view stays alive as long as the menu does,
                    // which is exactly as long as ProgressViewItems keeps its handle
                    MsgSend(view, Sel("release"));
                    MsgSend(nsi, Sel("release"));
                    break;
                }
            }
        }
        return m;
    }

    /// <summary>
    /// The label followed by the pill as an inline image. Only the font is set, never a colour, so
    /// AppKit still swaps the label to the highlighted text colour when the row is selected.
    /// </summary>
    static void SetPillTitle(IntPtr nsi, string label, TrayPill pill)
    {
        var fill = AppleTrayDrawing.ToneColor(pill.Tone, NSColorCG("controlAccentColor"));
        var (cg, size) = AppleTrayDrawing.RenderPill(pill.Text, fill, 2);

        var image = MsgSendPtrSize(MsgSend(GetClass("NSImage"), Sel("alloc")), Sel("initWithCGImage:size:"),
            cg.Handle, new NSSize { Width = size.Width, Height = size.Height });
        var attachment = MsgSend(MsgSend(GetClass("NSTextAttachment"), Sel("alloc")), Sel("init"));
        MsgSend(attachment, Sel("setImage:"), image);
        MsgSendRect(attachment, Sel("setBounds:"), new NSRect(0, AppleTrayDrawing.PillBaselineOffset(), size.Width, size.Height));
        var pillString = MsgSend(GetClass("NSAttributedString"), Sel("attributedStringWithAttachment:"), attachment);

        var font = MsgSendDouble(GetClass("NSFont"), Sel("menuFontOfSize:"), 0);
        var attributes = MsgSend(GetClass("NSDictionary"), Sel("dictionaryWithObject:forKey:"), font, NSString("NSFont"));
        var title = MsgSend(MsgSend(GetClass("NSMutableAttributedString"), Sel("alloc")), Sel("initWithString:attributes:"),
            NSString(label + "  "), attributes);
        MsgSend(title, Sel("appendAttributedString:"), pillString);
        MsgSend(nsi, Sel("setAttributedTitle:"), title);

        // setImage:/setAttributedTitle: retained what they need; release our alloc +1s
        // (pillString, font and attributes are autoreleased)
        MsgSend(title, Sel("release"));
        MsgSend(attachment, Sel("release"));
        MsgSend(image, Sel("release"));
        cg.Dispose();
    }

    IntPtr NewItem(string label, string keyEquivalent, ulong modifierMask, long? tag)
    {
        var alloc = MsgSend(GetClass("NSMenuItem"), Sel("alloc"));
        var nsi = MsgSend(alloc, Sel("initWithTitle:action:keyEquivalent:"),
            NSString(label), tag.HasValue ? ActionSel : IntPtr.Zero, NSString(keyEquivalent ?? string.Empty));
        if (tag.HasValue)
        {
            MsgSendLong(nsi, Sel("setTag:"), tag.Value);
            MsgSend(nsi, Sel("setTarget:"), SharedCallback);
        }
        if (modifierMask != 0)
            MsgSendULong(nsi, Sel("setKeyEquivalentModifierMask:"), modifierMask);
        return nsi;
    }

    static (string keyEquivalent, ulong modifierMask) ParseAccelerator(string? accelerator)
    {
        var parsed = TrayAccelerator.Parse(accelerator);
        if (parsed == null) return (string.Empty, 0UL);

        ulong mods = 0;
        if ((parsed.Modifiers & TrayAcceleratorModifiers.Control) != 0) mods |= (ulong)NSEventModifierFlagsRaw.Control;
        if ((parsed.Modifiers & TrayAcceleratorModifiers.Alt) != 0) mods |= (ulong)NSEventModifierFlagsRaw.Option;
        if ((parsed.Modifiers & TrayAcceleratorModifiers.Shift) != 0) mods |= (ulong)NSEventModifierFlagsRaw.Shift;
        if ((parsed.Modifiers & TrayAcceleratorModifiers.Meta) != 0) mods |= (ulong)NSEventModifierFlagsRaw.Command;

        return (MapKey(parsed.Key), mods);
    }

    static string MapKey(string key)
    {
        if (key.Length == 1) return key.ToLowerInvariant();
        if (key.Length >= 2 && (key[0] == 'F' || key[0] == 'f') && int.TryParse(key.AsSpan(1), out var fn) && fn >= 1 && fn <= 24)
            return ((char)(0xF704 + fn - 1)).ToString();
        return key.ToLowerInvariant() switch
        {
            "enter" or "return" => "\r",
            "tab" => "\t",
            "space" or "spacebar" => " ",
            "back" or "backspace" => "\b",
            "left" => ((char)0xF702).ToString(),
            "up" => ((char)0xF700).ToString(),
            "right" => ((char)0xF703).ToString(),
            "down" => ((char)0xF701).ToString(),
            _ => string.Empty
        };
    }

    long RegisterCallback(Action action)
    {
        var tag = Interlocked.Increment(ref NextTag);
        lock (Callbacks) Callbacks[tag] = action;
        this.ownedTags.Add(tag);
        return tag;
    }

    void ClearOwnedTags()
    {
        lock (Callbacks)
            foreach (var t in this.ownedTags) Callbacks.Remove(t);
        this.ownedTags.Clear();
    }

    public override void Dispose()
    {
        this.ClearOwnedTags();
        this.ClearProgressViews();
        lock (ButtonOwners) ButtonOwners.Remove(this.buttonTag);
        if (this.menu != IntPtr.Zero)
        {
            MsgSend(this.menu, Sel("release"));
            this.menu = IntPtr.Zero;
        }
        var nsStatusBar = GetClass("NSStatusBar");
        var systemBar = MsgSend(nsStatusBar, Sel("systemStatusBar"));
        MsgSend(systemBar, Sel("removeStatusItem:"), this.statusItem);
        base.Dispose();
    }
}
