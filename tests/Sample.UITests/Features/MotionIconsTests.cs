namespace Sample.UITests.Features;

[SkipOn(UIPlatform.Windows, KnownGaps.WinUiAnimationBlocksCapture)]
public class MotionIconsTests : UITest
{
    Task OpenPage() => Open("motionicons", "MotionIconsPage");


    [Test]
    public async Task Loop_Trigger_Plays_On_Its_Own()
    {
        await OpenPage();

        await Driver.WaitForProperty("LoopIcon", "IsPlaying", "True");
        // the manual ones stay at rest until asked
        await Driver.WaitForProperty("PlayOnceIcon", "IsPlaying", "False");
    }


    [Test]
    public async Task IsPlaying_Starts_And_Stopping_Resets_The_Spinner()
    {
        await OpenPage();
        await Driver.SetProperty("IsPlaying", "False", automationId: "BusySpinner");

        await Driver.SetProperty("IsPlaying", "True", automationId: "BusySpinner");
        await Driver.WaitUntil(async () => await Driver.GetDouble("Progress", "BusySpinner") > 0, "the spinner to start moving");

        await Driver.SetProperty("IsPlaying", "False", automationId: "BusySpinner");
        await Driver.WaitForProperty("BusySpinner", "IsPlaying", "False");
        await Driver.WaitForProperty("BusySpinner", "Progress", "0");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Toggle_Busy_Starts_And_Stops_The_Spinner()
    {
        await OpenPage();

        // the page's busy flag survives navigation; normalise to stopped first
        if (await Driver.GetProperty("IsPlaying", automationId: "BusySpinner") == "True")
            await Driver.Tap("ToggleBusyButton");
        await Driver.WaitForProperty("BusySpinner", "IsPlaying", "False");

        await Driver.Tap("ToggleBusyButton");
        await Driver.WaitForProperty("BusySpinner", "IsPlaying", "True");

        // a manual Play() would end after one cycle; a set flag keeps looping
        await Task.Delay(2500);
        await Driver.WaitForProperty("BusySpinner", "IsPlaying", "True", 1);

        await Driver.Tap("ToggleBusyButton");
        await Driver.WaitForProperty("BusySpinner", "IsPlaying", "False");
        await Driver.WaitForProperty("BusySpinner", "Progress", "0");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Play_Runs_RepeatCount_Cycles_Then_Stops()
    {
        await OpenPage();

        await Driver.WaitForProperty("PlayOnceIcon", "RepeatCount", "2");
        await Driver.Tap("PlayOnceButton");

        await Driver.WaitForProperty("PlayOnceIcon", "IsPlaying", "True", 3);
        await Driver.WaitForProperty("PlayOnceIcon", "IsPlaying", "False", 10);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Picking_A_Preset_Applies_It_To_Both_Icons()
    {
        await OpenPage();

        // PresetNames is Enum.GetNames<MotionPreset>(): Default, None, Pulse, Beat, Spin, ...
        await Driver.SetProperty("SelectedIndex", "4", automationId: "PresetPicker");
        await Driver.WaitForProperty("PresetSample", "Motion", "Spin");
        await Driver.WaitForProperty("PresetCustom", "Motion", "Spin");

        await Driver.SetProperty("SelectedIndex", "5", automationId: "PresetPicker");
        await Driver.WaitForProperty("PresetSample", "Motion", "Shake");
        await Driver.WaitForProperty("PresetCustom", "Motion", "Shake");

        await Driver.SetProperty("SelectedIndex", "0", automationId: "PresetPicker");
        await Driver.WaitForProperty("PresetSample", "Motion", "Default");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Slider_Scrubs_The_Icon()
    {
        await OpenPage();

        await Driver.SetProperty("Value", "0.5", automationId: "Scrubber");
        await Driver.WaitUntil(async () => Math.Abs((await Driver.GetDouble("Progress", "ScrubbedIcon") ?? -1) - 0.5) < 0.001, "the icon's progress follows the slider");

        await Driver.SetProperty("Value", "0", automationId: "Scrubber");
        await Driver.WaitUntil(async () => await Driver.GetDouble("Progress", "ScrubbedIcon") == 0, "back to rest");
        await Driver.WaitForProperty("ScrubbedIcon", "IsPlaying", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Filter_Narrows_The_Gallery()
    {
        await OpenPage();

        await Driver.SetProperty("Text", "", automationId: "IconFilter");
        var all = await Driver.GetProperty("Text", automationId: "GalleryCaption");
        await Assert.That(all).Contains("icons, each with its own motion.");

        await Driver.SetProperty("Text", "arrow", automationId: "IconFilter");
        await Driver.WaitUntil(async () =>
            System.Text.RegularExpressions.Regex.IsMatch(await Driver.GetProperty("Text", automationId: "GalleryCaption") ?? "", @"^\d+ of \d+ icons\.$"),
            "the caption reports a filtered count");

        // gallery labels are the icon names; nothing without "arrow" is left
        await Driver.WaitUntil(async () => (await Driver.Query(text: "heart")).All(e => e.Text != "heart"), "non-matching icons are gone");
        await Driver.WaitUntil(async () => (await Driver.Query(text: "arrow-down")).Any(e => e.Text == "arrow-down"), "matching icons remain");

        await Driver.SetProperty("Text", "", automationId: "IconFilter");
        await Driver.WaitForProperty("GalleryCaption", "Text", all!);
        await AssertNoAppErrors();
    }
}
