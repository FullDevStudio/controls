namespace Sample.UITests.Features;

public class BadgeTests : UITest
{
    Task OpenPage() => Open("badge", "BadgePage");

    // BadgeView is a Grid: Children[0] hosts the content, Children[1] is the badge Border whose
    // Content is the count Label. The agent's property paths can't index, so the badge's own
    // bindables are the seam, plus a text query for what the label displays.
    async Task<bool> LabelShowing(string text)
        => (await Driver.Query(text: text)).Any(e => e.Text == text);


    [Test]
    public async Task Increment_Raises_The_Count()
    {
        await OpenPage();

        var before = await Driver.GetProperty("Text", automationId: "MailBadge");
        var expected = Int32.TryParse(before, out var n) ? (n + 1).ToString() : "1";

        await Driver.Tap("IncrementMailButton");

        await Driver.WaitForProperty("MailBadge", "Text", expected);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Clear_Empties_The_Badge_And_Increment_Restarts_At_One()
    {
        await OpenPage();

        await Driver.Tap("ClearMailButton");
        await Driver.WaitUntil(async () => String.IsNullOrEmpty(await Driver.GetProperty("Text", automationId: "MailBadge")), "the mail badge is empty");

        await Driver.Tap("IncrementMailButton");
        await Driver.WaitForProperty("MailBadge", "Text", "1");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Count_Above_MaxCount_Displays_Overflow()
    {
        await OpenPage();

        // CartCount is 127 with MaxCount 99
        await Driver.WaitForProperty("CartBadge", "Text", "127");
        await Driver.WaitForProperty("CartBadge", "MaxCount", "99");
        await Driver.WaitUntil(() => LabelShowing("99+"), "the cart badge reads 99+");
    }


    [Test]
    public async Task Toggle_Dot_Flips_IsDot()
    {
        await OpenPage();

        var before = await Driver.GetProperty("IsDot", automationId: "DotBadge");
        var flipped = before == "True" ? "False" : "True";

        await Driver.Tap("ToggleDotButton");
        await Driver.WaitForProperty("DotBadge", "IsDot", flipped);

        await Driver.Tap("ToggleDotButton");
        await Driver.WaitForProperty("DotBadge", "IsDot", before!);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Toggle_Pulse_Flips_IsPulsing()
    {
        await OpenPage();

        var before = await Driver.GetProperty("IsPulsing", automationId: "PulseBadge");
        var flipped = before == "True" ? "False" : "True";

        await Driver.Tap("TogglePulseButton");
        await Driver.WaitForProperty("PulseBadge", "IsPulsing", flipped);

        // the badge keeps its text either way
        await Driver.WaitForProperty("PulseBadge", "Text", "NEW");

        await Driver.Tap("TogglePulseButton");
        await Driver.WaitForProperty("PulseBadge", "IsPulsing", before!);
        await AssertNoAppErrors();
    }
}
