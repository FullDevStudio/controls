namespace Sample.UITests.Features;

[SkipOn(UIPlatform.MacOS, "marquee page not in the macOS head")]
[SkipOn(UIPlatform.Linux, "marquee page not in the Linux head")]
public class MarqueeTests : UITest
{
    Task OpenPage() => Open("marquee", "MarqueePage");


    [Test]
    public async Task Running_Switch_Stops_And_Starts_The_Ticker()
    {
        await OpenPage();

        await Driver.SetProperty("IsToggled", "True", automationId: "RunningSwitch");
        await Driver.WaitForProperty("TickerMarquee", "IsRunning", "True");
        await Driver.WaitForProperty("TickerMarquee", "IsPaused", "False");

        await Driver.SetProperty("IsToggled", "False", automationId: "RunningSwitch");
        await Driver.WaitForProperty("TickerMarquee", "IsRunning", "False");
        await Driver.WaitForProperty("TickerMarquee", "IsPaused", "True");

        await Driver.SetProperty("IsToggled", "True", automationId: "RunningSwitch");
        await Driver.WaitForProperty("TickerMarquee", "IsRunning", "True");
        await Driver.WaitForProperty("TickerMarquee", "IsPaused", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task IsRunning_False_Pauses_Any_Marquee()
    {
        await OpenPage();

        await Driver.SetProperty("IsRunning", "False", automationId: "ReviewsMarquee");
        await Driver.WaitForProperty("ReviewsMarquee", "IsPaused", "True");

        // the reversed row is independent
        await Driver.WaitForProperty("ReviewsReversedMarquee", "IsPaused", "False");

        await Driver.SetProperty("IsRunning", "True", automationId: "ReviewsMarquee");
        await Driver.WaitForProperty("ReviewsMarquee", "IsPaused", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Upright_Switch_Drives_KeepContentUpright()
    {
        await OpenPage();

        await Driver.SetProperty("IsToggled", "False", automationId: "UprightSwitch");
        await Driver.WaitForProperty("AngledMarquee", "KeepContentUpright", "False");

        await Driver.SetProperty("IsToggled", "True", automationId: "UprightSwitch");
        await Driver.WaitForProperty("AngledMarquee", "KeepContentUpright", "True");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Items_Render_Through_The_Template_And_Repeat()
    {
        await OpenPage();

        // two review rows, each laying its items end to end at least twice so the loop has no seam
        await Driver.WaitUntil(async () => (await Driver.Query(text: "@jenny")).Count(e => e.Text == "@jenny") >= 4,
            "each review row renders the template and repeats it");
        await Driver.WaitForProperty("ReviewsReversedMarquee", "Reverse", "True");
    }
}
