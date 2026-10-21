namespace Shiny.Maui.Controls.Desktop.QuickEntry;

/// <summary>
/// Installs the one stylesheet every Linux overlay window needs. GTK paints a window's own background
/// before any content, so a MAUI page with a transparent background is not enough on its own — the
/// toplevel has to be told to paint nothing. It also carries the rule that takes the frame off a
/// <c>BorderlessEntry</c>, installed by the first one to get a handler.
/// </summary>
static class Gtk4Styling
{
    const string Css = """
        window.shiny-quick-entry,
        window.shiny-quick-entry > *,
        window.shiny-screen-glow,
        window.shiny-screen-glow > *,
        window.shiny-notch,
        window.shiny-notch > * { background-color: transparent; }

        window.shiny-quick-entry entry,
        window.shiny-quick-entry text {
            background-color: transparent;
            background-image: none;
            border: none;
            box-shadow: none;
            outline: none;
        }

        entry.shiny-borderless,
        entry.shiny-borderless text {
            background-color: transparent;
            background-image: none;
            border: none;
            box-shadow: none;
            outline: none;
        }
        """;

    /// <summary>The class <c>QuickEntryPlatform.PolishEntry</c> puts on every BorderlessEntry's widget.</summary>
    public const string BorderlessEntryClass = "shiny-borderless";

    static bool applied;

    public static void EnsureCss(IntPtr widget)
    {
        if (applied)
            return;

        var display = Gtk4Interop.WidgetGetDisplay(widget);
        if (display == IntPtr.Zero)
            return;

        var provider = Gtk4Interop.CssProviderNew();
        try
        {
            Gtk4Interop.CssProviderLoadFromString(provider, Css);
        }
        catch (EntryPointNotFoundException)
        {
            // gtk_css_provider_load_from_string arrived in GTK 4.12; -1 means NUL-terminated.
            Gtk4Interop.CssProviderLoadFromData(provider, Css, (IntPtr)(-1));
        }

        Gtk4Interop.StyleContextAddProviderForDisplay(display, provider, Gtk4Interop.StyleProviderPriorityApplication);
        applied = true;
    }
}
