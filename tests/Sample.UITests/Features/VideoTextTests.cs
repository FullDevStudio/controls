namespace Sample.UITests.Features;

/// <summary>
/// VideoText's page options reach the control's bindables. Nothing waits on the remote video, so these
/// hold offline and on heads without a masked output (Windows, Linux show a plain label instead).
/// </summary>
public class VideoTextTests : UITest
{
    // Shell keeps the page alive between visits, so each test sets the state it starts from.
    Task OpenPage() => Open("videotext", "VideoTextPage");


    [Test]
    public async Task Defaults_Are_A_Black_Solid_Overlay_With_No_Marquee()
    {
        await OpenPage();
        await Driver.SetProperty("SelectedIndex", "0", automationId: "VideoTextOverlay");
        await Driver.SetProperty("SelectedIndex", "0", automationId: "VideoTextMarquee");

        await Driver.WaitForProperty("VideoText", "OverlayColor", "#000000");
        await Driver.WaitForProperty("VideoText", "Marquee", "None");
        await Driver.WaitForProperty("VideoText", "MarqueeDuration", "00:00:08");
        await Driver.WaitForProperty("VideoText", "IsMuted", "True");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Overlay_Picker_Sets_The_Overlay_Colour()
    {
        await OpenPage();
        await Driver.SetProperty("SelectedIndex", "1", automationId: "VideoTextOverlay");
        await Driver.WaitForProperty("VideoText", "OverlayColor", "#FFFFFF");

        await Driver.SetProperty("SelectedIndex", "2", automationId: "VideoTextOverlay");
        await Driver.WaitForProperty("VideoText", "OverlayColor", "#312E81");

        // "None" drops the sheet: the video is clipped to the letters instead
        await Driver.SetProperty("SelectedIndex", "3", automationId: "VideoTextOverlay");
        await Driver.WaitUntil(
            async () => String.IsNullOrEmpty(await Driver.GetProperty("OverlayColor", automationId: "VideoText")),
            "the overlay colour to clear");

        await Driver.SetProperty("SelectedIndex", "0", automationId: "VideoTextOverlay");
        await Driver.WaitForProperty("VideoText", "OverlayColor", "#000000");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Opacity_Slider_Is_Two_Way_And_Clamped()
    {
        await OpenPage();
        await Driver.SetProperty("Value", "0.25", automationId: "VideoTextOpacity");
        await Driver.WaitUntil(async () => await Driver.GetDouble("OverlayOpacity", "VideoText") == 0.25, "0.25 to reach the control");

        await Driver.SetProperty("OverlayOpacity", "0.75", automationId: "VideoText");
        await Driver.WaitUntil(async () => await Driver.GetDouble("Value", "VideoTextOpacity") == 0.75, "0.75 to reach the slider");

        // out of range is coerced, not rejected
        await Driver.SetProperty("OverlayOpacity", "3", automationId: "VideoText");
        await Driver.WaitUntil(async () => await Driver.GetDouble("OverlayOpacity", "VideoText") == 1, "3 to clamp to 1");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Marquee_Picker_Sets_Each_Direction()
    {
        await OpenPage();
        string[] directions = ["None", "Left", "Right", "Up", "Down"];

        for (var i = directions.Length - 1; i >= 0; i--)
        {
            await Driver.SetProperty("SelectedIndex", i.ToString(), automationId: "VideoTextMarquee");
            await Driver.WaitForProperty("VideoText", "Marquee", directions[i]);
        }
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Marquee_Runs_Over_A_See_Through_Overlay_Without_Errors()
    {
        await OpenPage();
        await Driver.WaitForProperty("MarqueeVideoText", "Marquee", "Left");
        await Driver.WaitForProperty("MarqueeVideoText", "OverlayOpacity", "0.7");

        await Driver.SetProperty("MarqueeDuration", "00:00:02", automationId: "MarqueeVideoText");
        await Driver.WaitForProperty("MarqueeVideoText", "MarqueeDuration", "00:00:02");

        // a pass or two with the marquee and the video both drawing
        await Task.Delay(3000);

        await Driver.SetProperty("MarqueeDuration", "00:00:06", automationId: "MarqueeVideoText");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Font_Picker_Sets_The_Family()
    {
        await OpenPage();
        await Driver.SetProperty("SelectedIndex", "1", automationId: "VideoTextFont");
        await Driver.WaitForProperty("VideoText", "FontFamily", "OpenSansSemibold");

        await Driver.SetProperty("SelectedIndex", "2", automationId: "VideoTextFont");
        await Driver.WaitForProperty("VideoText", "FontFamily", "OpenSansRegular");

        await Driver.SetProperty("SelectedIndex", "0", automationId: "VideoTextFont");
        await Driver.WaitUntil(
            async () => String.IsNullOrEmpty(await Driver.GetProperty("FontFamily", automationId: "VideoText")),
            "the system font to come back");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Text_Size_And_Mute_Options_Reach_The_Control()
    {
        await OpenPage();
        await Driver.SetProperty("Text", "SURF", automationId: "VideoTextEntry");
        await Driver.WaitForProperty("VideoText", "Text", "SURF");

        await Driver.SetProperty("Value", "96", automationId: "VideoTextSize");
        await Driver.WaitUntil(async () => await Driver.GetDouble("FontSize", "VideoText") == 96, "96 to reach the control");

        await Driver.SetProperty("IsToggled", "False", automationId: "VideoTextMuted");
        await Driver.WaitForProperty("VideoText", "IsMuted", "False");

        await Driver.SetProperty("IsToggled", "True", automationId: "VideoTextItalic");
        await Driver.WaitForProperty("VideoText", "FontAttributes", "Bold, Italic");

        // back to the page's defaults
        await Driver.SetProperty("IsToggled", "False", automationId: "VideoTextItalic");
        await Driver.SetProperty("IsToggled", "True", automationId: "VideoTextMuted");
        await Driver.SetProperty("Value", "0", automationId: "VideoTextSize");
        await Driver.SetProperty("Text", "BLOOM", automationId: "VideoTextEntry");
        await Driver.WaitForProperty("VideoText", "Text", "BLOOM");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task No_Overlay_Demo_Is_Transparent_Around_The_Letters()
    {
        await OpenPage();
        await Driver.WaitUntil(
            async () => String.IsNullOrEmpty(await Driver.GetProperty("OverlayColor", automationId: "PlainVideoText")),
            "the plain demo to have no overlay");
        await Driver.WaitForProperty("PlainVideoText", "Text", "IN\nBLOOM");
        await AssertNoAppErrors();
    }
}
