using System.Runtime.InteropServices;

namespace Shiny.Maui.Controls.Desktop.TrayIcon;

/// <summary>
/// P/Invoke into libayatana-appindicator3 and GTK 3. Requires the user to install:
/// libayatana-appindicator3-1 (Debian/Ubuntu) or libayatana-appindicator3 (Fedora/Arch)
/// plus libgtk-3-0. libnotify is optional — ShowNotification fails silently if missing.
/// </summary>
static partial class LinuxInterop
{
    const string AppIndicator = "libayatana-appindicator3.so.1";
    const string Gtk = "libgtk-3.so.0";
    const string GObject = "libgobject-2.0.so.0";
    const string GLib = "libglib-2.0.so.0";
    const string Notify = "libnotify.so.4";

    public enum AppIndicatorCategory
    {
        ApplicationStatus = 0,
        Communications,
        SystemServices,
        Hardware,
        Other
    }

    public enum AppIndicatorStatus
    {
        Passive = 0,
        Active,
        Attention
    }

    [Flags]
    public enum GdkModifierType : uint
    {
        ShiftMask = 1 << 0,
        ControlMask = 1 << 2,
        Mod1Mask = 1 << 3, // Alt
        SuperMask = 1 << 26
    }

    [Flags]
    public enum GtkAccelFlags : uint
    {
        Visible = 1 << 0,
        Locked = 1 << 1
    }

