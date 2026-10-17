#if MACOS
namespace Shiny.Maui.Controls.Desktop.Notch;

/// <summary>Settings for <see cref="INotchWindowService"/>. Pass them to <c>UseDesktopNotch</c>.</summary>
public sealed class NotchWindowOptions
{
    /// <summary>
    /// Grow out of the camera notch when the screen has one. When false — or when there is no notch — the
    /// notch is a floating island below the menu bar instead.
    /// </summary>
    public bool UseHardwareNotch { get; set; } = true;

    /// <summary>Gap between the menu bar and a floating island, in points. Unused when hugging the hardware notch.</summary>
    public double TopMargin { get; set; } = 8;

    /// <summary>The widest the notch grows, in points.</summary>
    public double MaxWidth { get; set; } = 640;

    /// <summary>The tallest the notch grows, in points.</summary>
    public double MaxHeight { get; set; } = 480;

    /// <summary>How long the window takes to reshape, in milliseconds.</summary>
    public double MorphDuration { get; set; } = 300;

    /// <summary>Keep the notch on every Space and over full-screen apps.</summary>
    public bool JoinAllSpaces { get; set; } = true;

    /// <summary>
    /// Styles each <see cref="NotchView"/> the service creates — colour, padding, corner radius. Runs
    /// after the service has applied its own geometry, so anything set here wins.
    /// </summary>
    public Action<NotchView>? Configure { get; set; }
}
#endif
