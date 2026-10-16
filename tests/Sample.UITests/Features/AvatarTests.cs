namespace Sample.UITests.Features;

/// <summary>Avatar and AvatarGroup on the Avatar page.</summary>
public class AvatarTests : UITest
{
    static readonly string[] Cycle = ["None", "Online", "Away", "Busy", "Offline"];

    Task OpenPage() => Open("avatar", "AvatarPage");

    static string After(string status) => Cycle[(Array.IndexOf(Cycle, status) + 1) % Cycle.Length];

    async Task<int> OverflowCount()
        => Int32.Parse((await Driver.GetProperty("OverflowCount", automationId: "BoundGroup"))!);


    [Test]
    public async Task Tapping_The_Avatar_Cycles_Its_Status()
    {
        await OpenPage();

        for (var i = 0; i < 2; i++)
        {
            var before = (await Driver.GetProperty("Status", automationId: "StatusAvatar"))!;
            await Driver.Tap("StatusAvatar");
            await Driver.WaitForProperty("StatusAvatar", "Status", After(before));
            await Driver.WaitForProperty("StatusLabel", "Text", After(before));
        }
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Initials_Come_From_The_Name()
    {
        await OpenPage();

        // Allan Ritchie (no picture), Ada Lovelace (broken picture falls back), Alan Turing (sizes row)
        await Driver.WaitUntilTextExists("AR");
        await Driver.WaitUntilTextExists("AL");
        await Driver.WaitUntilTextExists("AT");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Adding_And_Removing_People_Moves_The_Overflow_Count()
    {
        await OpenPage();
        var start = await OverflowCount();
        await Assert.That(start).IsGreaterThan(0);

        await Driver.Tap("AddPersonButton");
        await Driver.WaitUntil(async () => await OverflowCount() == start + 1, "+N to count the added person");

        await Driver.Tap("RemovePersonButton");
        await Driver.WaitUntil(async () => await OverflowCount() == start, "+N to drop back after the removal");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Tapping_The_Overflow_Chip_Runs_The_Command()
    {
        await OpenPage();
        var count = await OverflowCount();

        // the +N chip is the last avatar in the group
        var chip = (await Driver.QuerySelector("#BoundGroup Avatar")).Last();
        await Driver.Tap(elementId: chip.Id);

        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: "AvatarMessage"))?.StartsWith($"Tapped +{count} ") == true,
            "the overflow command to report the hidden count"
        );
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Real_Tap_Cycles_The_Status()
    {
        if (!HasRealTouch)
            Skip.Test("needs real touch input (Android)");

        await OpenPage();
        var before = (await Driver.GetProperty("Status", automationId: "StatusAvatar"))!;
        await TouchTap("StatusAvatar");
        await Driver.WaitForProperty("StatusAvatar", "Status", After(before), timeoutSeconds: 5);
        await AssertNoAppErrors();
    }
}
