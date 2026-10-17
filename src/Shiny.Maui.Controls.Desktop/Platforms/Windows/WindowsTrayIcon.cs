using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using static Shiny.Maui.Controls.Desktop.TrayIcon.NativeMethods;
using SDImage = System.Drawing.Image;
using SDBitmap = System.Drawing.Bitmap;
using SDGraphics = System.Drawing.Graphics;
using SDFont = System.Drawing.Font;
using SDColor = System.Drawing.Color;
using SDRectangle = System.Drawing.Rectangle;
using SDRectangleF = System.Drawing.RectangleF;
using SDFontStyle = System.Drawing.FontStyle;
using SDGraphicsUnit = System.Drawing.GraphicsUnit;
using SDSolidBrush = System.Drawing.SolidBrush;
using SDStringFormat = System.Drawing.StringFormat;
using SDStringFormatFlags = System.Drawing.StringFormatFlags;
using SDStringAlignment = System.Drawing.StringAlignment;
using SDSystemFonts = System.Drawing.SystemFonts;
using SDPixelFormat = System.Drawing.Imaging.PixelFormat;
using SDImageFormat = System.Drawing.Imaging.ImageFormat;

namespace Shiny.Maui.Controls.Desktop.TrayIcon;

[SupportedOSPlatform("windows")]
sealed class WindowsTrayIcon : TrayIconBase
{
    static readonly Lazy<IntPtr> WindowClass = new(RegisterWindowClass);
    static readonly Dictionary<IntPtr, WindowsTrayIcon> Instances = new();
    static readonly WndProcDelegate StaticWndProc = StaticHandler;
    static uint NextId = 1;
    static int NextHotKeyId = 0xB001;

    readonly uint id;
    readonly IntPtr hwnd;
    IntPtr hIcon = IntPtr.Zero;
    IntPtr popupMenu = IntPtr.Zero;
    bool added;
    bool disposed;
    Dictionary<int, Action>? menuActions;
    readonly List<IntPtr> menuBitmaps = new();
    readonly List<WindowsTrayOwnerDraw.Row> ownerDrawRows = new();
    IntPtr menuTheme = IntPtr.Zero;
    volatile bool menuOpen;
    readonly Dictionary<int, Action> hotKeyActions = new();

    public WindowsTrayIcon()
    {
        // Register the "ShinyTrayHostWindow" class before CreateWindowEx uses it below —
        // otherwise the window is never created and construction fails.
        _ = WindowClass.Value;

        this.id = Interlocked.Increment(ref NextId);
        this.hwnd = CreateWindowEx(0, "ShinyTrayHostWindow", null, 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);
        if (this.hwnd == IntPtr.Zero)
            throw new InvalidOperationException("Failed to create tray host window.");

        lock (Instances)
            Instances[this.hwnd] = this;

        var data = this.BuildData(NIF_MESSAGE);
        data.uCallbackMessage = WM_TRAYICON;
        if (!Shell_NotifyIcon(NIM_ADD, ref data))
            throw new InvalidOperationException("Shell_NotifyIcon NIM_ADD failed.");

        data.uTimeoutOrVersion = NOTIFYICON_VERSION_4;
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
        this.added = true;
    }

