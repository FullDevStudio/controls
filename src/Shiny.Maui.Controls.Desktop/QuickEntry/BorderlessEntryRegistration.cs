using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;

namespace Shiny.Maui.Controls.Desktop.QuickEntry;

/// <summary>
/// Strips the native text field's chrome off every <c>BorderlessEntry</c> on the heads core cannot
/// reach: AppKit (border, bezel, fill, focus ring), GTK4 (frame and background) and Windows.
/// </summary>
/// <remarks>
/// <para>
/// A module initializer rather than a builder call, as <c>MotionIconRegistration</c> is: a
/// <c>BorderlessEntry</c> is inside <c>EntryCell</c>, <c>PromptView</c> and the rest, so drawing it
/// without a box is not a feature anyone should have to opt into. It was set only by
/// <c>UseDesktopQuickEntry</c>, which left every table's <c>EntryCell</c> on macOS a full bezelled
/// text field inside its row. Any app with this package installed loads it through one of its
/// <c>Use…</c> calls before the first page exists. On the phones and Catalyst the platform's
/// <c>PolishEntry</c> does nothing - core's mappers cover them.
/// </para>
/// <para>
/// <b>AppKit also needs its handler's mapper.</b> Its <c>EntryHandler</c> cannot make an
/// <c>NSTextField</c> secure, so <c>MapIsPassword</c> throws the field away and puts a new, fully
/// bezelled <c>NSSecureTextField</c> in its place - and it runs after <c>HandlerChanged</c>, with
/// nothing raised on the element. Measured: an <c>EntryCell</c> with <c>IsPassword</c> reached
/// <c>HandlerChanged</c> still holding the plain field, and drew the one box left on the table. So
/// the polish is appended to that mapper key, which runs after the swap every time it happens.
/// </para>
/// </remarks>
static class BorderlessEntryRegistration
{
#pragma warning disable CA2255 // Deliberate: see the remarks.
    [ModuleInitializer]
#pragma warning restore CA2255
    internal static void Register()
    {
        BorderlessEntryPolish.Handler ??= QuickEntryPlatform.PolishEntry;
#if MACOS
        PolishAfterPasswordSwap();
#endif
    }

#if MACOS
    // The AppKit head is the app's package, not this one's - the same reason the GTK interop finds the
    // GTK head's types by name. The mapper is reached through MAUI's own IPropertyMapper, which is
    // covariant in its handler, so nothing of the head's is referenced at compile time.
    const string AppKitEntryHandler = "Microsoft.Maui.Platforms.MacOS.Handlers.EntryHandler, Microsoft.Maui.Platforms.MacOS";

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "Mapper is public API of the AppKit head's EntryHandler, which the head itself reads to build every entry.")]
    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Mapper is public API of the AppKit head's EntryHandler, which the head itself reads to build every entry.")]
    [UnconditionalSuppressMessage("Trimming", "IL2057", Justification = "The type name is a constant; the head is always present on this TFM's app.")]
    static void PolishAfterPasswordSwap()
    {
        var mapper = Type.GetType(AppKitEntryHandler, throwOnError: false)
            ?.GetField("Mapper", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?.GetValue(null) as IPropertyMapper<IEntry, IElementHandler>;

        mapper?.AppendToMapping(nameof(IEntry.IsPassword), (handler, entry) =>
        {
            if (entry is not BorderlessEntry)
                return;

            BorderlessEntryPolish.Apply(handler.PlatformView);

            // The swap carries over the frame, font, text colour and text and nothing else, so the
            // new field had no placeholder and sat left-aligned in a row whose other values sit
            // right. Mapping those again puts them on the field that is now on screen.
            foreach (var key in SwapDrops)
                handler.UpdateValue(key);
        });
    }

    static readonly string[] SwapDrops =
    [
        nameof(IEntry.Placeholder),
        nameof(IEntry.PlaceholderColor),
        nameof(IEntry.HorizontalTextAlignment),
        nameof(IEntry.CharacterSpacing),
        nameof(IEntry.IsReadOnly),
        nameof(IEntry.MaxLength)
    ];
#endif
}
