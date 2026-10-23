#if !(ANDROID || IOS || MACCATALYST || WINDOWS || MACOS)
using Shiny.Maui.Controls.Camera.Video;

namespace Shiny.Maui.Controls.Camera;

// The platform-neutral TFM has no camera and no decoder: everything reports unsupported, and the encoder
// produces nothing unless a session factory is supplied (the unit tests do).
public partial class VideoEncoder
{
    private static partial bool PlatformIsSupported(VideoCodec codec) => false;

    private static partial IVideoEncoderSession? CreatePlatformSession(
        VideoEncoderSettings settings, int width, int height, Action<EncodedVideoFrame> sink) => null;
}


public partial class RemoteVideoView
{
    private static partial bool PlatformCanDecode(VideoCodec codec) => false;
}
#endif
