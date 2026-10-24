using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Views;
using Microsoft.Maui.Handlers;
using Shiny.Maui.Controls.Camera.Video;
using AView = Android.Views.View;
using AWidget = Android.Widget;

namespace Shiny.Maui.Controls.Camera;

public partial class RemoteVideoView
{
    private static partial bool PlatformCanDecode(VideoCodec codec) => codec switch
    {
        VideoCodec.Jpeg => true,
        VideoCodec.H264 => AndroidVideoCodecs.HasAvcDecoder,
        _ => false
    };
}


/// <summary>
/// The Android view behind a <see cref="RemoteVideoView"/>: a black frame holding a <see cref="TextureView"/>
/// that MediaCodec renders H.264 into, and an <see cref="AWidget.ImageView"/> above it for JPEG frames. Both
/// are rotated, mirrored and fitted by a matrix built from <see cref="VideoScaling.Layout"/>.
/// </summary>
/// <remarks>
/// A TextureView rather than a SurfaceView because it takes a transform: a SurfaceView can't be rotated or
/// mirrored, and the receiver is the side that turns frames upright.
/// </remarks>
public sealed class RemoteVideoPlatformView : AWidget.FrameLayout
{
    internal RemoteVideoPlatformView(Context context) : base(context)
    {
        this.SetBackgroundColor(Android.Graphics.Color.Black);
        this.SetClipChildren(true);

        this.Texture = new TextureView(context) { Alpha = 0f };
        this.Image = new AWidget.ImageView(context);
        this.Image.SetScaleType(AWidget.ImageView.ScaleType.Matrix);

        var fill = new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
        this.AddView(this.Texture, fill);
        this.AddView(this.Image, new LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent));
    }

    internal TextureView Texture { get; }
    internal AWidget.ImageView Image { get; }

    internal Aspect Aspect { get; set; } = Aspect.AspectFit;

    // what each surface is currently showing, so a layout pass or an aspect change can re-fit it
    (int Width, int Height, int Rotation, bool Mirrored)? textureFrame;
    (int Width, int Height, int Rotation, bool Mirrored)? imageFrame;
    Bitmap? bitmap;

    internal void ShowBitmap(Bitmap next, EncodedVideoFrame frame)
    {
        var previous = this.bitmap;
        this.bitmap = next;
        this.Image.SetImageBitmap(next);
        this.imageFrame = (next.Width, next.Height, frame.Rotation, frame.IsMirrored);
        this.Texture.Alpha = 0f;
        this.Fit();

        // no longer referenced by the ImageView, so its pixels can go now rather than at the next GC
        previous?.Recycle();
        previous?.Dispose();
    }

    internal void ShowTexture(EncodedVideoFrame frame)
    {
        var next = (frame.Width, frame.Height, frame.Rotation, frame.IsMirrored);
        if (this.Texture.Alpha < 1f || this.textureFrame != next || this.bitmap is not null)
        {
            this.textureFrame = next;
            this.ClearBitmap();
            this.Texture.Alpha = 1f;
            this.Fit();
        }
    }

    internal void ClearAll()
    {
        this.ClearBitmap();
        this.Texture.Alpha = 0f;
        this.textureFrame = null;
    }

    void ClearBitmap()
    {
        this.Image.SetImageBitmap(null);
        this.bitmap?.Recycle();
        this.bitmap?.Dispose();
        this.bitmap = null;
        this.imageFrame = null;
    }

    protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
    {
        base.OnLayout(changed, left, top, right, bottom);
        if (changed)
            this.Fit();
    }

    internal void Fit()
    {
        float vw = this.Width, vh = this.Height;
        if (vw <= 0 || vh <= 0)
            return;

        if (this.imageFrame is { } image)
        {
            using var matrix = Build(image, vw, vh, this.Aspect, stretchedToView: false);
            this.Image.ImageMatrix = matrix;
        }

        if (this.textureFrame is { } texture)
        {
            using var matrix = Build(texture, vw, vh, this.Aspect, stretchedToView: true);
            this.Texture.SetTransform(matrix);
        }
    }

    // Image space -> centred -> rotated clockwise -> mirrored and scaled in upright space -> view centre. A
    // TextureView's content starts out stretched to the view, so it is first taken back to image pixels.
    static Matrix Build((int Width, int Height, int Rotation, bool Mirrored) f, float vw, float vh, Aspect aspect, bool stretchedToView)
    {
        var (sx, sy, rotation) = VideoScaling.Layout(f.Width, f.Height, f.Rotation, vw, vh, aspect);
        var m = new Matrix();
        if (stretchedToView)
            m.SetScale(f.Width / vw, f.Height / vh);

        m.PostTranslate(-f.Width / 2f, -f.Height / 2f);
        m.PostRotate(rotation);
        m.PostScale((float)(f.Mirrored ? -sx : sx), (float)sy);
        m.PostTranslate(vw / 2f, vh / 2f);
        return m;
    }
}


