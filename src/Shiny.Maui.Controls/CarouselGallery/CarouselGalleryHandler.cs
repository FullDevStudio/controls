#if ANDROID || IOS || MACCATALYST || WINDOWS
using Microsoft.Maui.Handlers;

namespace Shiny.Maui.Controls.CarouselGallery;

public partial class CarouselGalleryHandler
{
    public static IPropertyMapper<CarouselGallery, CarouselGalleryHandler> Mapper =
        new PropertyMapper<CarouselGallery, CarouselGalleryHandler>(ViewHandler.ViewMapper)
        {
            [nameof(CarouselGallery.ItemsSource)] = MapItemsSource,
            [nameof(CarouselGallery.CurrentPosition)] = MapCurrentPosition,
            [nameof(CarouselGallery.ItemWidth)] = MapItemSize,
            [nameof(CarouselGallery.ItemHeight)] = MapItemSize,
            [nameof(CarouselGallery.ItemSpacing)] = MapItemSize,
            [nameof(CarouselGallery.FocusedItemScale)] = MapScaling,
            [nameof(CarouselGallery.UnfocusedItemScale)] = MapScaling,
            [nameof(CarouselGallery.PeekAreaInsets)] = MapPeekInsets,
            [nameof(CarouselGallery.ItemTemplate)] = MapItemTemplate,
            [nameof(CarouselGallery.ItemTemplateSelector)] = MapItemTemplate,
            [nameof(CarouselGallery.SnapCount)] = MapSnapCount,
        };

    public static CommandMapper<CarouselGallery, CarouselGalleryHandler> CommandMapper =
        new(ViewHandler.ViewCommandMapper);

    public CarouselGalleryHandler() : base(Mapper, CommandMapper)
    {
    }

    static partial void MapItemsSource(CarouselGalleryHandler handler, CarouselGallery view);
    static partial void MapCurrentPosition(CarouselGalleryHandler handler, CarouselGallery view);
    static partial void MapItemSize(CarouselGalleryHandler handler, CarouselGallery view);
    static partial void MapScaling(CarouselGalleryHandler handler, CarouselGallery view);
    static partial void MapPeekInsets(CarouselGalleryHandler handler, CarouselGallery view);
    static partial void MapItemTemplate(CarouselGalleryHandler handler, CarouselGallery view);
    static partial void MapSnapCount(CarouselGalleryHandler handler, CarouselGallery view);
}
#endif
