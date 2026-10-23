using Microsoft.Maui.Handlers;
using UIKit;

namespace Shiny.Maui.Controls.Camera;

/// <summary>
/// The UIKit view behind a <see cref="RemoteVideoView"/> (iOS and Mac Catalyst): a black, clipping view whose
/// layer hosts the frame layer and re-fits it on every layout pass.
/// </summary>
public sealed class RemoteVideoPlatformView : UIView
{
    internal RemoteVideoPlatformView()
    {
        this.BackgroundColor = UIColor.Black;
        this.ClipsToBounds = true; // AspectFill crops here
        this.Content = new RemoteVideoLayer(this.Layer, yAxisUp: false);
    }

    internal RemoteVideoLayer Content { get; }

    public override void LayoutSubviews()
    {
        base.LayoutSubviews();
        this.Content.Layout();
    }
}


public partial class RemoteVideoViewHandler : ViewHandler<RemoteVideoView, RemoteVideoPlatformView>
{
    AppleRemoteVideoRenderer? renderer;

    protected override RemoteVideoPlatformView CreatePlatformView() => new();

    protected override void ConnectHandler(RemoteVideoPlatformView platformView)
    {
        base.ConnectHandler(platformView);
        this.renderer = new AppleRemoteVideoRenderer(platformView.Content, () => this.MaybeVirtualView?.OnDecoderLostSync());
        this.VirtualView.SetRenderer(this.renderer);
    }

    protected override void DisconnectHandler(RemoteVideoPlatformView platformView)
    {
        this.MaybeVirtualView?.SetRenderer(null);
        this.renderer?.Dispose();
        this.renderer = null;
        platformView.Content.Clear();
        base.DisconnectHandler(platformView);
    }

    static partial void MapAspect(RemoteVideoViewHandler handler, RemoteVideoView view)
    {
        handler.PlatformView.Content.Aspect = view.Aspect;
        handler.PlatformView.Content.Layout();
    }
}