/// <summary>
/// The Android <see cref="IRemoteVideoRenderer"/>: JPEG through <see cref="BitmapFactory"/>, H.264 through a
/// MediaCodec decoder that renders straight into the view's <see cref="TextureView"/> surface.
/// </summary>
/// <remarks>
/// <para>
/// The decoder is built from the first key frame once the surface exists, with that frame's SPS and PPS as its
/// codec-specific data, and rebuilt when a key frame brings different ones (the sender changed size or
/// profile). A thread drains its output and renders each decoded frame the moment it is ready; feeding and
/// draining from one thread would hold every frame back until the next one arrived.
/// </para>
/// <para>
/// Anything that leaves the decoder short of a frame (no free input buffer, a codec error) is a loss of sync,
/// reported to the view so it can ask for a key frame.
/// </para>
/// </remarks>
sealed class AndroidRemoteVideoRenderer : Java.Lang.Object, IRemoteVideoRenderer, TextureView.ISurfaceTextureListener
{
    readonly object gate = new();
    readonly RemoteVideoPlatformView view;
    readonly Action onLostSync;
    Surface? surface;
    MediaCodec? decoder;
    Thread? drain;
    volatile bool draining;
    byte[]? sps;
    byte[]? pps;
    int decoderWidth;
    int decoderHeight;
    bool disposed;
    // a class, not a tuple: Interlocked.Exchange only swaps references, and throws NotSupportedException for a struct
    PendingBitmap? pendingBitmap;
    EncodedVideoFrame? pendingTexture;
    int updateQueued;

    public AndroidRemoteVideoRenderer(RemoteVideoPlatformView view, Action onLostSync)
    {
        this.view = view;
        this.onLostSync = onLostSync;
        view.Texture.SurfaceTextureListener = this;
        if (view.Texture.IsAvailable && view.Texture.SurfaceTexture is { } existing)
            this.surface = new Surface(existing);
    }

    public void Render(EncodedVideoFrame frame)
    {
        lock (this.gate)
        {
            if (this.disposed)
                return;

            if (frame.Codec == VideoCodec.Jpeg)
                this.RenderJpeg(frame);
            else if (frame.Codec == VideoCodec.H264)
                this.RenderH264(frame);
        }
    }

    void RenderJpeg(EncodedVideoFrame frame)
    {
        var data = frame.Data.ToArray();
        var bitmap = BitmapFactory.DecodeByteArray(data, 0, data.Length);
        if (bitmap is null)
            return; // one bad frame; the next stands alone

        if (Interlocked.Exchange(ref this.pendingBitmap, new PendingBitmap(bitmap, frame)) is { } superseded)
        {
            superseded.Bitmap.Recycle();
            superseded.Bitmap.Dispose();
        }
        this.QueueUpdate();
    }

    void RenderH264(EncodedVideoFrame frame)
    {
        var data = frame.Data.ToArray();
        if (frame.IsKeyFrame)
        {
            var (nextSps, nextPps) = H264AnnexB.GetParameterSets(data);
            if (nextSps is not null && nextPps is not null
                && (this.decoder is null || frame.Width != this.decoderWidth || frame.Height != this.decoderHeight
                    || !Same(nextSps, this.sps) || !Same(nextPps, this.pps)))
            {
                this.Rebuild(nextSps, nextPps, frame.Width, frame.Height);
            }
        }

        if (this.decoder is not { } codec)
        {
            this.onLostSync();
            return;
        }

        try
        {
            var index = codec.DequeueInputBuffer(20_000);
            if (index < 0)
            {
                // the frame can't go in, so every delta after it would decode against a hole
                this.onLostSync();
                return;
            }

            using (var input = codec.GetInputBuffer(index))
            {
                input!.Clear();
                input.Put(data);
            }
            codec.QueueInputBuffer(index, 0, data.Length, frame.Timestamp.Ticks / 10, MediaCodecBufferFlags.None);
        }
        catch (Exception)
        {
            this.Teardown();
            this.onLostSync();
            return;
        }

        Interlocked.Exchange(ref this.pendingTexture, frame);
        this.QueueUpdate();
    }

    void Rebuild(byte[] nextSps, byte[] nextPps, int width, int height)
    {
        this.Teardown();
        if (this.surface is not { } target)
            return; // not on screen yet; the next key frame after the surface arrives starts the decoder

        MediaCodec? codec = null;
        try
        {
            codec = MediaCodec.CreateDecoderByType(AndroidVideoCodecs.Avc);
            using var format = MediaFormat.CreateVideoFormat(AndroidVideoCodecs.Avc, width, height);
            format.SetByteBuffer("csd-0", Java.Nio.ByteBuffer.Wrap(WithStartCode(nextSps)));
            format.SetByteBuffer("csd-1", Java.Nio.ByteBuffer.Wrap(WithStartCode(nextPps)));
            if (OperatingSystem.IsAndroidVersionAtLeast(30))
                format.SetInteger(MediaFormat.KeyLowLatency, 1);

            codec.Configure(format, target, null, MediaCodecConfigFlags.None);
            codec.Start();
        }
        catch (Exception)
        {
            codec?.Release();
            return;
        }

        this.decoder = codec;
        this.sps = nextSps;
        this.pps = nextPps;
        this.decoderWidth = width;
        this.decoderHeight = height;
        this.draining = true;
        this.drain = new Thread(() => this.Drain(codec)) { IsBackground = true, Name = "shiny.camera.video.decoder" };
        this.drain.Start();
    }

