namespace Sample.UITests.Features;

/// <summary>AnimatedBackground on the Animated Backgrounds page.</summary>
[SkipOn(UIPlatform.Linux, "animated backgrounds page not in the Linux head")]
public class AnimatedBackgroundTests : UITest
{
    Task OpenPage() => Open("animatedbackgrounds", "AnimatedBackgroundsPage");

    static int Clicks(string? text)
        => Int32.Parse(text!.Split('(', ')')[1]);


    [Test]
    public async Task Choosing_An_Effect_Swaps_It_On_The_Hero()
    {
        await OpenPage();
        try
        {
            await Driver.SetProperty("SelectedIndex", "13", automationId: "EffectPicker");
            await Driver.WaitForProperty("HeroTitle", "Text", "Matrix");
            await Driver.WaitUntil(
                async () => (await Driver.GetProperty("Effect", automationId: "HeroBackground"))?.Contains("MatrixEffect") == true,
                "the hero to be given a MatrixEffect"
            );
        }
        finally
        {
            await Driver.SetProperty("SelectedIndex", "0", automationId: "EffectPicker");
            await Driver.WaitForProperty("HeroTitle", "Text", "Aurora");
        }
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Running_Switch_Stops_And_Starts_The_Effect()
    {
        await OpenPage();
        try
        {
            await Driver.SetProperty("IsToggled", "False", automationId: "RunningSwitch");
            await Driver.WaitForProperty("HeroBackground", "IsRunning", "False");
        }
        finally
        {
            await Driver.SetProperty("IsToggled", "True", automationId: "RunningSwitch");
        }
        await Driver.WaitForProperty("HeroBackground", "IsRunning", "True");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Content_On_Top_Gets_Its_Taps()
    {
        await OpenPage();
        var before = Clicks(await Driver.GetProperty("Text", automationId: "HeroButton"));

        await Driver.Tap("HeroButton");
        await Driver.WaitForProperty("HeroButton", "Text", $"A real button ({before + 1})");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Real_Tap_Goes_Through_To_The_Content()
    {
        // The regression: on Android the background's pointer recognizer claimed the press, so a real
        // finger never reached the Button on top - while DevFlow's tap, which skips the touch, passed.
        if (!HasRealTouch)
            Skip.Test("needs real touch input (Android)");

        await OpenPage();
        var before = Clicks(await Driver.GetProperty("Text", automationId: "HeroButton"));

        await TouchTap("HeroButton");
        await Driver.WaitForProperty("HeroButton", "Text", $"A real button ({before + 1})", timeoutSeconds: 5);
        await AssertNoAppErrors();
    }


    [Test]
    [Arguments("AuroraCard")]
    [Arguments("SnowCard")]
    [Arguments("MatrixCard")]
    [Arguments("WavesCard")]
    public async Task Gallery_Effect_Paints_And_Moves(string card)
    {
        await OpenPage();
        await Driver.Scroll(card);
        await Task.Delay(1500);

        // Two captures of the card a second apart: an effect that paints nothing (Matrix on iOS drew
        // no glyphs at all) or never advances gives the same image twice.
        var first = await File.ReadAllBytesAsync(await Driver.Screenshot($"{card}-a.png", card));
        await Task.Delay(1000);
        var second = await File.ReadAllBytesAsync(await Driver.Screenshot($"{card}-b.png", card));

        await Assert.That(first.AsSpan().SequenceEqual(second)).IsFalse();
        await AssertNoAppErrors();
    }
}
