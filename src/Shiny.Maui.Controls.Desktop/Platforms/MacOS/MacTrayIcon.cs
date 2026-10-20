using AppKit;
using CoreGraphics;
using Foundation;
using ObjCRuntime;

namespace Shiny.Maui.Controls.Desktop.TrayIcon;

sealed class MacTrayIcon : TrayIconBase
{
    NSStatusItem statusItem = null!;
    NSMenu? nsMenu;
    readonly Dictionary<TrayProgressItem, TrayProgressView> progressViews = new();

    public MacTrayIcon()
    {
        MacMainThread.Invoke(() =>
        {
            this.statusItem = NSStatusBar.SystemStatusBar.CreateStatusItem(NSStatusItemLength.Variable);
            if (this.statusItem.Button != null)
            {
                this.statusItem.Button.Activated += this.OnButtonActivated;
                // sendActionOn: takes an NSEvent *mask* (1 << eventType), not raw NSEventType
                // values — but the .NET binding only exposes SendActionOn(NSEventType). Passing
                // (NSEventType.LeftMouseUp | NSEventType.RightMouseUp) = 6 actually means the mask
                // (LeftMouseDown | LeftMouseUp); right-click is excluded, so the action never
                // fires on secondary click. Build the correct mask via NSEventMask (LeftMouseUp=4 |
                // RightMouseUp=16 = 20) and cast it back to satisfy the binding signature.
                this.statusItem.Button.SendActionOn((NSEventType)(NSEventMask.LeftMouseUp | NSEventMask.RightMouseUp));
            }
        });
    }

    void OnButtonActivated(object? sender, EventArgs e)
    {
        var ev = NSApplication.SharedApplication.CurrentEvent;
        var location = NSEvent.CurrentMouseLocation;
        var x = (int)location.X;
        var y = (int)location.Y;
        if (ev?.Type == NSEventType.RightMouseUp)
        {
            this.RaiseSecondary(x, y);
            this.ShowMenu();
        }
        else
        {
            this.RaisePrimary(x, y);
            if (ev?.ClickCount >= 2) this.RaiseDouble(x, y);
        }
    }

    protected override void OnIconChanged(Func<Stream> factory)
    {
        using var ms = new MemoryStream();
        using (var src = factory()) src.CopyTo(ms);
        var bytes = ms.ToArray();
        var isTemplate = this.IsTemplateImage;

        MacMainThread.Invoke(() =>
        {
            if (this.statusItem.Button == null) return;
            using var data = NSData.FromArray(bytes);
            var image = new NSImage(data);
            image.Template = isTemplate;

            // An NSImage built from raw bytes takes the *bitmap's* pixel size, and the status item
            // draws it at exactly that — so an ordinary app icon (512px and up) paints hundreds of
            // points tall and swamps the whole menu bar. Scale it down to the status bar's own
            // thickness, keeping the aspect ratio. The menu-item images below already do this.
            ScaleToStatusBar(image);
            // Same rationale as OnMenuChanged: release the outgoing image's native
            // backing now rather than leaving it for a GC that the small managed
            // footprint won't promptly trigger.
            var previous = this.statusItem.Button.Image;
            this.statusItem.Button.Image = image;
            previous?.Dispose();
        });
    }

    /// <summary>
    /// Fits an image to the menu bar. The inset leaves the button its own padding, which is what
    /// keeps the glyph from touching the top and bottom edges of the bar.
    /// </summary>
    static void ScaleToStatusBar(NSImage image)
    {
        const double Inset = 4d;

        var size = image.Size;
        if (size.Width <= 0 || size.Height <= 0)
            return;

        var target = Math.Max(1d, (double)NSStatusBar.SystemStatusBar.Thickness - Inset);
        var scale = target / Math.Max((double)size.Width, (double)size.Height);
        if (scale >= 1d)
            return;

        image.Size = new CGSize(size.Width * scale, size.Height * scale);
    }

