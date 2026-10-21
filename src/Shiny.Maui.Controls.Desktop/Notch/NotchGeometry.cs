#if !ANDROID && !IOS && !MACCATALYST
namespace Shiny.Maui.Controls.Desktop.Notch;

/// <summary>The camera notch on the screen the notch window uses, in points.</summary>
/// <param name="HasNotch">Whether that screen has a notch at all.</param>
/// <param name="Width">The cut-out's width, between the two menu-bar areas either side of it.</param>
/// <param name="Height">The cut-out's height — the menu bar's height on a notched display.</param>
readonly record struct NotchGeometry(bool HasNotch, double Width, double Height)
{
    public static NotchGeometry None => new(false, 0, 0);
}
#endif
