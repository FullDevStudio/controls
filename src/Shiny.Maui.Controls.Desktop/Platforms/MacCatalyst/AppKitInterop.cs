using System.Runtime.InteropServices;
using ObjCRuntime;

namespace Shiny.Maui.Controls.Desktop.TrayIcon;

/// <summary>
/// Thin bridge to AppKit from MacCatalyst. Loads AppKit on first access, then performs
/// every operation through objc_msgSend so we don't depend on AppKit bindings (which aren't
/// available to MacCatalyst projects).
/// </summary>
static partial class AppKitInterop
{
    const string Libobjc = "/usr/lib/libobjc.dylib";
    const string Libdl = "/usr/lib/libSystem.dylib";

    [LibraryImport(Libdl, EntryPoint = "dlopen", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr DlOpen(string path, int mode);

    [LibraryImport(Libobjc, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr GetClass(string name);

    [LibraryImport(Libobjc, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr Sel(string name);

    [LibraryImport(Libobjc, EntryPoint = "objc_allocateClassPair", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr AllocateClassPair(IntPtr superclass, string name, IntPtr extraBytes);

    [LibraryImport(Libobjc, EntryPoint = "objc_registerClassPair")]
    public static partial void RegisterClassPair(IntPtr cls);

    [LibraryImport(Libobjc, EntryPoint = "class_addMethod", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool AddMethod(IntPtr cls, IntPtr sel, IntPtr imp, string types);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static partial IntPtr MsgSend(IntPtr receiver, IntPtr selector);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static partial IntPtr MsgSend(IntPtr receiver, IntPtr selector, IntPtr arg1);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static partial IntPtr MsgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static partial IntPtr MsgSend(IntPtr receiver, IntPtr selector, IntPtr arg1, IntPtr arg2, IntPtr arg3);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static partial IntPtr MsgSendDouble(IntPtr receiver, IntPtr selector, double arg1);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static partial void MsgSendBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.U1)] bool arg1);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static partial void MsgSendLong(IntPtr receiver, IntPtr selector, long arg1);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static partial void MsgSendULong(IntPtr receiver, IntPtr selector, ulong arg1);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static partial void MsgSendCGSize(IntPtr receiver, IntPtr selector, NSSize size);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool MsgSendBoolReturn(IntPtr receiver, IntPtr selector);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static partial IntPtr MsgSendRect(IntPtr receiver, IntPtr selector, NSRect rect);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    public static partial IntPtr MsgSendPtrSize(IntPtr receiver, IntPtr selector, IntPtr arg1, NSSize size);

    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend")]
    private static partial NSRect MsgSendRectReturn(IntPtr receiver, IntPtr selector);

    // x86_64 returns a struct this size through a hidden pointer, so it needs the _stret entry;
    // arm64 has no _stret variant and returns it in registers through plain objc_msgSend.
    [LibraryImport(Libobjc, EntryPoint = "objc_msgSend_stret")]
    private static partial void MsgSendRectStret(out NSRect result, IntPtr receiver, IntPtr selector);

    public static NSRect GetRect(IntPtr receiver, IntPtr selector)
    {
        if (RuntimeInformation.ProcessArchitecture == Architecture.X64)
        {
            MsgSendRectStret(out var r, receiver, selector);
            return r;
        }
        return MsgSendRectReturn(receiver, selector);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NSSize
    {
        public double Width;
        public double Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct NSRect
    {
        public double X;
        public double Y;
        public double Width;
        public double Height;

        public NSRect(double x, double y, double width, double height)
        {
            this.X = x;
            this.Y = y;
            this.Width = width;
            this.Height = height;
        }
    }

    static bool initialized;
    public static void EnsureLoaded()
    {
        if (initialized) return;
        DlOpen("/System/Library/Frameworks/AppKit.framework/AppKit", 2);
        initialized = true;
    }

    public static IntPtr NSString(string s)
    {
        var nsStr = GetClass("NSString");
        var sel = Sel("stringWithUTF8String:");
        var ptr = Marshal.StringToHGlobalAnsi(s);
        try { return MsgSend(nsStr, sel, ptr); }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    public static IntPtr NSDataFromBytes(byte[] bytes)
    {
        var nsData = GetClass("NSData");
        var sel = Sel("dataWithBytes:length:");
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return MsgSend(nsData, sel, handle.AddrOfPinnedObject(), (IntPtr)bytes.Length);
        }
        finally { handle.Free(); }
    }
}
