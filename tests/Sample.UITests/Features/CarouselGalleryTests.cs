namespace Sample.UITests.Features;

[SkipOn(UIPlatform.MacOS, KnownGaps.CarouselGalleryNoHandler)]
[SkipOn(UIPlatform.Linux, KnownGaps.CarouselGalleryNoHandler)]
public class CarouselGalleryTests : UITest
{
    Task OpenPage() => Open("carouselgallery", "CarouselGalleryPage");

    [Test]
    public async Task Toggle_Snap_Switches_To_Free_Scroll_And_Back()
    {
        await OpenPage();
        await Driver.WaitForProperty("SnapLabel", "Text", "Snap (1)");

        await Driver.Tap("ToggleSnapButton");
        await Driver.WaitForProperty("SnapLabel", "Text", "Free Scroll");
        await Driver.WaitForProperty("CarouselStatusLabel", "Text", "Free scroll — Netflix-style smooth scrolling");
        await Driver.WaitForProperty("BasicCarousel", "SnapCount", "0");

        await Driver.Tap("ToggleSnapButton");
        await Driver.WaitForProperty("SnapLabel", "Text", "Snap (1)");
        await Driver.WaitForProperty("BasicCarousel", "SnapCount", "1");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Toggle_Scale_Turns_The_Focus_Effect_Off_And_On()
    {
        await OpenPage();
        await Driver.WaitForProperty("ScaleLabel", "Text", "Scale (0.85)");
        await Driver.WaitForProperty("ScaleCarousel", "UnfocusedItemScale", "0.85");

        await Driver.Tap("ToggleScaleButton");
        await Driver.WaitForProperty("ScaleLabel", "Text", "Scale Off");
        await Driver.WaitForProperty("CarouselStatusLabel", "Text", "Scale effect off — all items same size");
        await Driver.WaitForProperty("ScaleCarousel", "UnfocusedItemScale", "1");
        await Driver.WaitForProperty("BasicCarousel", "UnfocusedItemScale", "1");

        await Driver.Tap("ToggleScaleButton");
        await Driver.WaitForProperty("ScaleLabel", "Text", "Scale (0.85)");
        await Driver.WaitForProperty("ScaleCarousel", "UnfocusedItemScale", "0.85");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Setting_CurrentPosition_Scrolls_And_Sticks()
    {
        await OpenPage();
        await Driver.WaitForProperty("CarouselPositionLabel", "Text", "Position: 0");

        await Driver.SetProperty("CurrentPosition", "5", automationId: "BasicCarousel");
        await Driver.WaitForProperty("CarouselPositionLabel", "Text", "Position: 5");

        // The native scroll it triggers must not report a different position back.
        await Task.Delay(1500);
        await Assert.That(await Driver.GetProperty("CurrentPosition", automationId: "BasicCarousel")).IsEqualTo("5");
        await Assert.That(await Driver.GetProperty("Text", automationId: "CarouselPositionLabel")).IsEqualTo("Position: 5");
        await AssertNoAppErrors();
    }
}