    static IntPtr RegisterWindowClass()
    {
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(StaticWndProc),
            hInstance = GetModuleHandle(null),
            lpszClassName = "ShinyTrayHostWindow"
        };
        var atom = RegisterClassEx(ref wc);
        return new IntPtr(atom);
    }

    NOTIFYICONDATA BuildData(uint flags)
    {
        return new NOTIFYICONDATA
        {
            cbSize = (uint)Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = this.hwnd,
            uID = this.id,
            uFlags = flags,
            hIcon = this.hIcon,
            szTip = this.Tooltip ?? string.Empty,
            szInfo = string.Empty,
            szInfoTitle = string.Empty
        };
    }

    static IntPtr StaticHandler(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        WindowsTrayIcon? icon;
        lock (Instances) Instances.TryGetValue(hWnd, out icon);
        if (icon != null)
        {
            if (msg == WM_TRAYICON)
            {
                var ev = (uint)(lParam.ToInt64() & 0xFFFF);
                GetCursorPos(out var pt);
                switch (ev)
                {
                    case WM_LBUTTONUP: icon.RaisePrimary(pt.X, pt.Y); break;
                    case WM_RBUTTONUP:
                    case WM_CONTEXTMENU:
                        icon.RaiseSecondary(pt.X, pt.Y);
                        icon.ShowMenu();
                        break;
                    case WM_LBUTTONDBLCLK: icon.RaiseDouble(pt.X, pt.Y); break;
                }
                return IntPtr.Zero;
            }
            if (msg == WM_MEASUREITEM || msg == WM_DRAWITEM)
            {
                if (icon.HandleOwnerDraw(msg, lParam))
                    return (IntPtr)1;
            }
            if (msg == WM_HOTKEY)
            {
                var hkId = wParam.ToInt32();
                Action? action;
                lock (icon.hotKeyActions) icon.hotKeyActions.TryGetValue(hkId, out action);
                action?.Invoke();
                return IntPtr.Zero;
            }
        }
        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    unsafe bool HandleOwnerDraw(uint msg, IntPtr lParam)
    {
        if (msg == WM_MEASUREITEM)
        {
            ref var mis = ref *(MEASUREITEMSTRUCT*)lParam;
            var row = this.OwnerDrawRow(mis.CtlType, mis.itemData);
            if (row == null) return false;
            WindowsTrayOwnerDraw.Measure(row, ref mis);
            return true;
        }
        else
        {
            ref var dis = ref *(DRAWITEMSTRUCT*)lParam;
            var row = this.OwnerDrawRow(dis.CtlType, dis.itemData);
            if (row == null) return false;
            WindowsTrayOwnerDraw.Draw(row, this.menuTheme, ref dis);
            return true;
        }
    }

    /// <summary>Item data is the row's index + 1, so a zero (a menu we did not build) never matches.</summary>
    WindowsTrayOwnerDraw.Row? OwnerDrawRow(uint ctlType, UIntPtr itemData)
    {
        if (ctlType != ODT_MENU) return null;
        var index = (long)(ulong)itemData - 1;
        return index >= 0 && index < this.ownerDrawRows.Count ? this.ownerDrawRows[(int)index] : null;
    }

    protected override void OnMenuItemUpdated(TrayMenuItemBase item)
    {
        if (item is not TrayProgressItem)
        {
            base.OnMenuItemUpdated(item);
            return;
        }
        // Owner-drawn rows read their item on every paint, so a closed menu needs nothing. An open
        // one is a modal popup window of class #32768 on the tray thread; invalidating it repaints
        // the rows with the new value (InvalidateRect is safe to call from any thread).
        if (this.menuOpen)
            RepaintOpenMenus(this.hwnd);
    }

    static unsafe void RepaintOpenMenus(IntPtr owner)
    {
        var thread = GetWindowThreadProcessId(owner, IntPtr.Zero);
        EnumThreadWindows(thread, (IntPtr)(delegate* unmanaged<IntPtr, IntPtr, int>)&InvalidateIfMenu, IntPtr.Zero);
    }

    [UnmanagedCallersOnly]
    static unsafe int InvalidateIfMenu(IntPtr hwnd, IntPtr lParam)
    {
        var buffer = stackalloc char[16];
        var length = GetClassName(hwnd, buffer, 16);
        if (new ReadOnlySpan<char>(buffer, Math.Max(0, length)).SequenceEqual("#32768"))
            InvalidateRect(hwnd, IntPtr.Zero, false);
        return 1;
    }

    protected override void OnIconChanged(Func<Stream> factory)
    {
        var oldIcon = this.hIcon;
        this.hIcon = LoadHIconFromStream(factory(), this.Badge);
        var data = this.BuildData(NIF_ICON);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
        if (oldIcon != IntPtr.Zero) DestroyIcon(oldIcon);
    }

    static IntPtr LoadHIconFromStream(Stream stream, string? badge)
    {
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            bytes = ms.ToArray();
        }
        stream.Dispose();

        if (!string.IsNullOrEmpty(badge))
            bytes = ComposeBadge(bytes, badge);

        var tmp = Path.Combine(Path.GetTempPath(), "shinytray_" + Guid.NewGuid().ToString("N") + ".ico");
        try
        {
            var icoBytes = IsIco(bytes) ? bytes : WrapPngAsIco(bytes);
            File.WriteAllBytes(tmp, icoBytes);
            var cx = GetSystemMetrics(SM_CXSMICON);
            var cy = GetSystemMetrics(SM_CYSMICON);
            return LoadImage(IntPtr.Zero, tmp, IMAGE_ICON, cx, cy, LR_LOADFROMFILE);
        }
        finally
        {
            try { File.Delete(tmp); } catch { }
        }
    }

    static byte[] ComposeBadge(byte[] baseBytes, string badge)
    {
        // For ICO inputs we'd need a multi-image decoder; only composite over PNG sources.
        if (IsIco(baseBytes))
            return baseBytes;

        using var srcMs = new MemoryStream(baseBytes);
        using var src = SDImage.FromStream(srcMs);
        var size = Math.Max(src.Width, src.Height);
        using var bmp = new SDBitmap(size, size, SDPixelFormat.Format32bppArgb);
        using (var g = SDGraphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, new SDRectangle(0, 0, size, size));

            var text = badge.Length > 3 ? badge[..2] + "+" : badge;
            var pillHeight = Math.Max(10, size * 55 / 100);
            using var font = new SDFont(SDSystemFonts.MessageBoxFont!.FontFamily, pillHeight * 0.62f, SDFontStyle.Bold, SDGraphicsUnit.Pixel);
            using var measureFmt = new SDStringFormat(SDStringFormat.GenericTypographic);
            var textSize = g.MeasureString(text, font, int.MaxValue, measureFmt);

            var horizPad = pillHeight * 0.32f;
            var pillWidth = (float)Math.Max(pillHeight, textSize.Width + horizPad * 2);
            var x = size - pillWidth;
            var y = size - pillHeight;
            var rect = new SDRectangleF(x, y, pillWidth, pillHeight);

            using (var bg = new SDSolidBrush(SDColor.FromArgb(255, 220, 38, 38)))
            using (var path = RoundedRect(rect, pillHeight / 2f))
                g.FillPath(bg, path);

            using var fg = new SDSolidBrush(SDColor.White);
            using var fmt = new SDStringFormat
            {
                Alignment = SDStringAlignment.Center,
                LineAlignment = SDStringAlignment.Center,
                FormatFlags = SDStringFormatFlags.NoWrap
            };
            g.DrawString(text, font, fg, rect, fmt);
        }

        using var outMs = new MemoryStream();
        bmp.Save(outMs, SDImageFormat.Png);
        return outMs.ToArray();
    }

    static GraphicsPath RoundedRect(SDRectangleF r, float radius)
    {
        var p = new GraphicsPath();
        var d = radius * 2;
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static IntPtr LoadMenuHBitmapFromStream(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        stream.Dispose();
        ms.Position = 0;
        using var src = SDImage.FromStream(ms);
        var sz = Math.Max(16, GetSystemMetrics(SM_CXMENUCHECK));
        var bmp = new SDBitmap(sz, sz, SDPixelFormat.Format32bppPArgb);
        using (var g = SDGraphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.DrawImage(src, new SDRectangle(0, 0, sz, sz));
        }
        var hbmp = bmp.GetHbitmap(SDColor.FromArgb(0));
        bmp.Dispose();
        return hbmp;
    }

    static bool IsIco(byte[] bytes)
        => bytes.Length >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 1 && bytes[3] == 0;

    static bool IsPng(byte[] bytes)
        => bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;

    static byte[] WrapPngAsIco(byte[] png)
    {
        if (!IsPng(png))
            throw new NotSupportedException("Tray icon stream must be ICO or PNG on Windows.");

        var width = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        var height = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
        var w = (byte)(width >= 256 ? 0 : width);
        var h = (byte)(height >= 256 ? 0 : height);

        var ico = new byte[6 + 16 + png.Length];
        ico[2] = 1;
        ico[4] = 1;
        ico[6] = w;
        ico[7] = h;
        ico[10] = 1;
        ico[12] = 32;
        var size = png.Length;
        ico[14] = (byte)(size & 0xFF);
        ico[15] = (byte)((size >> 8) & 0xFF);
        ico[16] = (byte)((size >> 16) & 0xFF);
        ico[17] = (byte)((size >> 24) & 0xFF);
        ico[18] = 22;
        Buffer.BlockCopy(png, 0, ico, 22, png.Length);
        return ico;
    }

    protected override void OnTooltipChanged(string? value)
    {
        var data = this.BuildData(NIF_TIP | NIF_SHOWTIP);
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    protected override void OnTitleChanged(string? value) { }

    protected override void OnVisibilityChanged(bool visible)
    {
        var data = this.BuildData(NIF_STATE);
        data.dwStateMask = NIS_HIDDEN;
        data.dwState = visible ? 0 : NIS_HIDDEN;
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    public override void ShowNotification(string title, string message)
    {
        var data = this.BuildData(NIF_INFO);
        data.szInfo = message ?? string.Empty;
        data.szInfoTitle = title ?? string.Empty;
        data.dwInfoFlags = NIIF_INFO;
        Shell_NotifyIcon(NIM_MODIFY, ref data);
    }

    protected override void OnMenuChanged(object? sender, EventArgs e)
    {
        this.ReleaseMenuResources();
        this.UnregisterAllHotKeys();
    }

    void ReleaseMenuResources()
    {
        if (this.popupMenu != IntPtr.Zero)
            DestroyMenu(this.popupMenu);
        this.popupMenu = IntPtr.Zero;
        this.menuActions = null;
        foreach (var hb in this.menuBitmaps)
            DeleteObject(hb);
        this.menuBitmaps.Clear();
        foreach (var row in this.ownerDrawRows)
            row.Dispose();
        this.ownerDrawRows.Clear();
    }

    void UnregisterAllHotKeys()
    {
        lock (this.hotKeyActions)
        {
            foreach (var hkId in this.hotKeyActions.Keys)
                UnregisterHotKey(this.hwnd, hkId);
            this.hotKeyActions.Clear();
        }
    }

    public override void ShowMenu()
    {
        if (this.Menu == null) return;
        if (this.popupMenu == IntPtr.Zero)
            this.BuildPopupMenu();
        if (this.popupMenu == IntPtr.Zero) return;

        if (this.menuTheme == IntPtr.Zero)
            this.menuTheme = OpenThemeData(this.hwnd, "MENU");

        GetCursorPos(out var pt);
        SetForegroundWindow(this.hwnd);
        this.menuOpen = true;
        int cmd;
        try
        {
            cmd = TrackPopupMenuEx(this.popupMenu, TPM_RIGHTBUTTON | TPM_RETURNCMD, pt.X, pt.Y, this.hwnd, IntPtr.Zero);
        }
        finally
        {
            this.menuOpen = false;
        }
        if (cmd != 0 && this.menuActions != null && this.menuActions.TryGetValue(cmd, out var action))
            action();
        PostMessageW(this.hwnd, 0, IntPtr.Zero, IntPtr.Zero);
    }

    void BuildPopupMenu()
    {
        if (this.Menu == null) return;
        this.popupMenu = CreatePopupMenu();
        this.menuActions = new();
        var nextId = 1000;
        this.AppendItems(this.popupMenu, this.Menu.Items, this.menuActions, ref nextId);
    }

    void AppendItems(IntPtr menu, IEnumerable<TrayMenuItemBase> items, Dictionary<int, Action> actions, ref int nextId)
    {
        foreach (var item in items)
        {
            if (!item.IsVisible) continue;
            switch (item)
            {
                case TraySeparator:
                    AppendMenu(menu, MF_SEPARATOR, UIntPtr.Zero, null);
                    break;
                case TraySubmenu sub:
                {
                    var child = CreatePopupMenu();
                    this.AppendItems(child, sub.Items, actions, ref nextId);
                    var flags = MF_POPUP | (sub.IsEnabled ? 0 : MF_GRAYED);
                    AppendMenu(menu, flags, (UIntPtr)(ulong)child.ToInt64(), sub.Label);
                    break;
                }
                case TrayCheckMenuItem check:
                {
                    var id = nextId++;
                    var flags = MF_STRING | (check.IsEnabled ? 0 : MF_GRAYED) | (check.IsChecked ? MF_CHECKED : 0);
                    AppendMenu(menu, flags, (UIntPtr)id, FormatLabel(check.Label, null));
                    actions[id] = () => check.RaiseToggled(!check.IsChecked);
                    break;
                }
                case TrayProgressItem progress:
                {
                    // informational: disabled so it can't be chosen, drawn in full colour regardless
                    var id = nextId++;
                    this.ownerDrawRows.Add(new WindowsTrayOwnerDraw.Row(progress, null));
                    AppendMenuOwnerDraw(menu, MF_OWNERDRAW | MF_DISABLED, (UIntPtr)id, (IntPtr)this.ownerDrawRows.Count);
                    break;
                }
                case TrayMenuItem { Pill: not null } pilled:
                {
                    // Win32 has no way to put anything after a menu label, so the whole row is drawn
                    var id = nextId++;
                    System.Drawing.Image? rowIcon = null;
                    if (pilled.Icon != null)
                    {
                        try
                        {
                            using var src = pilled.Icon();
                            using var ms = new MemoryStream();
                            src.CopyTo(ms);
                            ms.Position = 0;
                            using var decoded = SDImage.FromStream(ms);
                            rowIcon = new SDBitmap(decoded);
                        }
                        catch { /* best-effort */ }
                    }
                    this.ownerDrawRows.Add(new WindowsTrayOwnerDraw.Row(pilled, rowIcon));
                    var flags = MF_OWNERDRAW | (pilled.IsEnabled ? 0 : MF_GRAYED);
                    AppendMenuOwnerDraw(menu, flags, (UIntPtr)id, (IntPtr)this.ownerDrawRows.Count);
                    actions[id] = () => pilled.RaiseClicked();
                    if (!string.IsNullOrEmpty(pilled.Accelerator) && pilled.IsEnabled)
                        this.TryRegisterHotKey(pilled.Accelerator, () => pilled.RaiseClicked());
                    break;
                }
                case TrayMenuItem mi:
                {
                    var id = nextId++;
                    var flags = MF_STRING | (mi.IsEnabled ? 0 : MF_GRAYED);
                    AppendMenu(menu, flags, (UIntPtr)id, FormatLabel(mi.Label, mi.Accelerator));
                    actions[id] = () => mi.RaiseClicked();

                    if (mi.Icon != null)
                    {
                        try
                        {
                            var hbmp = LoadMenuHBitmapFromStream(mi.Icon());
                            if (hbmp != IntPtr.Zero)
                            {
                                var mii = new MENUITEMINFO
                                {
                                    cbSize = (uint)Marshal.SizeOf<MENUITEMINFO>(),
                                    fMask = MIIM_BITMAP,
                                    hbmpItem = hbmp
                                };
                                SetMenuItemInfo(menu, (uint)id, false, ref mii);
                                this.menuBitmaps.Add(hbmp);
                            }
                        }
                        catch { /* best-effort */ }
                    }

                    if (!string.IsNullOrEmpty(mi.Accelerator) && mi.IsEnabled)
                        this.TryRegisterHotKey(mi.Accelerator, () => mi.RaiseClicked());

                    break;
                }
            }
        }
    }

    void TryRegisterHotKey(string acceleratorString, Action callback)
    {
        var accel = TrayAccelerator.Parse(acceleratorString);
        if (accel == null) return;
        var vk = MapVirtualKey(accel.Key);
        if (vk == 0) return;

        uint mods = 0;
        if ((accel.Modifiers & TrayAcceleratorModifiers.Control) != 0) mods |= MOD_CONTROL;
        if ((accel.Modifiers & TrayAcceleratorModifiers.Alt) != 0) mods |= MOD_ALT;
        if ((accel.Modifiers & TrayAcceleratorModifiers.Shift) != 0) mods |= MOD_SHIFT;
        if ((accel.Modifiers & TrayAcceleratorModifiers.Meta) != 0) mods |= MOD_WIN;

        var hkId = Interlocked.Increment(ref NextHotKeyId);
        if (RegisterHotKey(this.hwnd, hkId, mods, vk))
        {
            lock (this.hotKeyActions) this.hotKeyActions[hkId] = callback;
        }
    }

    static uint MapVirtualKey(string key)
    {
        if (key.Length == 1)
        {
            var c = char.ToUpperInvariant(key[0]);
            if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) return c;
        }
        if (key.Length >= 2 && (key[0] == 'F' || key[0] == 'f') && int.TryParse(key.AsSpan(1), out var fn) && fn >= 1 && fn <= 24)
            return (uint)(0x70 + fn - 1);

        return key.ToLowerInvariant() switch
        {
            "esc" or "escape" => 0x1B,
            "enter" or "return" => 0x0D,
            "tab" => 0x09,
            "space" or "spacebar" => 0x20,
            "back" or "backspace" => 0x08,
            "delete" or "del" => 0x2E,
            "insert" or "ins" => 0x2D,
            "home" => 0x24,
            "end" => 0x23,
            "pgup" or "pageup" => 0x21,
            "pgdn" or "pagedown" => 0x22,
            "left" => 0x25,
            "up" => 0x26,
            "right" => 0x27,
            "down" => 0x28,
            _ => 0
        };
    }

    static string FormatLabel(string label, string? accelerator)
        => string.IsNullOrEmpty(accelerator) ? label : label + "\t" + accelerator;

    public override void Dispose()
    {
        if (this.disposed) return;
        this.disposed = true;
        base.Dispose();
        this.UnregisterAllHotKeys();
        if (this.added)
        {
            var data = this.BuildData(0);
            Shell_NotifyIcon(NIM_DELETE, ref data);
        }
        this.ReleaseMenuResources();
        if (this.menuTheme != IntPtr.Zero) CloseThemeData(this.menuTheme);
        if (this.hIcon != IntPtr.Zero) DestroyIcon(this.hIcon);
        lock (Instances) Instances.Remove(this.hwnd);
        DestroyWindow(this.hwnd);
    }
}
