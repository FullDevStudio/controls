#if MACOS
namespace Shiny.Maui.Controls.Desktop.Notch;

/// <summary>
/// Puts a <see cref="NotchView"/> in the MacBook camera notch — in a borderless window above the menu
/// bar and every other application — and grows it out of the notch to fit whatever content you give it.
/// </summary>
/// <remarks>
/// <para>
/// On a Mac whose display has a notch, the empty notch is exactly the size of the hardware cut-out, so
/// it disappears into it; give it content and it extends down out of the notch. On a display without
/// one it is a floating island centred under the menu bar.
/// </para>
/// <para>
/// macOS AppKit (<c>net10.0-macos</c>) only — the type does not exist on any other target, so shared
/// code guards it with <c>#if MACOS</c>. Elsewhere, put an in-app <see cref="NotchView"/> on the page.
/// </para>
/// <para>
/// The window never takes keyboard focus (a borderless window cannot become key), so the content is for
/// glancing and clicking — status, media controls, a timer, a progress readout — not for typing.
/// </para>
/// </remarks>
public interface INotchWindowService
{
    /// <summary>True when the screen the notch is shown on has a camera notch.</summary>
    bool HasHardwareNotch { get; }

    /// <summary>Whether the notch window is on screen.</summary>
    bool IsVisible { get; }

    /// <summary>The content currently in the notch.</summary>
    View? Content { get; }

    /// <summary>Raised when the notch surface (not a button inside the content) is clicked.</summary>
    event EventHandler? Tapped;

    /// <summary>Shows the notch, optionally with new content. Showing with no content shows the empty notch.</summary>
    Task ShowAsync(View? content = null);

    /// <summary>
    /// Replaces the content and morphs to fit it. Content that only changes its own size needs no call —
    /// the notch follows it.
    /// </summary>
    Task SetContentAsync(View? content);

    /// <summary>Takes the notch off screen. The window is kept, so the next show is instant.</summary>
    void Hide();
}
#endif