    void Drain(MediaCodec codec)
    {
        using var info = new MediaCodec.BufferInfo();
        while (this.draining)
        {
            try
            {
                var index = codec.DequeueOutputBuffer(info, 50_000);
                if (index >= 0)
                    codec.ReleaseOutputBuffer(index, true); // render it to the TextureView
            }
            catch (Exception)
            {
                if (!this.draining)
                    return;

                // a decoder error surfaces here as often as on the input side
                this.onLostSync();
                return;
            }
        }
    }

    void QueueUpdate()
    {
        if (Interlocked.Exchange(ref this.updateQueued, 1) == 1)
            return;

        this.view.Post(() =>
        {
            Interlocked.Exchange(ref this.updateQueued, 0);
            if (Interlocked.Exchange(ref this.pendingBitmap, null) is { } shown)
                this.view.ShowBitmap(shown.Bitmap, shown.Frame);

            if (Interlocked.Exchange(ref this.pendingTexture, null) is { } frame)
                this.view.ShowTexture(frame);
        });
    }

    public void Clear()
    {
        lock (this.gate)
            this.Teardown();

        if (Interlocked.Exchange(ref this.pendingBitmap, null) is { } pending)
        {
            pending.Bitmap.Recycle();
            pending.Bitmap.Dispose();
        }
        Interlocked.Exchange(ref this.pendingTexture, null);
        this.view.Post(this.view.ClearAll);
    }

    void Teardown()
    {
        var codec = this.decoder;
        this.decoder = null;
        this.sps = null;
        this.pps = null;
        if (codec is null)
            return;

        this.draining = false;
        this.drain?.Join(TimeSpan.FromMilliseconds(500));
        this.drain = null;
        try
        {
            codec.Stop();
        }
        catch (Exception)
        {
            // already stopped or failed
        }
        codec.Release();
        codec.Dispose();
    }

    static bool Same(byte[] a, byte[]? b) => b is not null && a.AsSpan().SequenceEqual(b);

    static byte[] WithStartCode(byte[] nal)
    {
        var result = new byte[nal.Length + 4];
        H264AnnexB.StartCode.CopyTo(result);
        nal.CopyTo(result, 4);
        return result;
    }

    // TextureView.ISurfaceTextureListener - main thread

    public void OnSurfaceTextureAvailable(SurfaceTexture surfaceTexture, int width, int height)
    {
        lock (this.gate)
        {
            this.surface = new Surface(surfaceTexture);
            this.sps = null; // force the next key frame to build a decoder onto the new surface
        }
        this.onLostSync();
    }

    public bool OnSurfaceTextureDestroyed(SurfaceTexture surfaceTexture)
    {
        lock (this.gate)
        {
            this.Teardown();
            this.surface?.Release();
            this.surface = null;
        }
        return true;
    }

    public void OnSurfaceTextureSizeChanged(SurfaceTexture surfaceTexture, int width, int height) => this.view.Fit();

    public void OnSurfaceTextureUpdated(SurfaceTexture surfaceTexture) { }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (this.gate)
            {
                this.disposed = true;
                this.Teardown();
            }
            if (Interlocked.Exchange(ref this.pendingBitmap, null) is { } pending)
            {
                pending.Bitmap.Recycle();
                pending.Bitmap.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}


public partial class RemoteVideoViewHandler : ViewHandler<RemoteVideoView, RemoteVideoPlatformView>
{
    AndroidRemoteVideoRenderer? renderer;

    protected override RemoteVideoPlatformView CreatePlatformView() => new(this.Context);

    protected override void ConnectHandler(RemoteVideoPlatformView platformView)
    {
        base.ConnectHandler(platformView);
        this.renderer = new AndroidRemoteVideoRenderer(platformView, () => this.MaybeVirtualView?.OnDecoderLostSync());
        this.VirtualView.SetRenderer(this.renderer);
    }

    protected override void DisconnectHandler(RemoteVideoPlatformView platformView)
    {
        this.MaybeVirtualView?.SetRenderer(null);
        platformView.Texture.SurfaceTextureListener = null;
        this.renderer?.Dispose();
        this.renderer = null;
        platformView.ClearAll();
        base.DisconnectHandler(platformView);
    }

    static partial void MapAspect(RemoteVideoViewHandler handler, RemoteVideoView view)
    {
        handler.PlatformView.Aspect = view.Aspect;
        handler.PlatformView.Fit();
    }
}


/// <summary>
/// A decoded JPEG waiting for the next layout pass
/// </summary>
sealed record PendingBitmap(Bitmap Bitmap, EncodedVideoFrame Frame);
