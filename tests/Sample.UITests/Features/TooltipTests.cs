namespace Sample.UITests.Features;

/// <summary>Tooltip on <c>tooltip</c>: tap triggers on Buttons, the view-model-bound hint, and opening by IsOpen.</summary>
public class TooltipTests : UITest
{
    async Task OpenPage()
    {
        await Open("tooltip", "TooltipPage");
        foreach (var id in new[] { "BoundTooltip", "TopTooltip", "BottomTooltip", "StyledTooltip", "ApplyTooltip" })
        {
            if (await Driver.GetProperty("IsOpen", automationId: id) == "True")
                await Driver.SetProperty("IsOpen", "False", automationId: id);
            await Driver.WaitForProperty(id, "IsOpen", "False");
        }
    }

    async Task<bool> TextShowing(string text)
        => (await Driver.Query(text: text)).Any(e => e.IsVisible && e.Text == text);

    [Test]
    [Arguments("TooltipTop", "TopTooltip", "Above")]
    [Arguments("TooltipBottom", "BottomTooltip", "Below")]
    public async Task Tap_Trigger_On_A_Button_Toggles_The_Tooltip(string button, string tooltip, string text)
    {
        await OpenPage();

        // Buttons swallow gesture recognizers, so the Tap trigger anchors through Clicked
        await Driver.Tap(automationId: button);
        await Driver.WaitForProperty(tooltip, "IsOpen", "True");
        await Driver.WaitUntil(() => TextShowing(text), $"the '{text}' bubble");

        await Driver.Tap(automationId: button);
        await Driver.WaitForProperty(tooltip, "IsOpen", "False");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Bound_Hint_Follows_The_ViewModel()
    {
        await OpenPage();

        await Driver.Tap(automationId: "TooltipToggleHint");
        await Driver.WaitForProperty("BoundTooltip", "IsOpen", "True");
        await Driver.WaitUntil(() => TextShowing("Why is this disabled?"), "the hint's title");

        await Driver.Tap(automationId: "TooltipToggleHint");
        await Driver.WaitForProperty("BoundTooltip", "IsOpen", "False");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Setting_IsOpen_On_The_Hint_Writes_Back_To_The_ViewModel()
    {
        await OpenPage();

        // IsOpen is two-way: closing the tooltip itself must reach ShowHint, or the next toggle would
        // flip it the wrong way
        await Driver.SetProperty("IsOpen", "True", automationId: "BoundTooltip");
        await Driver.WaitForProperty("BoundTooltip", "IsOpen", "True");
        await Driver.SetProperty("IsOpen", "False", automationId: "BoundTooltip");
        await Driver.WaitForProperty("BoundTooltip", "IsOpen", "False");

        await Driver.Tap(automationId: "TooltipToggleHint");
        await Driver.WaitForProperty("BoundTooltip", "IsOpen", "True");
        await Driver.Tap(automationId: "TooltipToggleHint");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Long_Press_Tooltip_Opens_By_IsOpen_And_Auto_Dismisses()
    {
        await OpenPage();

        // the Apply tooltip is a LongPress trigger with AutoDismissDelay 3000
        await Driver.SetProperty("IsOpen", "True", automationId: "ApplyTooltip");
        await Driver.WaitUntil(() => TextShowing("Applies the change without closing the sheet."), "the Apply bubble");
        await Driver.WaitForProperty("ApplyTooltip", "IsOpen", "False", timeoutSeconds: 8);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Styled_Tooltip_Shows_Title_And_Text()
    {
        await OpenPage();
        await Driver.Tap(automationId: "TooltipStyled");
        await Driver.WaitForProperty("StyledTooltip", "IsOpen", "True");
        await Driver.WaitUntil(() => TextShowing("Heads up"), "the styled bubble's title", 20);

        await Driver.SetProperty("IsOpen", "False", automationId: "StyledTooltip");
        await Driver.WaitForProperty("StyledTooltip", "IsOpen", "False");
        await AssertNoAppErrors();
    }
}
