#if ANDROID
using Android.Views;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Platform;

namespace Shiny.Maui.Controls.Collections;

internal class MauiViewHolder : RecyclerView.ViewHolder
{
    readonly IMauiContext mauiContext;
    Microsoft.Maui.Controls.View? mauiView;
    Android.Views.View? platformView;

    public MauiViewHolder(IMauiContext mauiContext, ViewGroup parent)
        : base(new Android.Widget.FrameLayout(parent.Context!) { LayoutParameters = new ViewGroup.LayoutParams(
            ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent) })
    {
        this.mauiContext = mauiContext;
    }

    public Microsoft.Maui.Controls.View? MauiView => mauiView;

    /// <summary>The MAUI view's native view, inside <see cref="RecyclerView.ViewHolder.ItemView"/>.</summary>
    public Android.Views.View? PlatformChild => platformView;

    public void Bind(CollectionControlBase control, object item, int position)
    {
        if (mauiView is null)
        {
            mauiView = control.CreateItemView(item);
            platformView = mauiView.ToPlatform(mauiContext);
            var container = (Android.Widget.FrameLayout)ItemView;
            container.RemoveAllViews();
            container.AddView(platformView);
        }
        else
        {
            control.RecycleItemView(mauiView, item);
        }

        // Measure and layout the MAUI view
        var widthSpec = ItemView.LayoutParameters?.Width == ViewGroup.LayoutParams.MatchParent
            ? ((ItemView.Parent as Android.Views.View)?.Width ?? 0)
            : double.PositiveInfinity;

        if (widthSpec > 0)
        {
            var size = (mauiView as IView).Measure(
                widthSpec / mauiContext.Context!.Resources!.DisplayMetrics!.Density,
                double.PositiveInfinity);

            var density = mauiContext.Context!.Resources!.DisplayMetrics!.Density;
            var heightPx = (int)(size.Height * density);

            ItemView.LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, heightPx);
        }
    }

    public void BindCustomView(Microsoft.Maui.Controls.View view, IMauiContext context)
    {
        mauiView = view;
        platformView = mauiView.ToPlatform(context);
        var container = (Android.Widget.FrameLayout)ItemView;
        container.RemoveAllViews();
        container.AddView(platformView);

        var widthSpec = ItemView.LayoutParameters?.Width == ViewGroup.LayoutParams.MatchParent
            ? ((ItemView.Parent as Android.Views.View)?.Width ?? 0)
            : double.PositiveInfinity;

        if (widthSpec > 0)
        {
            var density = context.Context!.Resources!.DisplayMetrics!.Density;
            var size = (mauiView as IView).Measure(
                widthSpec / density,
                double.PositiveInfinity);
            var heightPx = (int)(size.Height * density);

            ItemView.LayoutParameters = new ViewGroup.LayoutParams(
                ViewGroup.LayoutParams.MatchParent, heightPx);
        }
    }

    public void Recycle()
    {
        if (mauiView is not null)
            mauiView.BindingContext = null;
    }
}
#endif
