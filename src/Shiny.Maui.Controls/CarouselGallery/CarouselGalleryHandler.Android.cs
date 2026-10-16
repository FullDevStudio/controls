#if ANDROID
using Android.Views;
using AndroidX.RecyclerView.Widget;
using Microsoft.Maui.Handlers;
using Shiny.Maui.Controls.Collections;
using AView = Android.Views.View;

namespace Shiny.Maui.Controls.CarouselGallery;

public partial class CarouselGalleryHandler : ViewHandler<CarouselGallery, RecyclerView>
{
    CarouselAdapter? adapter;
    LinearLayoutManager? layoutManager;
    LinearSnapHelper? snapHelper;
    bool isUpdatingPosition;

    protected override RecyclerView CreatePlatformView()
    {
        var recyclerView = new RecyclerView(Context);
        recyclerView.SetClipToPadding(false);

        layoutManager = new LinearLayoutManager(Context, LinearLayoutManager.Horizontal, false);
        recyclerView.SetLayoutManager(layoutManager);
        recyclerView.SetItemAnimator(new DefaultItemAnimator());

        // The scale effect ran only from the scroll listener, so until the first scroll every item sat
        // at full size. A pass after each layout applies it to whatever is on screen.
        recyclerView.LayoutChange += (_, _) => ApplyScaleTransforms();

        return recyclerView;
    }

    protected override void ConnectHandler(RecyclerView platformView)
    {
        base.ConnectHandler(platformView);
        adapter = new CarouselAdapter(VirtualView, MauiContext!);
        platformView.SetAdapter(adapter);
        platformView.AddItemDecoration(new CarouselItemDecoration(VirtualView));
        platformView.AddOnScrollListener(new CarouselScrollListener(this));
        UpdatePeekInsets();
        UpdateSnapMode();
        UpdateItemsSource();
    }

    protected override void DisconnectHandler(RecyclerView platformView)
    {
        platformView.SetAdapter(null);
        adapter?.Dispose();
        adapter = null;
        base.DisconnectHandler(platformView);
    }

    void UpdateItemsSource()
    {
        adapter?.UpdateItems();
    }

    void UpdatePeekInsets()
    {
        if (PlatformView is null)
            return;

        // Only the horizontal insets: the peek is beside the item. A uniform PeekAreaInsets="40" also
        // padded top and bottom, which left a 160-high card 100 of a 180-high carousel and pushed it out
        // the bottom; iOS centres the item vertically in the full height, and so does the decoration.
        var density = Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        var insets = VirtualView.PeekAreaInsets;
        PlatformView.SetPadding((int)(insets.Left * density), 0, (int)(insets.Right * density), 0);
    }

    void UpdateSnapMode()
    {
        if (PlatformView is null)
            return;

        // Detach existing snap helper
        snapHelper?.AttachToRecyclerView(null);
        snapHelper = null;

        if (VirtualView.SnapCount >= 1)
        {
            snapHelper = new LinearSnapHelper();
            snapHelper.AttachToRecyclerView(PlatformView);
        }
    }

    void ApplyScaleTransforms()
    {
        if (layoutManager is null || PlatformView is null)
            return;

        var midX = PlatformView.Width / 2f;

        for (var i = 0; i < PlatformView.ChildCount; i++)
        {
            var child = PlatformView.GetChildAt(i);
            if (child is null)
                continue;

            var childMidX = (child.Left + child.Right) / 2f;
            var distFromCenter = Math.Abs(midX - childMidX) / (float)midX;
            distFromCenter = Math.Min(1f, distFromCenter);

            var scale = (float)(VirtualView.FocusedItemScale +
                (VirtualView.UnfocusedItemScale - VirtualView.FocusedItemScale) * distFromCenter);

            child.ScaleX = scale;
            child.ScaleY = scale;
        }
    }

    /// <summary>
    /// Brings <paramref name="position"/> to the middle, as iOS does (CenteredHorizontally). Start-aligning
    /// it left it off centre, and the snap helper - which snaps to the centre - then moved the carousel
    /// to a different item on the next scroll.
    /// </summary>
    void ScrollToCentre(int position)
    {
        if (layoutManager is null || PlatformView is null)
            return;

        if (PlatformView.Width == 0)
        {
            PlatformView.Post(() => ScrollToCentre(position));
            return;
        }

        var density = Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        var itemWidth = (int)(VirtualView.ItemWidth * density);
        var offset = (PlatformView.Width - itemWidth) / 2 - PlatformView.PaddingLeft;
        layoutManager.ScrollToPositionWithOffset(position, offset);
    }

    void OnScrollSettled()
    {
        if (isUpdatingPosition || layoutManager is null || PlatformView is null)
            return;

        int position;
        if (snapHelper is not null)
        {
            var snapView = snapHelper.FindSnapView(layoutManager);
            if (snapView is null)
                return;
            position = layoutManager.GetPosition(snapView);
        }
        else
        {
            // Free scroll: find closest item to center
            var midX = PlatformView.Width / 2f;
            var closestPosition = RecyclerView.NoPosition;
            var minDistance = float.MaxValue;

            for (var i = 0; i < PlatformView.ChildCount; i++)
            {
                var child = PlatformView.GetChildAt(i);
                if (child is null) continue;
                var childMidX = (child.Left + child.Right) / 2f;
                var distance = Math.Abs(midX - childMidX);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPosition = layoutManager.GetPosition(child);
                }
            }
            position = closestPosition;
        }

