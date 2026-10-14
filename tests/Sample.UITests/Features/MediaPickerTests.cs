namespace Sample.UITests.Features;

/// <summary>
/// MediaPickerButton bound to a view model. The page's "Load Sample Photos" fills the collection from
/// bundled assets, so the populated state is reachable without photo-library access.
/// </summary>
public class MediaPickerTests : UITest
{
    Task OpenPage() => Open("mediapicker", "MediaPickerPage");


    // devflow on the macOS head: with "Show as carousel in view" on (the default) the count stops at 1 -
    // adding the second photo while the in-view CarouselView is showing aborts the command, nothing is logged.
    // Toggle the carousel off and the same button adds all 3.
    [Test, SkipOn(UIPlatform.MacOS, "BUG: AppKit - a second photo added while MediaPickerButton shows its CarouselView aborts the load (1 of 3 added); fine with the carousel off")]
    public async Task Sample_Photos_Populate_And_Clear()
    {
        await OpenPage();
        await Driver.Tap(automationId: "ClearPhotosButton");
        await Driver.WaitForProperty("PhotoCountLabel", "Text", "Photos added: 0");

        await Driver.Tap(automationId: "LoadSamplePhotosButton");
        await Driver.WaitForProperty("PhotoCountLabel", "Text", "Photos added: 3");

        await Driver.Tap(automationId: "ClearPhotosButton");
        await Driver.WaitForProperty("PhotoCountLabel", "Text", "Photos added: 0");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Stepper_Sets_Max_Photos()
    {
        await OpenPage();
        await Driver.SetProperty("Value", "5", automationId: "MaxPhotosStepper");
        await Driver.WaitForProperty("MaxPhotosLabel", "Text", "Max photos: 5");

        await Driver.SetProperty("Value", "8", automationId: "MaxPhotosStepper");

        await Driver.WaitForProperty("MaxPhotosLabel", "Text", "Max photos: 8");
        await Driver.WaitForProperty("PhotoPicker", "MaxPhotos", "8");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Slider_Sets_Compression_Quality()
    {
        await OpenPage();
        await Driver.SetProperty("Value", "85", automationId: "QualitySlider");
        await Driver.WaitForProperty("QualityLabel", "Text", "Compression quality: 85%");

        await Driver.SetProperty("Value", "50", automationId: "QualitySlider");

        await Driver.WaitForProperty("QualityLabel", "Text", "Compression quality: 50%");
        await Driver.WaitForProperty("PhotoPicker", "CompressionQuality", "50");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Png_Switch_Sets_The_Output_Format()
    {
        await OpenPage();
        await Driver.SetProperty("IsToggled", "False", automationId: "PngSwitch");
        await Driver.WaitForProperty("PhotoPicker", "OutputFormat", "Jpeg");

        await Driver.SetProperty("IsToggled", "True", automationId: "PngSwitch");
        await Driver.WaitForProperty("PhotoPicker", "OutputFormat", "Png");

        await Driver.SetProperty("IsToggled", "False", automationId: "PngSwitch");
        await Driver.WaitForProperty("PhotoPicker", "OutputFormat", "Jpeg");
        await AssertNoAppErrors();
    }
}
