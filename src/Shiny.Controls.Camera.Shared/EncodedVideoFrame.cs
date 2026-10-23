namespace Shiny.Controls.Camera;

/// <summary>How a live video frame is compressed. See <c>VideoEncoder</c> and <c>RemoteVideoView</c>.</summary>
public enum VideoCodec
{
    /// <summary>
    /// Every frame a self-contained JPEG. Larger than H.264 at the same quality, but every frame is a key frame,
    /// so a lost frame costs nothing and decoding can start anywhere. Supported on every platform.
    /// </summary>
    Jpeg,

    /// <summary>
    /// H.264 (AVC) in Annex B form: NAL units separated by <c>00 00 00 01</c> start codes. Every key frame
    /// carries its SPS and PPS in-band, so a receiver can start decoding from any key frame. Delta frames
    /// depend on the frames before them, so after a lost frame the receiver waits for (and asks for) the next
    /// key frame.
    /// </summary>
    H264
}


/// <summary>
/// One encoded frame of live video, as produced by <c>VideoEncoder.FrameEncoded</c> and consumed by
/// <c>RemoteVideoView.Render</c>. Everything a receiver needs to draw it is on the record, so it can be carried
/// over any transport field by field.
/// </summary>
/// <remarks>
/// Frames are encoded <b>un-rotated</b>: <see cref="Width"/> x <see cref="Height"/> is the encoded image as
/// the sensor produced it (scaled down), and the receiver applies <see cref="Rotation"/> and then
/// <see cref="IsMirrored"/>, the same order <see cref="CoordinateTransform.ApplyOrientation(Microsoft.Maui.Graphics.RectF,int,bool)"/>
/// uses. A phone turned from portrait to landscape therefore changes these two values, not the encoded size.
/// </remarks>
/// <param name="Codec">How <paramref name="Data"/> is compressed.</param>
/// <param name="Data">
/// The compressed frame: a complete JPEG file, or one H.264 access unit in Annex B form (a key frame starts
/// with its SPS and PPS).
/// </param>
/// <param name="Width">Encoded width in pixels, before <paramref name="Rotation"/> is applied.</param>
/// <param name="Height">Encoded height in pixels, before <paramref name="Rotation"/> is applied.</param>
/// <param name="Rotation">Clockwise degrees (0, 90, 180 or 270) to rotate the image so the scene is upright.</param>
/// <param name="IsMirrored">Mirror the image horizontally after rotating it (front camera).</param>
/// <param name="IsKeyFrame">
/// The frame decodes on its own. Always <c>true</c> for <see cref="VideoCodec.Jpeg"/>; for
/// <see cref="VideoCodec.H264"/> an IDR access unit with its parameter sets.
/// </param>
/// <param name="Timestamp">When the frame was captured, relative to the first frame of the encoder session.</param>
public sealed record EncodedVideoFrame(
    VideoCodec Codec,
    ReadOnlyMemory<byte> Data,
    int Width,
    int Height,
    int Rotation,
    bool IsMirrored,
    bool IsKeyFrame,
    TimeSpan Timestamp
);
