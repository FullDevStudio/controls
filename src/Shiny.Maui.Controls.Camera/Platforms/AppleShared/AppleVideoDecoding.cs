using CoreAnimation;
using CoreFoundation;
using CoreGraphics;
using CoreImage;
using CoreMedia;
using CoreVideo;
using Foundation;
using ImageIO;
using Shiny.Maui.Controls.Camera.Video;
using VideoToolbox;

namespace Shiny.Maui.Controls.Camera;

public partial class RemoteVideoView
{
    private static partial bool PlatformCanDecode(VideoCodec codec) => codec is VideoCodec.Jpeg or VideoCodec.H264;
}


/// <summary>
/// Turns received frames into <see cref="CGImage"/>s: JPEG through ImageIO, H.264 through a
/// <see cref="VTDecompressionSession"/>. Shared by the UIKit and AppKit views, which only differ in how they
/// put the image on screen.
/// </summary>
/// <remarks>
/// <para>
/// One decoder for both codecs, rather than an <c>AVSampleBufferDisplayLayer</c> for H.264 and an image for
/// JPEG, so both land in the same layer with the same rotation, mirroring and aspect, and so a decode failure
/// is reported as a status code the view can act on (by asking the sender for a key frame) rather than as a
/// layer that quietly goes black.
/// </para>
/// <para>
/// The SPS and PPS come in-band with every key frame. A key frame whose parameter sets differ from the last
/// ones (the sender changed size or profile) rebuilds the format description and the session.
/// </para>
/// </remarks>
sealed class AppleVideoDecoder(Action<CGImage, EncodedVideoFrame> onImage, Action onLostSync) : IDisposable
{
    VTDecompressionSession? session;
    CMVideoFormatDescription? format;
    byte[]? sps;
    byte[]? pps;
    EncodedVideoFrame? decoding;

    public void Decode(EncodedVideoFrame frame)
    {
        switch (frame.Codec)
        {
            case VideoCodec.Jpeg:
                this.DecodeJpeg(frame);
                break;

            case VideoCodec.H264:
                this.DecodeH264(frame);
                break;
        }
    }

    void DecodeJpeg(EncodedVideoFrame frame)
    {
        using var data = NSData.FromArray(frame.Data.ToArray());
        using var source = CGImageSource.FromData(data);

        // decoded here, on the caller's thread, rather than lazily on the main thread the first time the
        // layer draws it
        var image = source?.CreateImage(0, new CGImageOptions { ShouldCacheImmediately = true });
        if (image is null)
            return; // a corrupt JPEG costs one frame; the next one stands alone

        onImage(image, frame);
    }

    void DecodeH264(EncodedVideoFrame frame)
    {
        var data = frame.Data.Span;
        if (frame.IsKeyFrame)
        {
            var (nextSps, nextPps) = H264AnnexB.GetParameterSets(data);
            if (nextSps is not null && nextPps is not null && (!Same(nextSps, this.sps) || !Same(nextPps, this.pps)))
                this.Rebuild(nextSps, nextPps);
        }

        if (this.session is null || this.format is null)
        {
            onLostSync();
            return;
        }

        var payload = H264AnnexB.ToLengthPrefixed(data);
        if (payload.Length == 0)
            return;

        // A block that owns its memory, with the frame copied in. FromMemoryBlock(byte[]) leaves the block
        // unallocated, and VideoToolbox then drops every frame with kCMBlockBufferUnallocatedBlockErr (-12707).
        using var block = CMBlockBuffer.FromMemoryBlock(
            IntPtr.Zero,
            (nuint)payload.Length,
            null,
            0,
            (nuint)payload.Length,
            CMBlockBufferFlags.AssureMemoryNow,
            out var blockError
        );
        if (block is null || blockError != CMBlockBufferError.None || block.ReplaceDataBytes(payload, 0) != CMBlockBufferError.None)
        {
            onLostSync();
            return;
        }

        var timing = new CMSampleTimingInfo
        {
            PresentationTimeStamp = new CMTime(frame.Timestamp.Ticks / 10, 1_000_000),
            Duration = CMTime.Invalid,
            DecodeTimeStamp = CMTime.Invalid
        };

        using var sample = CMSampleBuffer.CreateReady(block, this.format, 1, [timing], [(nuint)payload.Length], out var sampleError);
        if (sample is null || sampleError != CMSampleBufferError.None)
        {
            onLostSync();
            return;
        }

        // synchronous: the output callback runs before DecodeFrame returns, so the frame it belongs to is
        // simply the one being decoded
        this.decoding = frame;
        var status = this.session.DecodeFrame(sample, VTDecodeFrameFlags.None, IntPtr.Zero, out _);
        this.decoding = null;

        if (status != VTStatus.Ok)
        {
            // an invalid session (the app went to the background and the hardware decoder was reclaimed) is
            // rebuilt from the next key frame's parameter sets
            this.Teardown();
            onLostSync();
        }
    }

    void Rebuild(byte[] nextSps, byte[] nextPps)
    {
        this.Teardown();

        var description = CMVideoFormatDescription.FromH264ParameterSets([nextSps, nextPps], 4, out var error);
        if (description is null || error != CMFormatDescriptionError.None)
            return;

        var created = VTDecompressionSession.Create(this.OnDecoded, description, null, null);
        if (created is null)
        {
            description.Dispose();
            return;
        }

        this.format = description;
        this.session = created;
        this.sps = nextSps;
        this.pps = nextPps;
    }

