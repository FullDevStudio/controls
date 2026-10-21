using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Shiny.Maui.Controls.Desktop.Notch;

/// <summary>The Win32 the notch window needs on top of what the quick entry popup already declares.</summary>
[SupportedOSPlatform("windows")]
static partial class NotchInterop
{
    public const int GWL_STYLE = -16;
    public const long WS_CAPTION = 0x00C00000;
    public const long WS_THICKFRAME = 0x00040000;
    public const long WS_SYSMENU = 0x00080000;
    public const long WS_POPUP = 0x80000000;

    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_FRAMECHANGED = 0x0020;

    public const uint MONITOR_DEFAULTTOPRIMARY = 1;

    /// <summary>DWMWCP_DONOTROUND: the window region is the outline, so DWM must not round it a second time.</summary>
    public const int DWMWCP_DONOTROUND = 1;

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    [LibraryImport("gdi32.dll")]
    public static partial IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(IntPtr obj);

    /// <summary>On success the system owns the region; the caller must not delete it.</summary>
    [LibraryImport("user32.dll")]
    public static partial int SetWindowRgn(IntPtr hwnd, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetWindowRect(IntPtr hwnd, out QuickEntry.QuickEntryInterop.RECT rect);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsWindowVisible(IntPtr hwnd);
}
