namespace Sample.UITests.Features;

public class YogaLayoutTests : UITest
{
    /// <summary>Opens the page and restores the playground's defaults - Shell keeps the page for the session.</summary>
    async Task OpenPage()
    {
        await Open("yogalayout", "YogaLayoutPage");
        await Driver.SetProperty("SelectedIndex", "0", automationId: "YogaDirection");   // Column
        await Driver.SetProperty("SelectedIndex", "0", automationId: "YogaJustify");     // FlexStart
        await Driver.SetProperty("SelectedIndex", "4", automationId: "YogaAlignItems");  // Stretch
        await Driver.SetProperty("SelectedIndex", "0", automationId: "YogaWrap");        // NoWrap
        await Driver.SetProperty("Value", "8", automationId: "YogaGap");
        await Driver.WaitForProperty("YogaPlayground", "FlexDirection", "Column");
        await Driver.WaitForProperty("YogaPlayground", "JustifyContent", "FlexStart");
        await Driver.WaitForProperty("YogaPlayground", "AlignItems", "Stretch");
    }

    async Task<double> Num(string automationId, string property)
        => await Driver.GetDouble(property, automationId) ?? throw new InvalidOperationException($"{automationId}.{property} is not a number");


    [Test, SkipOn(UIPlatform.MacOS, KnownGaps.AppKitNoRelayoutOnChange)]
    public async Task Default_Column_Then_Row()
    {
        await OpenPage();

        await Driver.WaitUntil(async () =>
            await Num("YogaBoxB", "Y") >= await Num("YogaBoxA", "Y") + 60
            && Math.Abs(await Num("YogaBoxB", "X") - await Num("YogaBoxA", "X")) < 1,
            "B stacked under the 60pt A in the default column");

        // YogaFlexDirection: Column, ColumnReverse, Row, RowReverse
        await Driver.SetProperty("SelectedIndex", "2", automationId: "YogaDirection");
        await Driver.WaitForProperty("YogaPlayground", "FlexDirection", "Row");

        await Driver.WaitUntil(async () =>
            await Num("YogaBoxB", "X") >= await Num("YogaBoxA", "X") + 60
            && Math.Abs(await Num("YogaBoxB", "Y") - await Num("YogaBoxA", "Y")) < 1,
            "B beside A in a row");
        await AssertNoAppErrors();
    }


    [Test, SkipOn(UIPlatform.MacOS, KnownGaps.AppKitNoRelayoutOnChange)]
    public async Task Gap_Slider_Sets_The_Space_Between_Children()
    {
        await OpenPage();

        await Driver.SetProperty("Value", "20", automationId: "YogaGap");
        await Driver.WaitForProperty("YogaPlayground", "Gap", "20");

        await Driver.WaitUntil(async () =>
            Math.Abs(await Num("YogaBoxB", "Y") - (await Num("YogaBoxA", "Y") + await Num("YogaBoxA", "Height")) - 20) < 1,
            "a 20pt gap between A and B");
        await AssertNoAppErrors();
    }


    [Test, SkipOn(UIPlatform.MacOS, KnownGaps.AppKitNoRelayoutOnChange)]
    public async Task Justify_FlexEnd_Pushes_Children_To_The_Bottom()
    {
        await OpenPage();

        // YogaJustify: FlexStart, Center, FlexEnd, …
        await Driver.SetProperty("SelectedIndex", "2", automationId: "YogaJustify");
        await Driver.WaitForProperty("YogaPlayground", "JustifyContent", "FlexEnd");

        // 240pt tall with 8pt padding; children are positioned relative to the playground.
        await Driver.WaitUntil(async () =>
            Math.Abs(await Num("YogaBoxD", "Y") + await Num("YogaBoxD", "Height") - (await Num("YogaPlayground", "Height") - 8)) < 1,
            "the last child flush with the bottom padding");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Auto_Start_Margin_Pushes_Done_To_The_End()
    {
        await OpenPage();

        await Driver.WaitUntil(async () =>
            await Num("YogaDone", "Width") > 0
            && Math.Abs(await Num("YogaDone", "X") + await Num("YogaDone", "Width") - (await Num("YogaDoneBar", "Width") - 12)) < 1.5,
            "Done flush with the bar's 12pt end padding");
    }


    [Test]
    public async Task Absolute_Badge_And_Aspect_Ratio()
    {
        await OpenPage();

        await Driver.WaitUntil(async () =>
            Math.Abs(await Num("YogaBadge", "Y") - 8) < 1
            && Math.Abs(await Num("YogaBadge", "X") + await Num("YogaBadge", "Width") - (await Num("YogaCard", "Width") - 8)) < 1.5,
            "the NEW badge inset 8pt from the card's top-right corner");

        await Driver.WaitUntil(async () =>
        {
            var h = await Num("YogaCardImage", "Height");
            return h > 0 && Math.Abs(await Num("YogaCardImage", "Width") / h - 1.5) < 0.05;
        }, "the 1.5 aspect ratio image");
    }
}