    protected override void OnTooltipChanged(string? value)
        => MacMainThread.Invoke(() =>
        {
            if (this.statusItem.Button != null)
                this.statusItem.Button.ToolTip = value ?? string.Empty;
        });

    protected override void OnTitleChanged(string? value)
        => this.UpdateButtonTitle();

    protected override void OnBadgeChanged(string? value)
        => this.UpdateButtonTitle();

    void UpdateButtonTitle()
        => MacMainThread.Invoke(() =>
        {
            if (this.statusItem.Button == null) return;
            var t = this.Title;
            var b = this.Badge;
            string combined;
            if (string.IsNullOrEmpty(t) && string.IsNullOrEmpty(b)) combined = string.Empty;
            else if (string.IsNullOrEmpty(b)) combined = t ?? string.Empty;
            else if (string.IsNullOrEmpty(t)) combined = b!;
            else combined = $"{t} {b}";
            this.statusItem.Button.Title = combined;
        });

    protected override void OnVisibilityChanged(bool visible)
        => MacMainThread.Invoke(() => this.statusItem.Visible = visible);

    public override void ShowNotification(string title, string message)
        => MacMainThread.Invoke(() =>
        {
            var n = new NSUserNotification
            {
                Title = title ?? string.Empty,
                InformativeText = message ?? string.Empty
            };
            NSUserNotificationCenter.DefaultUserNotificationCenter.DeliverNotification(n);
        });

    protected override void OnMenuChanged(object? sender, EventArgs e)
        => MacMainThread.Invoke(() =>
        {
            if (this.Menu == null) return;

            // Deliberately do NOT assign statusItem.Menu here. Assigning it makes AppKit
            // pop the menu automatically on *any* button click and suppresses the button's
            // action entirely, so primary (left) click could never be distinguished from
            // secondary (right) click. Instead we keep the built menu in a field and pop it
            // on demand from ShowMenu() (secondary click), leaving primary click free to
            // raise PrimaryClick — matching the Windows and MacCatalyst implementations.
            //
            // Dispose the previous menu explicitly. The managed NSMenu peer holds a native
            // +1 until GC, and because the managed footprint of a menu wrapper is tiny while
            // the native NSMenu/NSMenuItem/NSImage graph behind it is large, the GC rarely
            // runs, finalizers don't fire, and the native bytes pile up. Apps that rebuild
            // the menu on a poll/timer leak a whole menu tree each rebuild (observed: tens of
            // GB over a long-running session). Disposing here releases it immediately.
            var previous = this.nsMenu;
            this.progressViews.Clear();
            this.nsMenu = this.BuildNSMenu(this.Menu.Items);
            previous?.Dispose();
        });

    protected override void OnMenuItemUpdated(TrayMenuItemBase item)
    {
        if (item is not TrayProgressItem progress)
        {
            base.OnMenuItemUpdated(item);
            return;
        }
        // Repaint the one row rather than rebuilding: the popped-up menu is the one AppKit is
        // tracking, so a rebuild would only replace a menu the user can't see. The main-thread hop
        // is serviced in the event-tracking run-loop mode, so this lands while the menu is open.
        MacMainThread.Invoke(() =>
        {
            if (this.progressViews.TryGetValue(progress, out var view))
                view.NeedsDisplay = true;
        });
    }

    public override void ShowMenu()
        => MacMainThread.Invoke(() =>
        {
            // Pop the menu on demand rather than leaving it assigned to statusItem.Menu, so
            // the button keeps sending its action and primary-click stays distinguishable
            // from secondary-click. PopUpStatusItemMenu is the same call the MacCatalyst head
            // uses; it's been soft-deprecated since 10.10 but remains the simplest reliable
            // way to anchor a status-bar menu under the icon, and there's no AOT-safe
            // replacement bound for status items.
#pragma warning disable CA1422 // PopUpStatusItemMenu obsoleted since 10.10 — intentional, see above
            if (this.nsMenu != null) this.statusItem.PopUpStatusItemMenu(this.nsMenu);
#pragma warning restore CA1422
        });

