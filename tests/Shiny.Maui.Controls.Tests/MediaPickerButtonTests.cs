using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Shiny.Maui.Controls.Media;
using Shouldly;
using Xunit;
using Carousel = Shiny.Maui.Controls.CarouselGallery.CarouselGallery;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// What a screen can change about the picker without picking anything: which photos may be removed,
/// how far apart the thumbnails sit, and what the add trigger looks like.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class MediaPickerButtonTests
{
    public MediaPickerButtonTests()
    {
        TestDispatcherProvider.Install();
        _ = new Application();
    }


    static MediaPickerItem Photo(bool canRemove = true)
        => new([1, 2, 3], 10, 10, "image/jpeg") { CanRemove = canRemove };

    static VerticalStackLayout Root(MediaPickerButton picker) => (VerticalStackLayout)picker.Content;

    static Carousel CarouselOf(MediaPickerButton picker)
        => (Carousel)((ContentView)Root(picker).Children[0]).Content;

    static ContentView AddHostOf(MediaPickerButton picker) => (ContentView)Root(picker).Children[1];

    static Button RemoveButtonFor(MediaPickerButton picker, MediaPickerItem item)
    {
        var view = (View)CarouselOf(picker).ItemTemplate!.CreateContent();
        view.BindingContext = item;
        return ((Grid)((Border)view).Content).Children.OfType<Button>().Single();
    }


    [Fact]
    public void ALockedPhotoHasNoRemoveButton()
    {
        var locked = Photo(canRemove: false);
        var loose = Photo();
        var picker = new MediaPickerButton { MaxPhotos = 5, Photos = new ObservableCollection<MediaPickerItem> { locked, loose } };

        RemoveButtonFor(picker, locked).IsVisible.ShouldBeFalse();
        RemoveButtonFor(picker, loose).IsVisible.ShouldBeTrue();
    }


    [Fact]
    public void ALockedPhotoSurvivesItsRemoveButton()
    {
        var locked = Photo(canRemove: false);
        var photos = new ObservableCollection<MediaPickerItem> { locked };
        var picker = new MediaPickerButton { Photos = photos };

        // Even a stale button (one bound before the lock) cannot take it out.
        var button = RemoveButtonFor(picker, locked);
        button.SendClicked();

        photos.ShouldBe([locked]);
    }


    [Fact]
    public void ItemSpacingReachesTheCarousel()
    {
        var picker = new MediaPickerButton { Photos = new ObservableCollection<MediaPickerItem> { Photo() } };

        CarouselOf(picker).ItemSpacing.ShouldBe(0);

        picker.ItemSpacing = 16;

        CarouselOf(picker).ItemSpacing.ShouldBe(16);
    }


    [Fact]
    public void AddButtonTemplateReplacesTheDefaultTrigger_AndUnsettingItRestoresIt()
    {
        var picker = new MediaPickerButton();
        var standard = AddHostOf(picker).Content;

        picker.AddButtonTemplate = new DataTemplate(() => new Label { Text = "Snap one" });

        AddHostOf(picker).Content.ShouldBeOfType<Label>().Text.ShouldBe("Snap one");
        AddHostOf(picker).GestureRecognizers.OfType<TapGestureRecognizer>().ShouldHaveSingleItem();

        picker.AddButtonTemplate = null;

        AddHostOf(picker).Content.ShouldBeSameAs(standard);
    }


    [Fact]
    public void ACustomTriggerStillHidesAtMaxPhotos()
    {
        var picker = new MediaPickerButton
        {
            MaxPhotos = 2,
            Photos = new ObservableCollection<MediaPickerItem> { Photo() },
            AddButtonTemplate = new DataTemplate(() => new Label { Text = "Snap one" })
        };

        AddHostOf(picker).IsVisible.ShouldBeTrue();

        picker.MaxPhotos = 1;

        AddHostOf(picker).IsVisible.ShouldBeFalse();
    }
}
