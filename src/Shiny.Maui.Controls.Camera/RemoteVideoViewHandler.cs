#if ANDROID || IOS || MACCATALYST || WINDOWS || MACOS
using Microsoft.Maui.Handlers;

namespace Shiny.Maui.Controls.Camera;

/// <summary>
/// The platform handler for <see cref="RemoteVideoView"/>. Registered by <c>UseShinyCamera()</c>.
/// </summary>
/// <remarks>
/// Each platform builds a host view and an <see cref="IRemoteVideoRenderer"/> over it on connect, attaches the
/// renderer to the virtual view (which feeds it through its key-frame gate), and detaches and disposes it on
/// disconnect.
/// </remarks>
public partial class RemoteVideoViewHandler
{
    public static IPropertyMapper<RemoteVideoView, RemoteVideoViewHandler> Mapper =
        new PropertyMapper<RemoteVideoView, RemoteVideoViewHandler>(ViewHandler.ViewMapper)
        {
            [nameof(RemoteVideoView.Aspect)] = MapAspect,
        };

    public static CommandMapper<RemoteVideoView, RemoteVideoViewHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper);

    public RemoteVideoViewHandler() : base(Mapper, CommandMapper)
    {
    }

    /// <summary>
    /// The bound view, or <c>null</c> once disconnected. <c>VirtualView</c> throws when disconnected, and the
    /// renderer's callbacks can outlive the handler.
    /// </summary>
    internal RemoteVideoView? MaybeVirtualView => ((IElementHandler)this).VirtualView as RemoteVideoView;

    static partial void MapAspect(RemoteVideoViewHandler handler, RemoteVideoView view);
}
#endif