    NSMenu BuildNSMenu(IEnumerable<TrayMenuItemBase> items)
    {
        var menu = new NSMenu { AutoEnablesItems = false };
        foreach (var item in items)
        {
            if (!item.IsVisible) continue;
            NSMenuItem nsi;
            switch (item)
            {
                case TraySeparator:
                    nsi = NSMenuItem.SeparatorItem;
                    break;
                case TraySubmenu sub:
                    nsi = new NSMenuItem(sub.Label) { Submenu = this.BuildNSMenu(sub.Items) };
                    nsi.Enabled = sub.IsEnabled;
                    break;
                case TrayCheckMenuItem check:
                    nsi = new NSMenuItem(check.Label, (s, _) =>
                    {
                        check.RaiseToggled(!check.IsChecked);
                        if (s is NSMenuItem m) m.State = check.IsChecked ? NSCellStateValue.On : NSCellStateValue.Off;
                    });
                    nsi.State = check.IsChecked ? NSCellStateValue.On : NSCellStateValue.Off;
                    nsi.Enabled = check.IsEnabled;
                    break;
                case TrayMenuItem mi:
                {
                    var (keyEq, mask) = ParseAccelerator(mi.Accelerator);
                    nsi = new NSMenuItem(mi.Label, keyEq, (_, _) => mi.RaiseClicked());
                    nsi.KeyEquivalentModifierMask = mask;
                    nsi.Enabled = mi.IsEnabled;
                    if (mi.Pill != null)
                        nsi.AttributedTitle = PillTitle(mi.Label, mi.Pill);
                    if (mi.Icon != null)
                    {
                        try
                        {
                            using var ms = new MemoryStream();
                            using (var src = mi.Icon()) src.CopyTo(ms);
                            using var data = NSData.FromArray(ms.ToArray());
                            var img = new NSImage(data);
                            img.Size = new CGSize(16, 16);
                            nsi.Image = img;
                        }
                        catch { /* best-effort */ }
                    }
                    break;
                }
                case TrayProgressItem progress:
                {
                    var view = new TrayProgressView(progress);
                    this.progressViews[progress] = view;
                    nsi = new NSMenuItem(progress.Label) { View = view, Enabled = progress.IsEnabled };
                    break;
                }
                default:
                    continue;
            }
            menu.AddItem(nsi);
        }
        return menu;
    }

    /// <summary>
    /// The label followed by the pill as an inline image. Only the font is set, never a colour, so
    /// AppKit still swaps the label to the highlighted text colour when the row is selected.
    /// </summary>
    static NSAttributedString PillTitle(string label, TrayPill pill)
    {
        var title = new NSMutableAttributedString(label + "  ", new NSStringAttributes { Font = NSFont.MenuFontOfSize(0) }.Dictionary);
        var fill = AppleTrayDrawing.ToneColor(pill.Tone, NSColor.ControlAccent.CGColor);
        var (cg, size) = AppleTrayDrawing.RenderPill(pill.Text, fill, 2);

        // The attachment and the attributed title retain what they need natively; our wrappers (and
        // the CGImage, which the NSImage retains) are released now rather than left to the finalizer.
        using (cg)
        using (var image = new NSImage(cg, size))
        using (var attachment = new NSTextAttachment
        {
            Image = image,
            Bounds = new CGRect(0, AppleTrayDrawing.PillBaselineOffset(), size.Width, size.Height)
        })
        using (var pillString = NSAttributedString.FromAttachment(attachment))
        {
            title.Append(pillString);
        }
        return title;
    }