    [LibraryImport(AppIndicator, EntryPoint = "app_indicator_new", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr AppIndicatorNew(string id, string iconName, int category);

    [LibraryImport(AppIndicator, EntryPoint = "app_indicator_set_status")]
    public static partial void AppIndicatorSetStatus(IntPtr self, int status);

    [LibraryImport(AppIndicator, EntryPoint = "app_indicator_set_icon_full", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void AppIndicatorSetIconFull(IntPtr self, string iconName, string iconDesc);

    [LibraryImport(AppIndicator, EntryPoint = "app_indicator_set_title", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void AppIndicatorSetTitle(IntPtr self, string title);

    [LibraryImport(AppIndicator, EntryPoint = "app_indicator_set_label", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void AppIndicatorSetLabel(IntPtr self, string label, string guide);

    [LibraryImport(AppIndicator, EntryPoint = "app_indicator_set_menu")]
    public static partial void AppIndicatorSetMenu(IntPtr self, IntPtr menu);

    [LibraryImport(AppIndicator, EntryPoint = "app_indicator_set_secondary_activate_target")]
    public static partial void AppIndicatorSetSecondaryActivateTarget(IntPtr self, IntPtr menuItem);

    [LibraryImport(Gtk, EntryPoint = "gtk_init_check")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool GtkInitCheck(ref int argc, ref IntPtr argv);

    [LibraryImport(Gtk, EntryPoint = "gtk_main_iteration")]
    public static partial void GtkMainIteration();

    [LibraryImport(Gtk, EntryPoint = "gtk_events_pending")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool GtkEventsPending();

    [LibraryImport(Gtk, EntryPoint = "gtk_menu_new")]
    public static partial IntPtr GtkMenuNew();

    [LibraryImport(Gtk, EntryPoint = "gtk_menu_item_new_with_label", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr GtkMenuItemNewWithLabel(string label);

    [LibraryImport(Gtk, EntryPoint = "gtk_check_menu_item_new_with_label", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr GtkCheckMenuItemNewWithLabel(string label);

    [LibraryImport(Gtk, EntryPoint = "gtk_check_menu_item_set_active")]
    public static partial void GtkCheckMenuItemSetActive(IntPtr item, [MarshalAs(UnmanagedType.U1)] bool active);

    [LibraryImport(Gtk, EntryPoint = "gtk_check_menu_item_get_active")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool GtkCheckMenuItemGetActive(IntPtr item);

    [LibraryImport(Gtk, EntryPoint = "gtk_separator_menu_item_new")]
    public static partial IntPtr GtkSeparatorMenuItemNew();

    [LibraryImport(Gtk, EntryPoint = "gtk_menu_item_set_submenu")]
    public static partial void GtkMenuItemSetSubmenu(IntPtr item, IntPtr submenu);

    [LibraryImport(Gtk, EntryPoint = "gtk_menu_shell_append")]
    public static partial void GtkMenuShellAppend(IntPtr shell, IntPtr child);

    [LibraryImport(Gtk, EntryPoint = "gtk_widget_show_all")]
    public static partial void GtkWidgetShowAll(IntPtr widget);

    [LibraryImport(Gtk, EntryPoint = "gtk_widget_show")]
    public static partial void GtkWidgetShow(IntPtr widget);

    [LibraryImport(Gtk, EntryPoint = "gtk_widget_set_sensitive")]
    public static partial void GtkWidgetSetSensitive(IntPtr widget, [MarshalAs(UnmanagedType.U1)] bool sensitive);

    // Deprecated since GTK 3.10 but still functional — used for menu item icons.
    [LibraryImport(Gtk, EntryPoint = "gtk_image_menu_item_new_with_label", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr GtkImageMenuItemNewWithLabel(string label);

    [LibraryImport(Gtk, EntryPoint = "gtk_image_menu_item_set_image")]
    public static partial void GtkImageMenuItemSetImage(IntPtr menuItem, IntPtr image);

    [LibraryImport(Gtk, EntryPoint = "gtk_image_menu_item_set_always_show_image")]
    public static partial void GtkImageMenuItemSetAlwaysShowImage(IntPtr menuItem, [MarshalAs(UnmanagedType.U1)] bool alwaysShow);

    [LibraryImport(Gtk, EntryPoint = "gtk_image_new_from_file", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr GtkImageNewFromFile(string filename);

    [LibraryImport(Gtk, EntryPoint = "gtk_accel_group_new")]
    public static partial IntPtr GtkAccelGroupNew();

    [LibraryImport(Gtk, EntryPoint = "gtk_widget_add_accelerator", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void GtkWidgetAddAccelerator(IntPtr widget, string accelSignal, IntPtr accelGroup, uint accelKey, uint accelMods, uint accelFlags);

    [LibraryImport(Gtk, EntryPoint = "gtk_menu_set_accel_group")]
    public static partial void GtkMenuSetAccelGroup(IntPtr menu, IntPtr accelGroup);

    [LibraryImport(Gtk, EntryPoint = "gdk_keyval_from_name", StringMarshalling = StringMarshalling.Utf8)]
    public static partial uint GdkKeyvalFromName(string name);

    [LibraryImport(GObject, EntryPoint = "g_signal_connect_data", StringMarshalling = StringMarshalling.Utf8)]
    public static partial ulong GSignalConnectData(IntPtr instance, string signalName, IntPtr handler, IntPtr data, IntPtr destroyData, int connectFlags);

    [LibraryImport(GObject, EntryPoint = "g_object_ref_sink")]
    public static partial IntPtr GObjectRefSink(IntPtr obj);

    [LibraryImport(Gtk, EntryPoint = "gtk_menu_item_set_label", StringMarshalling = StringMarshalling.Utf8)]
    public static partial void GtkMenuItemSetLabel(IntPtr menuItem, string label);

    [LibraryImport(GLib, EntryPoint = "g_idle_add")]
    public static partial uint GIdleAdd(IntPtr function, IntPtr data);

    [LibraryImport(GObject, EntryPoint = "g_object_unref")]
    public static partial void GObjectUnref(IntPtr obj);

    [LibraryImport(GLib, EntryPoint = "g_main_context_iteration")]
    public static partial int GMainContextIteration(IntPtr ctx, [MarshalAs(UnmanagedType.U1)] bool mayBlock);

    // libnotify — optional, ShowNotification gracefully no-ops if missing.
    [LibraryImport(Notify, EntryPoint = "notify_init", StringMarshalling = StringMarshalling.Utf8)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool NotifyInit(string appName);

    [LibraryImport(Notify, EntryPoint = "notify_is_initted")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool NotifyIsInitted();

    [LibraryImport(Notify, EntryPoint = "notify_notification_new", StringMarshalling = StringMarshalling.Utf8)]
    public static partial IntPtr NotifyNotificationNew(string summary, string body, string? iconName);

    [LibraryImport(Notify, EntryPoint = "notify_notification_show")]
    [return: MarshalAs(UnmanagedType.U1)]
    public static partial bool NotifyNotificationShow(IntPtr notification, IntPtr error);
}