        if (position != RecyclerView.NoPosition && position != VirtualView.CurrentPosition)
        {
            isUpdatingPosition = true;
            VirtualView.RaisePositionChanged(position);
            isUpdatingPosition = false;
        }
    }

    static partial void MapItemsSource(CarouselGalleryHandler handler, CarouselGallery view)
    {
        handler.UpdateItemsSource();
    }

    static partial void MapCurrentPosition(CarouselGalleryHandler handler, CarouselGallery view)
    {
        if (handler.isUpdatingPosition)
            return;

        handler.ScrollToCentre(view.CurrentPosition);
    }

    static partial void MapItemSize(CarouselGalleryHandler handler, CarouselGallery view)
    {
        handler.adapter?.NotifyDataSetChanged();
        handler.PlatformView?.InvalidateItemDecorations();
    }

    static partial void MapScaling(CarouselGalleryHandler handler, CarouselGallery view)
    {
        handler.ApplyScaleTransforms();
    }

    static partial void MapPeekInsets(CarouselGalleryHandler handler, CarouselGallery view)
    {
        handler.UpdatePeekInsets();
    }

    static partial void MapItemTemplate(CarouselGalleryHandler handler, CarouselGallery view)
    {
        handler.adapter?.NotifyDataSetChanged();
    }

    static partial void MapSnapCount(CarouselGalleryHandler handler, CarouselGallery view)
    {
        handler.UpdateSnapMode();
    }

    class CarouselAdapter : RecyclerView.Adapter
    {
        readonly CarouselGallery control;
        readonly IMauiContext mauiContext;
        IList<object> items = [];

        public CarouselAdapter(CarouselGallery control, IMauiContext mauiContext)
        {
            this.control = control;
            this.mauiContext = mauiContext;
        }

        public override int ItemCount => items.Count;

        public void UpdateItems()
        {
            items = control.GetItemsList();
            NotifyDataSetChanged();
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var holder = new MauiViewHolder(mauiContext, parent);

            // iOS raises ItemSelected when a cell is tapped; Android never did.
            holder.ItemView.Click += (_, _) =>
            {
                var position = holder.BindingAdapterPosition;
                if (position != RecyclerView.NoPosition && position < items.Count)
                    control.RaiseItemSelected(items[position], position);
            };
            return holder;
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            if (holder is not MauiViewHolder mauiHolder || position >= items.Count)
                return;

            mauiHolder.Bind(control, items[position], position);

            // Every item is ItemWidth x ItemHeight - set after Bind, which sizes the item to its content
            // for list-style collections and so overwrote a size set before it (the cards shrank to their
            // icon and label). RecyclerView's own LayoutParams, so the layout manager keeps them.
            var density = mauiContext.Context!.Resources!.DisplayMetrics!.Density;
            holder.ItemView.LayoutParameters = new RecyclerView.LayoutParams(
                (int)(control.ItemWidth * density),
                (int)(control.ItemHeight * density));

            // The template's view fills the item, as the cell's content does on iOS. It went in with
            // wrap-content params, so each card shrank to its label and icon - tiny tiles huddled
            // together with the item's space empty around them.
            if (mauiHolder.PlatformChild is { } child
                && child.LayoutParameters is not { Width: ViewGroup.LayoutParams.MatchParent, Height: ViewGroup.LayoutParams.MatchParent })
            {
                child.LayoutParameters = new Android.Widget.FrameLayout.LayoutParams(
                    ViewGroup.LayoutParams.MatchParent,
                    ViewGroup.LayoutParams.MatchParent);
            }
        }
    }

    /// <summary>
    /// ItemSpacing between items (iOS: MinimumLineSpacing), and items centred vertically in the carousel
    /// as the iOS flow layout does - Android ignored ItemSpacing and pinned every item to the top.
    /// </summary>
    class CarouselItemDecoration : RecyclerView.ItemDecoration
    {
        readonly CarouselGallery gallery;

        public CarouselItemDecoration(CarouselGallery gallery) => this.gallery = gallery;

        public override void GetItemOffsets(Android.Graphics.Rect outRect, AView view, RecyclerView parent, RecyclerView.State state)
        {
            var density = parent.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
            var position = parent.GetChildAdapterPosition(view);

            outRect.Left = position > 0 ? (int)(gallery.ItemSpacing * density) : 0;

            var available = parent.Height - parent.PaddingTop - parent.PaddingBottom;
            var itemHeight = (int)(gallery.ItemHeight * density);
            outRect.Top = Math.Max(0, (available - itemHeight) / 2);
        }
    }

    class CarouselScrollListener : RecyclerView.OnScrollListener
    {
        readonly CarouselGalleryHandler handler;

        public CarouselScrollListener(CarouselGalleryHandler handler) => this.handler = handler;

        public override void OnScrolled(RecyclerView recyclerView, int dx, int dy)
        {
            handler.ApplyScaleTransforms();
        }

        public override void OnScrollStateChanged(RecyclerView recyclerView, int newState)
        {
            if (newState == RecyclerView.ScrollStateIdle)
                handler.OnScrollSettled();
        }
    }
}
#endif
