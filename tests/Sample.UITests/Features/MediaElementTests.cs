namespace Sample.UITests.Features;

/// <summary>
/// MediaElement's page options reach the player's bindables. Nothing here waits on the remote trailer
/// to load, so it holds offline and on a simulator without a codec for it.
/// </summary>
public class MediaElementTests : UITest
{
    // Shell keeps the page alive between visits, so each test sets the state it starts from.
    Task OpenPage() => Open("mediaelement", "MediaElementPage");


    [Test]
    public async Task Mute_Command_Toggles_IsMuted()
    {
        await OpenPage();
        await Driver.SetProperty("IsMuted", "False", automationId: "VideoPlayer");
        await Driver.WaitForProperty("VideoPlayer", "IsMuted", "False");

        await Driver.Tap(automationId: "MuteButton");
        await Driver.WaitForProperty("VideoPlayer", "IsMuted", "True");

        await Driver.Tap(automationId: "MuteButton");
        await Driver.WaitForProperty("VideoPlayer", "IsMuted", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Aspect_Picker_Sets_The_Player_Aspect()
    {
        await OpenPage();
        await Driver.SetProperty("SelectedIndex", "0", automationId: "AspectPicker");
        await Driver.WaitForProperty("VideoPlayer", "Aspect", "AspectFit");

        await Driver.SetProperty("SelectedIndex", "1", automationId: "AspectPicker");
        await Driver.WaitForProperty("VideoPlayer", "Aspect", "AspectFill");

        await Driver.SetProperty("SelectedIndex", "2", automationId: "AspectPicker");
        await Driver.WaitForProperty("VideoPlayer", "Aspect", "Fill");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Speed_Picker_Sets_The_Playback_Rate()
    {
        await OpenPage();
        await Driver.SetProperty("SelectedIndex", "1", automationId: "RatePicker");
        await Driver.WaitUntil(async () => await Driver.GetDouble("PlaybackRate", "VideoPlayer") == 1.0, "1x to reach the player");

        await Driver.SetProperty("SelectedIndex", "3", automationId: "RatePicker");
        await Driver.WaitUntil(async () => await Driver.GetDouble("PlaybackRate", "VideoPlayer") == 2.0, "2x to reach the player");

        await Driver.SetProperty("SelectedIndex", "0", automationId: "RatePicker");
        await Driver.WaitUntil(async () => await Driver.GetDouble("PlaybackRate", "VideoPlayer") == 0.5, "0.5x to reach the player");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Transport_Switches_Are_Two_Way_With_The_Player()
    {
        await OpenPage();
        await Driver.SetProperty("IsToggled", "True", automationId: "TransportBarSwitch");
        await Driver.SetProperty("IsToggled", "True", automationId: "SeekBarSwitch");
        await Driver.WaitForProperty("VideoPlayer", "ShowTransportBar", "True");
        await Driver.WaitForProperty("VideoPlayer", "ShowSeekBar", "True");

        await Driver.SetProperty("IsToggled", "False", automationId: "TransportBarSwitch");
        await Driver.WaitForProperty("VideoPlayer", "ShowTransportBar", "False");

        await Driver.SetProperty("IsToggled", "False", automationId: "SeekBarSwitch");
        await Driver.WaitForProperty("VideoPlayer", "ShowSeekBar", "False");

        // and back from the control to the switch
        await Driver.SetProperty("ShowTransportBar", "True", automationId: "VideoPlayer");
        await Driver.WaitForProperty("TransportBarSwitch", "IsToggled", "True");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Loop_Switch_Sets_IsLooping()
    {
        await OpenPage();
        await Driver.SetProperty("IsToggled", "False", automationId: "LoopSwitch");
        await Driver.WaitForProperty("VideoPlayer", "IsLooping", "False");

        await Driver.SetProperty("IsToggled", "True", automationId: "LoopSwitch");
        await Driver.WaitForProperty("VideoPlayer", "IsLooping", "True");
        await AssertNoAppErrors();
    }
}
