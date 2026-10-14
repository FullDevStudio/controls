using AVFoundation;

namespace Shiny.Maui.Controls.Media;

/// <summary>
/// Implemented by the per-platform video view (a <c>UIView</c> on iOS/Catalyst, an <c>NSView</c> on
/// macOS) so the one shared AVFoundation backend can reach the layer it has to drive, without the
/// backend needing to know which UI framework it's running under.
/// </summary>
interface IApplePlayerOutput
{
    /// <summary>The layer that renders the video, and that Picture-in-Picture attaches to.</summary>
    AVPlayerLayer PlayerLayer { get; }

    /// <summary>
    /// Whether the backend should offer Picture-in-Picture from this layer. <c>false</c> for VideoText,
    /// whose video is decoration masked to glyphs rather than something to float over other apps.
    /// </summary>
    bool SupportsPictureInPicture => true;
}
