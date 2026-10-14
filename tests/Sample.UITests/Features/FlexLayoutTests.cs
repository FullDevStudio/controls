namespace Sample.UITests.Features;

public class FlexLayoutTests : UITest
{
    // The pickers list each enum in declaration order (Enum.GetValues):
    //   FlexDirection: Row, RowReverse, Column, ColumnReverse
    //   FlexWrap:      NoWrap, Wrap, Reverse
    //   FlexJustify:   Center, Start, End, SpaceBetween, SpaceAround, SpaceEvenly

    /// <summary>Opens the page and restores the playground's defaults - Shell keeps the page for the session.</summary>
    async Task OpenPage()
    {
        await Open("flexlayout", "FlexLayoutPage");
        await Driver.SetProperty("SelectedIndex", "0", automationId: "FlexDirection");
        await Driver.SetProperty("SelectedIndex", "1", automationId: "FlexWrap");
        await Driver.SetProperty("SelectedIndex", "1", automationId: "FlexJustify");
        await Driver.SetProperty("Value", "8", automationId: "FlexSpacing");
        await Driver.WaitForProperty("FlexPlayground", "Direction", "Row");
        await Driver.WaitForProperty("FlexPlayground", "Wrap", "Wrap");
        await Driver.WaitForProperty("FlexPlayground", "JustifyContent", "Start");
    }

    async Task<double> Num(string automationId, string property)
        => await Driver.GetDouble(property, automationId) ?? throw new InvalidOperationException($"{automationId}.{property} is not a number");


    [Test]
    public async Task Order_Minus_One_Is_Laid_Out_First()
    {
        await OpenPage();

        await Driver.WaitUntil(async () => await Num("FlexItemOne", "Width") > 0, "the playground to be laid out");
        await Driver.WaitUntil(async () =>
            await Num("FlexItemOrdered", "X") < await Num("FlexItemOne", "X")
            && Math.Abs(await Num("FlexItemOrdered", "Y") - await Num("FlexItemOne", "Y")) < 1,
            "the Order=-1 child to sit before the first declared child on the same row");
    }


    [Test, SkipOn(UIPlatform.MacOS, KnownGaps.AppKitNoRelayoutOnChange)]
    public async Task Spacing_Slider_Sets_The_Column_Gap()
    {
        await OpenPage();

        await Driver.SetProperty("Value", "20", automationId: "FlexSpacing");
        await Driver.WaitForProperty("FlexPlayground", "ColumnSpacing", "20");
        await Driver.WaitForProperty("FlexPlayground", "RowSpacing", "20");

        // Order=-1 sits first, so "One" follows it on the same row, 20 apart.
        await Driver.WaitUntil(async () =>
            Math.Abs(await Num("FlexItemOne", "X") - (await Num("FlexItemOrdered", "X") + await Num("FlexItemOrdered", "Width")) - 20) < 1,
            "a 20pt gap between the first two children");
        await AssertNoAppErrors();
    }


    [Test, SkipOn(UIPlatform.MacOS, KnownGaps.AppKitNoRelayoutOnChange)]
    public async Task Direction_Column_Stacks_The_Children()
    {
        await OpenPage();

        await Driver.SetProperty("SelectedIndex", "2", automationId: "FlexDirection");
        await Driver.WaitForProperty("FlexPlayground", "Direction", "Column");

        await Driver.WaitUntil(async () =>
            await Num("FlexItemTwo", "Y") > await Num("FlexItemOne", "Y")
            && Math.Abs(await Num("FlexItemTwo", "X") - await Num("FlexItemOne", "X")) < 1,
            "Two directly under One");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Justify_Picker_Reaches_The_Layout()
    {
        await OpenPage();

        await Driver.SetProperty("SelectedIndex", "2", automationId: "FlexJustify");
        await Driver.WaitForProperty("FlexPlayground", "JustifyContent", "End");

        await Driver.SetProperty("SelectedIndex", "0", automationId: "FlexWrap");
        await Driver.WaitForProperty("FlexPlayground", "Wrap", "NoWrap");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Benchmark_Reports_Both_Layouts()
    {
        await OpenPage();

        await Driver.SetProperty("Value", "50", automationId: "FlexBenchCount");
        await Driver.Tap("FlexBenchRun");

        await Driver.WaitUntil(async () =>
        {
            var text = await Driver.GetProperty("Text", automationId: "FlexBenchResult") ?? "";
            return text.Contains("ShinyFlexLayout:") && text.Contains("MAUI FlexLayout:") && text.Contains("faster");
        }, "the benchmark result", timeoutSeconds: 60);

        var result = await Driver.GetProperty("Text", automationId: "FlexBenchResult");
        await Assert.That(result).StartsWith("50 labels");
        await Driver.WaitForProperty("FlexBenchRun", "IsEnabled", "True");
        await AssertNoAppErrors();
    }
}
