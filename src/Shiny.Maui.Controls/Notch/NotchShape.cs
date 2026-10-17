namespace Shiny.Maui.Controls;

/// <summary>The outline a <see cref="NotchView"/> draws.</summary>
public enum NotchShape
{
    /// <summary>
    /// A free-floating pill with every corner rounded — the Dynamic Island look. Use it when the
    /// notch sits a little below the top of the page.
    /// </summary>
    Island,

    /// <summary>
    /// Square top corners and rounded bottom ones, so the surface reads as hanging from the edge it
    /// is placed against — the MacBook camera notch look. Use it when the notch is flush with the top
    /// of the page or the screen.
    /// </summary>
    Notch
}
