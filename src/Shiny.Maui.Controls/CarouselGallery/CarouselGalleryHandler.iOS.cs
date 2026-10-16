#if IOS || MACCATALYST
using CoreGraphics;
using Foundation;
using Microsoft.Maui.Handlers;
using ObjCRuntime;
using Shiny.Maui.Controls.Collections;
using UIKit;

namespace Shiny.Maui.Controls.CarouselGallery;

public partial class CarouselGalleryHandler : ViewHandler<CarouselGallery, UICollectionView>
{
    CarouselFlowLayout? flowLayout;
    CarouselDataSource? dataSource;
    CarouselDelegate? collectionDelegate;
    bool isUpdatingPosition;

    protected override UICollectionView CreatePlatformView()
    {
        flowLayout = new CarouselFlowLayout(VirtualView);

        var collectionView = new UICollectionView(CGRect.Empty, flowLayout)
        {
            BackgroundColor = UIColor.Clear,
            ShowsHorizontalScrollIndicator = false,
            DecelerationRate = VirtualView.SnapCount >= 1
                ? UIScrollView.DecelerationRateFast
                : UIScrollView.DecelerationRateNormal,
            ClipsToBounds = false
        };

        collectionView.RegisterClassForCell(typeof(MauiCollectionViewCell), MauiCollectionViewCell.ReuseId);

        return collectionView;
    }

    protected override void ConnectHandler(UICollectionView platformView)
    {
        base.ConnectHandler(platformView);
        dataSource = new CarouselDataSource(VirtualView, MauiContext!);
        collectionDelegate = new CarouselDelegate(this);
        platformView.DataSource = dataSource;
        platformView.Delegate = collectionDelegate;
        UpdatePeekInsets();
    }

    protected override void DisconnectHandler(UICollectionView platformView)
    {
        platformView.DataSource = null;
        platformView.Delegate = null;
        dataSource?.Dispose();
        collectionDelegate?.Dispose();
        base.DisconnectHandler(platformView);
    }

    void UpdatePeekInsets()
    {
        if (PlatformView is null)
            return;

        var insets = VirtualView.PeekAreaInsets;
        PlatformView.ContentInset = new UIEdgeInsets(
            (nfloat)insets.Top, (nfloat)insets.Left,
            (nfloat)insets.Bottom, (nfloat)insets.Right);
    }

    void ApplyScaleTransforms()
    {
        if (PlatformView is null)
            return;

        var midX = PlatformView.ContentOffset.X + PlatformView.Bounds.Width / 2;

        foreach (var cell in PlatformView.VisibleCells)
        {
            var cellMidX = cell.Center.X;
            var distance = Math.Abs(midX - cellMidX);
            var maxDistance = PlatformView.Bounds.Width / 2;
            var normalizedDist = Math.Min(1.0, distance / maxDistance);

            var scale = VirtualView.FocusedItemScale +
                (VirtualView.UnfocusedItemScale - VirtualView.FocusedItemScale) * normalizedDist;

            cell.Transform = CGAffineTransform.MakeScale((nfloat)scale, (nfloat)scale);
        }
    }

    void OnScrollSettled()
    {
        if (isUpdatingPosition || PlatformView is null)
            return;

        var centerPoint = new CGPoint(
            PlatformView.ContentOffset.X + PlatformView.Bounds.Width / 2,
            PlatformView.Bounds.Height / 2);

        var indexPath = PlatformView.IndexPathForItemAtPoint(centerPoint);
        if (indexPath is not null && indexPath.Row != VirtualView.CurrentPosition)
        {
            isUpdatingPosition = true;
            VirtualView.RaisePositionChanged(indexPath.Row);
            isUpdatingPosition = false;
        }
    }

    static partial void MapItemsSource(CarouselGalleryHandler handler, CarouselGallery view)
    {
        handler.dataSource?.UpdateItems();
        handler.PlatformView?.ReloadData();
    }

    static partial void MapCurrentPosition(CarouselGalleryHandler handler, CarouselGallery view)
    {
        if (handler.isUpdatingPosition || handler.PlatformView is null)
            return;

        handler.PlatformView.ScrollToItem(
            NSIndexPath.FromRowSection(view.CurrentPosition, 0),
            UICollectionViewScrollPosition.CenteredHorizontally,
            true);
    }

