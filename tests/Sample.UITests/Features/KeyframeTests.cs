namespace Sample.UITests.Features;

[SkipOn(UIPlatform.Linux, "keyframe page not in the Linux head")]
public class KeyframeTests : UITest
{
    Task OpenPage() => Open("keyframe", "KeyframePage");

    Task<double?> Progress() => Driver.GetDouble("Progress", "KeyframeScene");


    [Test]
    public async Task Keyframes_Animate_The_Attached_View()
    {
        await OpenPage();

        // PulseBox ping-pongs Scale 1 -> 1.15 forever; SlideBox sweeps TranslationX in from -200
        var scales = new HashSet<double?>();
        var translations = new HashSet<double?>();
        await Driver.WaitUntil(async () =>
        {
            scales.Add(await Driver.GetDouble("Scale", "PulseBox"));
            translations.Add(await Driver.GetDouble("TranslationX", "SlideBox"));
            return scales.Count > 2 && translations.Count > 2;
        }, "the keyframed properties keep changing");

        await Assert.That(scales.All(s => s is >= 0.99 and <= 1.16)).IsTrue();
        await Assert.That(translations.All(t => t is >= -201 and <= 50)).IsTrue();
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Playing_Reports_Progress_Into_The_Scrubber()
    {
        await OpenPage();

        await Driver.Tap("KeyframePlayButton");
        await Driver.WaitForProperty("KeyframeScene", "IsPlaying", "True");

        // Progress is two-way bound to the slider, so playback moves it
        var seen = new HashSet<double?>();
        await Driver.WaitUntil(async () =>
        {
            seen.Add(await Driver.GetDouble("Value", "KeyframeScrubber"));
            return seen.Count > 2;
        }, "the scrubber follows playback");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Pause_Freezes_Progress()
    {
        await OpenPage();

        await Driver.Tap("KeyframePauseButton");
        await Driver.WaitForProperty("KeyframeScene", "IsPlaying", "False");

        var frozen = await Progress();
        await Task.Delay(800);
        await Assert.That(await Progress()).IsEqualTo(frozen);

        await Driver.Tap("KeyframePlayButton");
        await Driver.WaitForProperty("KeyframeScene", "IsPlaying", "True");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Scrubbing_While_Paused_Seeks_The_Scene()
    {
        await OpenPage();

        await Driver.Tap("KeyframePauseButton");
        await Driver.WaitForProperty("KeyframeScene", "IsPlaying", "False");

        await Driver.SetProperty("Value", "0.5", automationId: "KeyframeScrubber");
        await Driver.WaitUntil(async () => Math.Abs((await Progress() ?? -1) - 0.5) < 0.01, "the scene seeks to the slider");

        await Driver.SetProperty("Value", "0.25", automationId: "KeyframeScrubber");
        await Driver.WaitUntil(async () => Math.Abs((await Progress() ?? -1) - 0.25) < 0.01, "the scene seeks back");

        // still paused: the seek doesn't restart playback
        await Task.Delay(500);
        await Assert.That(Math.Abs((await Progress())!.Value - 0.25)).IsLessThan(0.01);

        await Driver.Tap("KeyframePlayButton");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Reverse_Plays_At_A_Negative_Rate()
    {
        await OpenPage();

        await Driver.Tap("KeyframeReverseButton");
        await Driver.WaitForProperty("KeyframeScene", "IsPlaying", "True");
        await Driver.WaitUntil(async () => await Driver.GetDouble("Speed", "KeyframeScene") < 0, "speed is negative");

        await Driver.Tap("KeyframePlayButton");
        await Driver.WaitUntil(async () => await Driver.GetDouble("Speed", "KeyframeScene") > 0, "speed is positive again");
        await AssertNoAppErrors();
    }
}
