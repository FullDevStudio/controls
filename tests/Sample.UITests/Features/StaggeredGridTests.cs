namespace Sample.UITests.Features;

[SkipOn(UIPlatform.MacOS, KnownGaps.StaggeredGridNoHandler)]
[SkipOn(UIPlatform.Linux, KnownGaps.StaggeredGridNoHandler)]
public class StaggeredGridTests : UITest
{
    /// <summary>Opens the page back at its default two columns - Shell keeps the view-model for the session.</summary>
    async Task OpenPage()
    {
        await Open("staggeredgrid", "StaggeredGridPage");
        for (var i = 0; i < 5 && await Driver.GetProperty("Text", automationId: "StaggeredColumnsLabel") is { } t && t != "2"; i++)
            await Driver.Tap(Int32.Parse(t) > 2 ? "StaggeredMinus" : "StaggeredPlus");
        await Driver.WaitForProperty("StaggeredColumnsLabel", "Text", "2");
    }


    [Test]
    public async Task Plus_And_Minus_Change_The_Column_Count()
    {
        await OpenPage();
        await Driver.WaitForProperty("StaggeredColumnsLabel", "Text", "2");
        await Driver.WaitForProperty("PinGrid", "ColumnCount", "2");

        await Driver.Tap("StaggeredPlus");
        await Driver.WaitForProperty("StaggeredColumnsLabel", "Text", "3");
        await Driver.WaitForProperty("PinGrid", "ColumnCount", "3");

        await Driver.Tap("StaggeredMinus");
        await Driver.Tap("StaggeredMinus");
        await Driver.WaitForProperty("StaggeredColumnsLabel", "Text", "1");
        await Driver.WaitForProperty("PinGrid", "ColumnCount", "1");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Column_Count_Is_Clamped_Between_One_And_Five()
    {
        await OpenPage();

        for (var i = 0; i < 5; i++)
            await Driver.Tap("StaggeredPlus");
        await Driver.WaitForProperty("StaggeredColumnsLabel", "Text", "5");

        for (var i = 0; i < 6; i++)
            await Driver.Tap("StaggeredMinus");
        await Driver.WaitForProperty("StaggeredColumnsLabel", "Text", "1");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Shuffle_And_Relayout_Do_Not_Throw()
    {
        await OpenPage();

        // A shuffle clears and refills the ObservableCollection — the native adapter sees a Reset then 40 Adds.
        await Driver.Tap("StaggeredShuffle");
        await Driver.Tap("StaggeredPlus");
        await Driver.Tap("StaggeredShuffle");
        await Driver.WaitForProperty("PinGrid", "ColumnCount", "3");
        await Task.Delay(500);

        await Assert.That(await Driver.IsAlive()).IsTrue();
        await AssertNoAppErrors();
    }
}