    static partial void MapItemSize(CarouselGalleryHandler handler, CarouselGallery view)
    {
        if (handler.flowLayout is not null)
        {
            handler.flowLayout.ItemSize = new CGSize(view.ItemWidth, view.ItemHeight);
            handler.flowLayout.MinimumLineSpacing = (nfloat)view.ItemSpacing;
            handler.flowLayout.InvalidateLayout();
        }
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
        handler.PlatformView?.ReloadData();
    }

    static partial void MapSnapCount(CarouselGalleryHandler handler, CarouselGallery view)
    {
        if (handler.PlatformView is null)
            return;

        handler.PlatformView.DecelerationRate = view.SnapCount >= 1
            ? UIScrollView.DecelerationRateFast
            : UIScrollView.DecelerationRateNormal;

        handler.flowLayout?.InvalidateLayout();
    }

    class CarouselFlowLayout : UICollectionViewFlowLayout
    {
        readonly CarouselGallery gallery;

        public CarouselFlowLayout(CarouselGallery gallery)
        {
            this.gallery = gallery;
            ScrollDirection = UICollectionViewScrollDirection.Horizontal;
            MinimumLineSpacing = (nfloat)gallery.ItemSpacing;
            ItemSize = new CGSize(gallery.ItemWidth, gallery.ItemHeight);
        }

        public override CGPoint TargetContentOffset(CGPoint proposedContentOffset, CGPoint scrollingVelocity)
        {
            if (CollectionView is null)
                return proposedContentOffset;

            // Free scroll mode — no snapping
            if (gallery.SnapCount == 0)
                return proposedContentOffset;

            var cvBounds = CollectionView.Bounds;
            var halfWidth = cvBounds.Width / 2;
            var proposedCenterX = proposedContentOffset.X + halfWidth;

            var attrs = LayoutAttributesForElementsInRect(new CGRect(
                proposedContentOffset.X, 0, cvBounds.Width, cvBounds.Height));

            if (attrs is null || attrs.Length == 0)
                return proposedContentOffset;

            UICollectionViewLayoutAttributes? closest = null;
            var minDistance = double.MaxValue;

            foreach (var attr in attrs)
            {
                var distance = (double)Math.Abs(attr.Center.X - proposedCenterX);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = attr;
                }
            }

            if (closest is null)
                return proposedContentOffset;

            return new CGPoint(closest.Center.X - halfWidth, proposedContentOffset.Y);
        }

        public override bool ShouldInvalidateLayoutForBoundsChange(CGRect newBounds) => true;
    }

    class CarouselDataSource : UICollectionViewDataSource
    {
        readonly CarouselGallery control;
        readonly IMauiContext mauiContext;
        IList<object> items = [];

        public CarouselDataSource(CarouselGallery control, IMauiContext mauiContext)
        {
            this.control = control;
            this.mauiContext = mauiContext;
            UpdateItems();
        }

        public void UpdateItems() => items = control.GetItemsList();

        public override nint GetItemsCount(UICollectionView collectionView, nint section) => items.Count;

        public override UICollectionViewCell GetCell(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var cell = (MauiCollectionViewCell)collectionView.DequeueReusableCell(
                MauiCollectionViewCell.ReuseId, indexPath);

            if (indexPath.Row < items.Count)
                cell.Bind(control, mauiContext, items[indexPath.Row]);

            return cell;
        }
    }

    class CarouselDelegate : UICollectionViewDelegate
    {
        readonly CarouselGalleryHandler handler;

        public CarouselDelegate(CarouselGalleryHandler handler) => this.handler = handler;

        public override void ItemSelected(UICollectionView collectionView, NSIndexPath indexPath)
        {
            var items = handler.VirtualView.GetItemsList();
            if (indexPath.Row < items.Count)
                handler.VirtualView.RaiseItemSelected(items[indexPath.Row], indexPath.Row);
        }

        public override void Scrolled(UIScrollView scrollView)
        {
            handler.ApplyScaleTransforms();
        }

        public override void DecelerationEnded(UIScrollView scrollView)
        {
            handler.OnScrollSettled();
        }

        public override void ScrollAnimationEnded(UIScrollView scrollView)
        {
            handler.OnScrollSettled();
        }
    }
}
#endif
