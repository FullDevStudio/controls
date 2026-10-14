namespace Sample.UITests.Features;

[SkipOn(UIPlatform.MacOS, KnownGaps.VirtualizedGridNoHandler)]
[SkipOn(UIPlatform.Linux, KnownGaps.VirtualizedGridNoHandler)]
public class VirtualizedGridTests : UITest
{
    Task OpenPage() => Open("virtualizedgrid", "VirtualizedGridPage");


    [Test]
    public async Task Mode_Buttons_Swap_The_Visible_Grid()
    {
        await OpenPage();
        await Driver.WaitForProperty("FlatGrid", "IsVisible", "True");
        await Driver.WaitForProperty("GroupedGrid", "IsVisible", "False");

        await Driver.Tap("VGridGroupedButton");
        await Driver.WaitForProperty("GroupedGrid", "IsVisible", "True");
        await Driver.WaitForProperty("FlatGrid", "IsVisible", "False");
        await Driver.WaitForProperty("GroupedGrid", "IsGroupingEnabled", "True");

        await Driver.Tap("VGridLoadMoreButton");
        await Driver.WaitForProperty("LoadMoreGrid", "IsVisible", "True");
        await Driver.WaitForProperty("GroupedGrid", "IsVisible", "False");

        await Driver.Tap("VGridFlatButton");
        await Driver.WaitForProperty("FlatGrid", "IsVisible", "True");
        await Driver.WaitForProperty("LoadMoreGrid", "IsVisible", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Load_More_Mode_Row_Only_Shows_In_Load_More()
    {
        await OpenPage();
        await Driver.WaitForProperty("VGridModeRow", "IsVisible", "False");

        await Driver.Tap("VGridLoadMoreButton");
        await Driver.WaitForProperty("VGridModeRow", "IsVisible", "True");
        await Driver.WaitForProperty("VGridModeLabel", "Text", "Using: Button");
        await Driver.WaitForProperty("LoadMoreGrid", "ShowLoadMoreButton", "True");
    }


    [Test]
    public async Task Threshold_Mode_Turns_Off_The_Load_More_Button()
    {
        await OpenPage();
        await Driver.Tap("VGridLoadMoreButton");

        await Driver.Tap("VGridThresholdMode");
        await Driver.WaitForProperty("VGridModeLabel", "Text", "Using: Threshold (8)");
        await Driver.WaitForProperty("VGridStatusLabel", "Text", "Load More: auto-loads when 8 items from end");
        await Driver.WaitForProperty("LoadMoreGrid", "ShowLoadMoreButton", "False");

        await Driver.Tap("VGridButtonMode");
        await Driver.WaitForProperty("VGridModeLabel", "Text", "Using: Button");
        await Driver.WaitForProperty("VGridStatusLabel", "Text", "Load More: button at end of list");
        await Driver.WaitForProperty("LoadMoreGrid", "ShowLoadMoreButton", "True");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Column_Count_Is_Clamped_Between_One_And_Six()
    {
        await OpenPage();
        await Driver.WaitForProperty("VGridColumnsLabel", "Text", "3");

        for (var i = 0; i < 4; i++)
            await Driver.Tap("VGridPlus");
        await Driver.WaitForProperty("VGridColumnsLabel", "Text", "6");
        await Driver.WaitForProperty("FlatGrid", "ColumnCount", "6");

        for (var i = 0; i < 6; i++)
            await Driver.Tap("VGridMinus");
        await Driver.WaitForProperty("VGridColumnsLabel", "Text", "1");
        await Driver.WaitForProperty("FlatGrid", "ColumnCount", "1");
        // every grid shares the bound count
        await Driver.WaitForProperty("GroupedGrid", "ColumnCount", "1");
        await AssertNoAppErrors();
    }
}
