namespace Shiny.Maui.Controls.Desktop.QuickEntry;

/// <summary>
/// Installs the one stylesheet every Linux overlay window needs. GTK paints a window's own background
/// before any content, so a MAUI page with a transparent background is not enough on its own — the
/// toplevel has to be told to paint nothing.
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
        """;

    static bool applied;

    public static void EnsureCss(IntPtr window)
    {
        if (applied)
            return;

        var display = Gtk4Interop.WidgetGetDisplay(window);
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