    static (string keyEquivalent, NSEventModifierMask mask) ParseAccelerator(string? accelerator)
    {
        var parsed = TrayAccelerator.Parse(accelerator);
        if (parsed == null) return (string.Empty, 0);
        NSEventModifierMask mask = 0;
        if ((parsed.Modifiers & TrayAcceleratorModifiers.Control) != 0) mask |= NSEventModifierMask.ControlKeyMask;
        if ((parsed.Modifiers & TrayAcceleratorModifiers.Alt) != 0) mask |= NSEventModifierMask.AlternateKeyMask;
        if ((parsed.Modifiers & TrayAcceleratorModifiers.Shift) != 0) mask |= NSEventModifierMask.ShiftKeyMask;
        if ((parsed.Modifiers & TrayAcceleratorModifiers.Meta) != 0) mask |= NSEventModifierMask.CommandKeyMask;

        var key = MapKey(parsed.Key);
        return (key, mask);
    }

    static string MapKey(string key)
    {
        if (key.Length == 1)
            return key.ToLowerInvariant();

        if (key.Length >= 2 && (key[0] == 'F' || key[0] == 'f') && int.TryParse(key.AsSpan(1), out var fn) && fn >= 1 && fn <= 24)
            return ((char)(0xF704 + fn - 1)).ToString();

        return key.ToLowerInvariant() switch
        {
            "esc" or "escape" => "",
            "enter" or "return" => "\r",
            "tab" => "\t",
            "space" or "spacebar" => " ",
            "back" or "backspace" => "\b",
            "delete" or "del" => "",
            "left" => ((char)0xF702).ToString(),
            "up" => ((char)0xF700).ToString(),
            "right" => ((char)0xF703).ToString(),
            "down" => ((char)0xF701).ToString(),
            "home" => ((char)0xF729).ToString(),
            "end" => ((char)0xF72B).ToString(),
            "pgup" or "pageup" => ((char)0xF72C).ToString(),
            "pgdn" or "pagedown" => ((char)0xF72D).ToString(),
            _ => string.Empty
        };
    }

    public override void Dispose()
    {
        MacMainThread.Invoke(() =>
        {
            if (this.statusItem.Button != null) this.statusItem.Button.Activated -= this.OnButtonActivated;
            NSStatusBar.SystemStatusBar.RemoveStatusItem(this.statusItem);
        });
        base.Dispose();
    }
}

/// <summary>
/// A tray progress row. It reads its item on every draw, so an update is just a repaint. Width is
/// a minimum: the menu stretches it to the widest item through the autoresizing mask.
/// </summary>
sealed class TrayProgressView : NSView
{
    /// <summary>Where a plain menu item's title starts, so the row lines up with the items around it.</summary>
    const double Leading = 14;

    readonly TrayProgressItem item;

    public TrayProgressView(TrayProgressItem item)
        : base(new CGRect(0, 0, AppleTrayDrawing.ProgressWidth, AppleTrayDrawing.ProgressHeight(item.HasCaption)))
    {
        this.item = item;
        this.AutoresizingMask = NSViewResizingMask.WidthSizable;
    }

    public override bool IsFlipped => true;

    public override void DrawRect(CGRect dirtyRect)
    {
        var ctx = NSGraphicsContext.CurrentContext?.CGContext;
        if (ctx == null) return;

        // Resolved here, inside drawing, so the dynamic colours follow light/dark mode
        var enabled = this.item.IsEnabled;
        var fill = AppleTrayDrawing.ToneColor(this.item.Tone, NSColor.ControlAccent.CGColor);
        var colors = new AppleTrayDrawing.ProgressColors(
            (enabled ? NSColor.Label : NSColor.TertiaryLabel).CGColor,
            (enabled ? NSColor.SecondaryLabel : NSColor.TertiaryLabel).CGColor,
            NSColor.QuaternaryLabel.CGColor,
            enabled ? fill : new CGColor(fill, 0.4f));
        AppleTrayDrawing.DrawProgress(ctx, this.Bounds, this.item, colors, Leading);
    }
}
