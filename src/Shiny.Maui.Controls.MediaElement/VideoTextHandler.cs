#if ANDROID || IOS || MACCATALYST || MACOS
using Microsoft.Maui.Handlers;

namespace Shiny.Maui.Controls.Media;

/// <summary>Hosts the masked native video view of a <see cref="VideoText"/>.</summary>
partial class VideoTextHandler
{
    internal VideoTextSurface? MaybeVirtualView => ((IElementHandler)this).VirtualView as VideoTextSurface;

    public static IPropertyMapper<VideoTextSurface, VideoTextHandler> Mapper =
        new PropertyMapper<VideoTextSurface, VideoTextHandler>(ViewHandler.ViewMapper)
        {
            [nameof(VideoTextSurface.Text)] = MapText,
            [nameof(VideoTextSurface.VideoSize)] = MapVideoSize
        };

    public VideoTextHandler() : base(Mapper, new CommandMapper<VideoTextSurface, VideoTextHandler>(ViewHandler.ViewCommandMapper))
    {
    }

    protected override void ConnectHandler(VideoTextPlatformView platformView)
    {
        base.ConnectHandler(platformView);
        this.VirtualView.AttachOutput(platformView);
    }

    protected override void DisconnectHandler(VideoTextPlatformView platformView)
    {
        this.MaybeVirtualView?.DetachOutput();
        base.DisconnectHandler(platformView);
    }

    static void MapVideoSize(VideoTextHandler handler, VideoTextSurface view)
        => handler.PlatformView.SetVideoSize(view.VideoSize.Width, view.VideoSize.Height);
}
#endif
