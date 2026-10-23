using Shiny.Maui.Controls.Camera.Video;

namespace Shiny.Maui.Controls.Camera;

/// <summary>
/// Draws live video received from the other side of a call: the <see cref="EncodedVideoFrame"/>s a
/// <see cref="VideoEncoder"/> produced, after the app has carried them across. Hand each one to
/// <see cref="Render"/> as it arrives; the view decodes it, turns it upright (rotation, then mirroring) and
/// fits it to its bounds by <see cref="Aspect"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Keeping H.264 in sync is the view's job, and asking for a key frame is the app's.</b> A delta frame that
/// arrives before the first key frame (or after the decoder lost its place) can't be drawn, so the view drops
/// it and raises <see cref="KeyFrameNeeded"/>; the app passes that on to the sender, whose
/// <see cref="VideoEncoder.RequestKeyFrame"/> answers it. The event is throttled to about once a second while
/// out of sync. Without anyone listening, the stream recovers at the sender's next scheduled key frame
/// (<see cref="VideoEncoder.KeyFrameInterval"/>).
/// </para>
/// <para>
/// Codec support: JPEG everywhere (iOS, Mac Catalyst, macOS, Android, Windows); H.264 on iOS, Mac Catalyst and
/// macOS (VideoToolbox) and on Android (MediaCodec, when the device has an AVC decoder). Windows reports
/// <c>CanDecode(VideoCodec.H264) == false</c>. Check <see cref="CanDecode"/> before agreeing on a codec.
/// </para>
/// </remarks>
public partial class RemoteVideoView : View
{
    readonly RemoteVideoGate gate;
    readonly object renderLock = new();
    volatile IRemoteVideoRenderer? renderer;

    /// <summary>Create the view.</summary>
    public RemoteVideoView() : this(new RemoteVideoGate()) { }

    internal RemoteVideoView(RemoteVideoGate gate) => this.gate = gate;

    /// <summary>
    /// How the upright image fills the view. Default <see cref="Aspect.AspectFit"/> (letterboxed, nothing
    /// cropped). <see cref="Aspect.AspectFill"/> covers the view and crops the overflow, which is what a
    /// full-screen call usually wants; <see cref="Aspect.Fill"/> stretches; <see cref="Aspect.Center"/> reads
    /// as AspectFit.
    /// </summary>
    public static readonly BindableProperty AspectProperty = BindableProperty.Create(
        nameof(Aspect), typeof(Aspect), typeof(RemoteVideoView), Aspect.AspectFit);

    /// <inheritdoc cref="AspectProperty"/>
    public Aspect Aspect
    {
        get => (Aspect)this.GetValue(AspectProperty);
        set => this.SetValue(AspectProperty, value);
    }

    /// <summary>
    /// Raised when the view needs a key frame before it can draw again: the first frames of a stream were deltas,
    /// a frame went missing and the decoder failed, or the codec changed. Ask the sender for one (its
    /// <see cref="VideoEncoder.RequestKeyFrame"/>). Raised on the thread that called <see cref="Render"/> (or the
    /// decoder's thread), not the UI thread, and at most about once a second while the view stays out of sync.
    /// </summary>
    public event EventHandler? KeyFrameNeeded;

    /// <summary>
    /// Draw a received frame. Safe to call from any thread, typically the transport's receive loop; the decode runs
    /// on the calling thread and the image is handed to the UI thread, newest first, so a slow UI thread never
    /// backs the stream up.
    /// </summary>
    /// <remarks>
    /// Frames must arrive in order. A delta frame before the first key frame is dropped (and
    /// <see cref="KeyFrameNeeded"/> raised). Frames that arrive while the view has no handler (not on screen yet)
    /// are dropped too, and the stream restarts at the next key frame once it does.
    /// </remarks>
    public void Render(EncodedVideoFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var request = false;
        lock (this.renderLock)
        {
            if (this.renderer is not { } target)
            {
                // nothing can decode yet; start from a key frame once something can
                this.gate.Reset();
                return;
            }

            var (render, ask) = this.gate.Accept(frame);
            request = ask;
            if (render)
                target.Render(frame);
        }

        if (request)
            this.RaiseKeyFrameNeeded();
    }

    /// <summary>
    /// Blank the view and forget the stream, e.g. when the call ends or the camera on the other side turns off.
    /// The next stream starts from its first key frame. Safe from any thread.
    /// </summary>
    public void Clear()
    {
        lock (this.renderLock)
        {
            this.gate.Reset();
            this.renderer?.Clear();
        }
    }

    /// <summary>Whether this platform can decode and draw <paramref name="codec"/>.</summary>
    public static bool CanDecode(VideoCodec codec) => PlatformCanDecode(codec);

    /// <summary>
    /// Attach (or, with <c>null</c>, detach) the platform renderer. Called by the handler. The stream restarts at
    /// the next key frame either way.
    /// </summary>
    internal void SetRenderer(IRemoteVideoRenderer? next)
    {
        lock (this.renderLock)
        {
            this.gate.Reset();
            this.renderer = next;
        }
    }

    /// <summary>The platform decoder failed on a frame the gate let through. Called from the decoding thread.</summary>
    internal void OnDecoderLostSync()
    {
        if (this.gate.LostSync())
            this.RaiseKeyFrameNeeded();
    }

    void RaiseKeyFrameNeeded()
    {
        try
        {
            this.KeyFrameNeeded?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            // a throwing handler must not stop the decoder that is reporting it
        }
    }

    private static partial bool PlatformCanDecode(VideoCodec codec);
}


/// <summary>
/// The platform half of a <see cref="RemoteVideoView"/>: decodes frames the view's key-frame gate let through
/// and puts them on screen. Calls are serialized by the view.
/// </summary>
interface IRemoteVideoRenderer : IDisposable
{
    /// <summary>Decode and show a frame. Reports a failure through the callback it was created with.</summary>
    void Render(EncodedVideoFrame frame);

    /// <summary>Blank the view and drop decoder state.</summary>
    void Clear();
}
