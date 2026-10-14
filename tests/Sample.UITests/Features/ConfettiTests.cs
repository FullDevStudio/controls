namespace Sample.UITests.Features;

public class ConfettiTests : UITest
{
    Task OpenPage() => Open("confetti", "ConfettiPage");

    Task<string?> Status() => Driver.GetProperty("Text", automationId: "ConfettiStatus");


    [Test]
    public async Task FireFrom_Reports_When_The_Burst_Lands()
    {
        await OpenPage();
        await Driver.SetProperty("Text", "", automationId: "ConfettiStatus");

        await Driver.Tap("FromElementButton");

        // "In the air..." is set synchronously before the await, so it can be gone again by the time
        // we look (reduced motion lands immediately) - only the landing is guaranteed observable.
        await Driver.WaitForProperty("ConfettiStatus", "Text", "Landed.", 20);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Clear_Completes_A_Burst_In_Flight()
    {
        await OpenPage();

        // reset the label so the landing below is this test's
        await Driver.SetProperty("Text", "", automationId: "ConfettiStatus");

        await Driver.Tap("FromElementButton");
        await Driver.Tap("ClearButton");

        // Clear cancels the run, completing FireFromAsync well before the ~3s a burst takes to land
        await Driver.WaitForProperty("ConfettiStatus", "Text", "Landed.", 2);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Attached_Trigger_Buttons_Burst()
    {
        await OpenPage();

        await Driver.Tap("BurstButton");
        await Driver.Tap("RandomButton");
        await Driver.Tap("EmojiButton");

        // the burst draws on a canvas the page's confetti layer hosts
        await Driver.WaitUntilTypeExists("ConfettiCanvas", 10);

        await Driver.Tap("ClearButton");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Service_Presets_Fire_And_Clear()
    {
        await OpenPage();

        await Driver.Tap("FireworksButton");
        await Driver.Tap("SideCannonsButton");
        await Driver.WaitUntilTypeExists("ConfettiCanvas", 10);

        await Driver.Tap("ClearButton");
        await AssertNoAppErrors();
    }
}
