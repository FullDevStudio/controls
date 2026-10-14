namespace Sample.UITests.Features;

/// <summary>
/// ButtonGroup: action segments keep their own commands, Single mode is a picker with a two-way
/// SelectedIndex, Multiple mode toggles. Segments are ShinyButtons, so DevFlow can tap them.
/// </summary>
public class ButtonGroupTests : UITest
{
    async Task<string> WaitForText(string automationId, Func<string, bool> condition, string description)
    {
        string? text = null;
        await Driver.WaitUntil(async () =>
        {
            text = await Driver.GetProperty("Text", automationId: automationId);
            return text != null && condition(text);
        }, $"{automationId}.Text {description} (last: '{text}')");
        return text!;
    }

    Task OpenGroups() => Open("buttongroup", "ButtonGroupPage");


    [Test]
    public async Task Action_Segments_Run_Their_Own_Commands()
    {
        await OpenGroups();

        await Driver.Tap(automationId: "ReportButton");
        await WaitForText("ButtonGroupStatus", t => t.StartsWith("Report at "), "starts with 'Report at '");

        await Driver.Tap(automationId: "ArchiveButton");
        await WaitForText("ButtonGroupStatus", t => t.StartsWith("Archive at "), "starts with 'Archive at '");

        // A plain action group never selects.
        await Assert.That(await Driver.GetProperty("SelectedIndex", automationId: "ActionGroup")).IsEqualTo("-1");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Tapping_A_Segment_Selects_It_Two_Way()
    {
        await OpenGroups();

        await Driver.Tap(automationId: "RangeDay");
        await Driver.WaitForProperty("RangeGroup", "SelectedIndex", "0");
        await WaitForText("RangeSummary", t => t == "Showing: Day", "is 'Showing: Day'");

        await Driver.Tap(automationId: "RangeMonth");
        await Driver.WaitForProperty("RangeGroup", "SelectedIndex", "2");
        await WaitForText("RangeSummary", t => t == "Showing: Month", "is 'Showing: Month'");

        // The selected segment takes the group's selected appearance; the others do not.
        var selected = await Driver.GetProperty("SelectedAppearance", automationId: "RangeGroup");
        await Driver.WaitForProperty("RangeMonth", "Appearance", selected!);
        await Assert.That(await Driver.GetProperty("Appearance", automationId: "RangeDay")).IsNotEqualTo(selected);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Setting_SelectedIndex_Moves_The_Selection()
    {
        await OpenGroups();

        await Driver.SetProperty("SelectedIndex", "1", automationId: "RangeGroup");
        await WaitForText("RangeSummary", t => t == "Showing: Week", "is 'Showing: Week'");

        var selected = await Driver.GetProperty("SelectedAppearance", automationId: "RangeGroup");
        await Driver.WaitForProperty("RangeWeek", "Appearance", selected!);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Single_Selection_Cannot_Be_Emptied_By_Retapping()
    {
        await OpenGroups();
        await Driver.Tap(automationId: "RangeWeek");
        await Driver.WaitForProperty("RangeGroup", "SelectedIndex", "1");

        await Driver.Tap(automationId: "RangeWeek");
        await Task.Delay(400);

        await Assert.That(await Driver.GetProperty("SelectedIndex", automationId: "RangeGroup")).IsEqualTo("1");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Multiple_Selection_Toggles_A_Segment()
    {
        await OpenGroups();
        var before = await Driver.GetProperty("Text", automationId: "FormatSummary") ?? "";
        var hadUnderline = before.Contains("Underline");

        await Driver.Tap(automationId: "FormatUnderline");
        await WaitForText("FormatSummary", t => t.Contains("Underline") != hadUnderline, "to toggle Underline");

        // A second tap toggles it straight back.
        await Driver.Tap(automationId: "FormatUnderline");
        await WaitForText("FormatSummary", t => t == before, $"to return to '{before}'");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Multiple_Selection_Reports_In_Index_Order()
    {
        await OpenGroups();

        // Clear whatever an earlier test left selected, through the UI.
        foreach (var (id, name) in new[] { ("FormatBold", "Bold"), ("FormatItalic", "Italic"), ("FormatUnderline", "Underline") })
        {
            var summary = await Driver.GetProperty("Text", automationId: "FormatSummary") ?? "";
            if (summary.Contains(name))
            {
                await Driver.Tap(automationId: id);
                await WaitForText("FormatSummary", t => !t.Contains(name), $"to drop {name}");
            }
        }
        await WaitForText("FormatSummary", t => t == "No formatting", "is 'No formatting'");

        await Driver.Tap(automationId: "FormatUnderline");
        await WaitForText("FormatSummary", t => t == "Underline", "is 'Underline'");
        await Driver.Tap(automationId: "FormatBold");
        await WaitForText("FormatSummary", t => t == "Bold, Underline", "is 'Bold, Underline'");

        await Driver.Tap(automationId: "FormatBold");
        await Driver.Tap(automationId: "FormatUnderline");
        await WaitForText("FormatSummary", t => t == "No formatting", "is 'No formatting'");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Text_Segment_Group_Adjusts_The_Amount()
    {
        await OpenGroups();
        var start = Int32.Parse((await WaitForText("AmountSummary", t => t.StartsWith("Amount: "), "starts with 'Amount: '"))["Amount: ".Length..]);

        await Driver.Tap(automationId: "AmountIncrease");
        await WaitForText("AmountSummary", t => t == $"Amount: {start + 1}", $"is 'Amount: {start + 1}'");

        await Driver.Tap(automationId: "AmountDecrease");
        await WaitForText("AmountSummary", t => t == $"Amount: {start}", $"is 'Amount: {start}'");
        await AssertNoAppErrors();
    }
}
