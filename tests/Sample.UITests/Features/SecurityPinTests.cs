namespace Sample.UITests.Features;

/// <summary>
/// SecurityPin. Typing goes into a hidden Entry DevFlow cannot type into reliably, so these set the
/// bindable <c>Value</c> — which the control pushes through that same Entry, so Completed still fires —
/// and assert the page's bound labels and status message.
/// </summary>
public class SecurityPinTests : UITest
{
    async Task OpenPin()
    {
        // sibling ShellContent switches don't present on AppKit (KnownGaps.AppKitSiblingShellContentSwitch)
        if (Platform == UIPlatform.MacOS)
            await Open("home");
        await Open("securitypin", "SecurityPinPage");
        // The page is cached by Shell, so start every test from the page's own Clear All.
        await Driver.Tap(automationId: "ClearPinsButton");
        await WaitForText("PinStatusLabel", t => t == "Cleared", "is 'Cleared'");
    }

    async Task WaitForText(string automationId, Func<string, bool> condition, string description)
    {
        string? text = null;
        await Driver.WaitUntil(async () =>
        {
            text = await Driver.GetProperty("Text", automationId: automationId);
            return text != null && condition(text);
        }, $"{automationId}.Text {description} (last: '{text}')");
    }


    [Test]
    public async Task Partial_Pin_Binds_But_Does_Not_Complete()
    {
        await OpenPin();

        await Driver.SetProperty("Value", "12", automationId: "HiddenPin");

        await WaitForText("HiddenPinValueLabel", t => t == "Current value: 12", "is 'Current value: 12'");
        await Task.Delay(400);
        await Assert.That(await Driver.GetProperty("Text", automationId: "PinStatusLabel")).IsEqualTo("Cleared");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Full_Masked_Pin_Raises_Completed_Without_Revealing_It()
    {
        await OpenPin();

        await Driver.SetProperty("Value", "1234", automationId: "HiddenPin");

        await WaitForText("HiddenPinValueLabel", t => t == "Current value: 1234", "is 'Current value: 1234'");
        // The page's Completed handler reports the length, masked.
        await WaitForText("PinStatusLabel", t => t == "Hidden PIN entered: ****", "is 'Hidden PIN entered: ****'");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Visible_Pin_Completes_With_Its_Digits()
    {
        await OpenPin();

        await Driver.SetProperty("Value", "135790", automationId: "VisiblePin");

        await WaitForText("PinStatusLabel", t => t == "Visible PIN entered: 135790", "is 'Visible PIN entered: 135790'");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Value_Longer_Than_Length_Is_Truncated()
    {
        await OpenPin();

        await Driver.SetProperty("Value", "12345678", automationId: "VisiblePin");

        await Driver.WaitForProperty("VisiblePin", "Value", "123456");
        await WaitForText("PinStatusLabel", t => t == "Visible PIN entered: 123456", "is 'Visible PIN entered: 123456'");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Clear_All_Empties_Every_Pin()
    {
        await OpenPin();
        await Driver.SetProperty("Value", "1234", automationId: "HiddenPin");
        await Driver.SetProperty("Value", "123", automationId: "VisiblePin");
        await WaitForText("HiddenPinValueLabel", t => t == "Current value: 1234", "is 'Current value: 1234'");

        await Driver.Tap(automationId: "ClearPinsButton");

        await WaitForText("PinStatusLabel", t => t == "Cleared", "is 'Cleared'");
        await WaitForText("HiddenPinValueLabel", t => t.Trim() == "Current value:", "is 'Current value: '");
        await Driver.WaitUntil(async () => String.IsNullOrEmpty(await Driver.GetProperty("Value", automationId: "VisiblePin")), "VisiblePin.Value to be empty");
        await AssertNoAppErrors();
    }
}
