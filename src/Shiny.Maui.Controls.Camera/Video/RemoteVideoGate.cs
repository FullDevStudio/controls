namespace Shiny.Maui.Controls.Camera.Video;

/// <summary>
/// The receiving side's sync logic for <see cref="RemoteVideoView"/>: which frames reach the decoder, and when
/// to ask the sender for a key frame. Kept apart from every platform decoder so it can be tested on its own.
/// </summary>
/// <remarks>
/// <para>
/// An H.264 delta frame only means something on top of the frames before it. Fed to a decoder that has not
/// seen a key frame (the call just started, a frame was lost, the decoder was rebuilt), it decodes into
/// garbage or an error. So until a key frame arrives every delta is dropped, and the sender is asked for one.
/// JPEG frames always stand alone and always pass.
/// </para>
/// <para>
/// Requests are throttled to one per <c>requestInterval</c>: a 15 fps stream that has lost sync would
/// otherwise ask fifteen times a second while the first request is still on its way. A request that gets lost
/// is repeated once the interval has passed, and a key frame arriving clears the throttle so the next loss
/// asks straight away.
/// </para>
/// <para>
/// A change of codec counts as a loss of sync: the first frame of the new codec has to be a key frame.
/// </para>
/// </remarks>
sealed class RemoteVideoGate(TimeProvider time, TimeSpan requestInterval)
{
    readonly object sync = new();
    bool synced;
    VideoCodec? codec;
    long? lastRequest;

    /// <summary>A gate on the system clock that repeats a key-frame request at most once a second.</summary>
    public RemoteVideoGate() : this(TimeProvider.System, TimeSpan.FromSeconds(1)) { }

    /// <summary>Whether the gate has seen a key frame since it was last reset or lost sync.</summary>
    public bool IsSynced
    {
        get { lock (this.sync) return this.synced; }
    }

    /// <summary>
    /// Decide what to do with an incoming frame: hand it to the decoder (<c>Render</c>), and/or ask the
    /// sender for a key frame (<c>RequestKeyFrame</c>).
    /// </summary>
    public (bool Render, bool RequestKeyFrame) Accept(EncodedVideoFrame frame)
    {
        lock (this.sync)
        {
            if (this.codec != frame.Codec)
            {
                this.codec = frame.Codec;
                this.synced = false;
            }

            if (frame.IsKeyFrame || frame.Codec == VideoCodec.Jpeg)
            {
                this.synced = true;
                this.lastRequest = null;
                return (true, false);
            }

            if (this.synced)
                return (true, false);

            return (false, this.TryRequest());
        }
    }

    /// <summary>
    /// The decoder failed or was rebuilt: drop deltas until the next key frame. Returns whether to ask the sender
    /// for one now (subject to the same throttle).
    /// </summary>
    public bool LostSync()
    {
        lock (this.sync)
        {
            this.synced = false;
            return this.TryRequest();
        }
    }

    /// <summary>Start over, as if no frame had ever arrived (<see cref="RemoteVideoView.Clear"/>).</summary>
    public void Reset()
    {
        lock (this.sync)
        {
            this.synced = false;
            this.codec = null;
            this.lastRequest = null;
        }
    }

    bool TryRequest()
    {
        var now = time.GetTimestamp();
        if (this.lastRequest is { } last && time.GetElapsedTime(last, now) < requestInterval)
            return false;

        this.lastRequest = now;
        return true;
    }
}