    void OnDecoded(IntPtr refcon, VTStatus status, VTDecodeInfoFlags flags, CVImageBuffer? buffer, CMTime pts, CMTime duration)
    {
        try
        {
            if ((flags & VTDecodeInfoFlags.FrameDropped) != 0)
                return;

            if (status != VTStatus.Ok || buffer is not CVPixelBuffer pixels || this.decoding is not { } frame)
            {
                onLostSync();
                return;
            }

            using var image = new CIImage(pixels);
            var cg = AppleVideoContext.Shared.CreateCGImage(image, image.Extent);
            if (cg is not null)
                onImage(cg, frame);
        }
        catch (Exception)
        {
            // a native callback: nothing may unwind into VideoToolbox
        }
    }

    static bool Same(byte[] a, byte[]? b) => b is not null && a.AsSpan().SequenceEqual(b);

    void Teardown()
    {
        this.session?.Dispose();
        this.session = null;
        this.format?.Dispose();
        this.format = null;
        this.sps = null;
        this.pps = null;
    }

    public void Dispose() => this.Teardown();
}


/// <summary>
/// The <see cref="IRemoteVideoRenderer"/> for every Apple platform: decodes on the caller's thread and hands the
/// newest image to the main thread, latest-wins.
/// </summary>
/// <remarks>
/// Like the camera's filtered preview, it keeps exactly one pending image and one queued main-thread update: a
/// burst of frames replaces the pending image rather than queueing behind it, so a busy main thread shows the
/// newest frame it can instead of falling further and further behind.
/// </remarks>
sealed class AppleRemoteVideoRenderer : IRemoteVideoRenderer
{
    readonly object gate = new();
    readonly AppleVideoDecoder decoder;
    readonly RemoteVideoLayer layer;
    // a class, not a tuple: Interlocked.Exchange only swaps references, and throws NotSupportedException for a struct
    PendingImage? pending;
    int updateQueued;
    bool disposed;

    public AppleRemoteVideoRenderer(RemoteVideoLayer layer, Action onLostSync)
    {
        this.layer = layer;
        this.decoder = new AppleVideoDecoder(this.Publish, onLostSync);
    }

    public void Render(EncodedVideoFrame frame)
    {
        lock (this.gate)
        {
            if (!this.disposed)
                this.decoder.Decode(frame);
        }
    }

    public void Clear()
    {
        lock (this.gate)
            this.decoder.Dispose();

        Interlocked.Exchange(ref this.pending, null)?.Image.Dispose();
        DispatchQueue.MainQueue.DispatchAsync(this.layer.Clear);
    }

    void Publish(CGImage image, EncodedVideoFrame frame)
    {
        var previous = Interlocked.Exchange(ref this.pending, new PendingImage(image, frame));
        previous?.Image.Dispose(); // never reached the layer, so still ours

        if (Interlocked.Exchange(ref this.updateQueued, 1) == 1)
            return;

        DispatchQueue.MainQueue.DispatchAsync(() =>
        {
            Interlocked.Exchange(ref this.updateQueued, 0);
            if (Interlocked.Exchange(ref this.pending, null) is { } next)
                this.layer.Show(next.Image, next.Frame);
        });
    }

    public void Dispose()
    {
        lock (this.gate)
        {
            this.disposed = true;
            this.decoder.Dispose();
        }
        Interlocked.Exchange(ref this.pending, null)?.Image.Dispose();
    }


    sealed record PendingImage(CGImage Image, EncodedVideoFrame Frame);
}


/// <summary>
/// The layer a received frame is drawn into, and the geometry that puts it upright: sized to the encoded
/// image, centred in its host, then rotated, mirrored and scaled by one affine transform. Main thread only.
/// </summary>
sealed class RemoteVideoLayer(CALayer host, bool yAxisUp)
{
    readonly CALayer content = new() { ContentsGravity = CALayer.GravityResize };
    EncodedVideoFrame? shown;
    CGImage? image;

    /// <summary>How the image fills the host.</summary>
    public Aspect Aspect { get; set; } = Aspect.AspectFit;

    public void Show(CGImage next, EncodedVideoFrame frame)
    {
        if (!ReferenceEquals(this.content.SuperLayer, host))
            host.AddSublayer(this.content);

        this.content.Contents = next;
        this.image?.Dispose(); // the layer holds its own reference now
        this.image = next;
        this.shown = frame;
        this.Layout();
    }

    public void Clear()
    {
        this.content.Contents = null;
        this.image?.Dispose();
        this.image = null;
        this.shown = null;
    }

    /// <summary>Re-fit the image to the host's current bounds (after a layout pass or an aspect change).</summary>
    public void Layout()
    {
        if (this.shown is not { } frame)
            return;

        var bounds = host.Bounds;
        var (sx, sy, rotation) = VideoScaling.Layout(
            frame.Width, frame.Height, frame.Rotation, bounds.Width, bounds.Height, this.Aspect);

        // UIKit's y axis points down, so a positive angle turns clockwise; a layer-backed NSView's points up,
        // where the same angle turns the other way
        var radians = rotation * Math.PI / 180d * (yAxisUp ? -1 : 1);
        var transform = CGAffineTransform.Multiply(
            CGAffineTransform.MakeRotation((nfloat)radians),
            CGAffineTransform.MakeScale((nfloat)(frame.IsMirrored ? -sx : sx), (nfloat)sy));

        CATransaction.Begin();
        CATransaction.DisableActions = true; // no implicit animation into each new frame's geometry
        this.content.AffineTransform = CGAffineTransform.MakeIdentity();
        this.content.Bounds = new CGRect(0, 0, frame.Width, frame.Height);
        this.content.Position = new CGPoint(bounds.GetMidX(), bounds.GetMidY());
        this.content.AffineTransform = transform;
        CATransaction.Commit();
    }
}
