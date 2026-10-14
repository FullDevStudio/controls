namespace Sample.UITests.Features;

/// <summary>
/// The MauiControlFeedbackIntegrator on <c>feedback</c> hooks stock MAUI controls' events and fires
/// IFeedbackService (haptics/sound). There is nothing on screen to read, so the behaviour asserted is
/// that driving every hooked control runs the hook without it throwing, and the control still takes
/// its new value.
/// </summary>
public class FeedbackTests : UITest
{
    Task OpenPage() => Open("feedback", "FeedbackPage");

    [Test]
    public async Task Value_Controls_Change_Through_The_Feedback_Hooks()
    {
        await OpenPage();

        var slider = await Driver.GetDouble("Value", "FeedbackSlider");
        var target = slider == 75 ? "25" : "75";
        await Driver.SetProperty("Value", target, automationId: "FeedbackSlider");
        await Driver.WaitForProperty("FeedbackSlider", "Value", target);

        var stepper = await Driver.GetDouble("Value", "FeedbackStepper") ?? 0;
        var next = (stepper >= 10 ? stepper - 1 : stepper + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        await Driver.SetProperty("Value", next, automationId: "FeedbackStepper");
        await Driver.WaitForProperty("FeedbackStepper", "Value", next);

        await Driver.SetProperty("SelectedIndex", "2", automationId: "FeedbackPicker");
        await Driver.WaitForProperty("FeedbackPicker", "SelectedIndex", "2");

        await AssertNoAppErrors();
    }

    [Test]
    public async Task Toggle_Controls_Flip_Through_The_Feedback_Hooks()
    {
        await OpenPage();

        foreach (var (id, property) in new[] { ("FeedbackSwitch", "IsToggled"), ("FeedbackCheckBox", "IsChecked") })
        {
            var current = await Driver.GetProperty(property, automationId: id);
            var flipped = current == "True" ? "False" : "True";
            await Driver.SetProperty(property, flipped, automationId: id);
            await Driver.WaitForProperty(id, property, flipped);
        }

        await Driver.SetProperty("IsChecked", "True", automationId: "FeedbackRadioB");
        await Driver.WaitForProperty("FeedbackRadioB", "IsChecked", "True");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Button_And_Text_Input_Run_The_Feedback_Hooks()
    {
        await OpenPage();

        await Driver.Tap(automationId: "FeedbackButton");
        await Driver.SetProperty("Text", "typed", automationId: "FeedbackEntry");
        await Driver.WaitForProperty("FeedbackEntry", "Text", "typed");
        await AssertNoAppErrors();
    }
}
