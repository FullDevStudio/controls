using Microsoft.Maui.DevFlow.Driver;

namespace Sample.UITests.Features;

/// <summary>
/// SignaturePad on a ShinyContentPage panel. DevFlow cannot draw ink on the GraphicsView, so signing
/// itself is covered by the Blazor suite; here: opening from the page, the Sign gate, and Cancel.
/// </summary>
public class SignaturePadTests : UITest
{
    const string Panel = "SignaturePanel";

    async Task<ElementInfo?> PadButton(string text)
        => (await Driver.Query(type: "Button")).FirstOrDefault(x => x.Text == text && x.IsVisible);

    async Task OpenPage()
    {
        await Open("signaturepad", "SignaturePadPage");

        // Shell keeps the page; make sure an earlier test did not leave the panel up.
        if (await Driver.GetProperty("IsOpen", automationId: Panel) == "True")
        {
            await Driver.SetProperty("IsOpen", "False", automationId: Panel);
            await Driver.WaitForProperty(Panel, "IsOpen", "False");
        }
    }


    [Test]
    public async Task Capture_Button_Opens_The_Pad_With_Sign_Disabled()
    {
        await OpenPage();

        await Driver.Tap(automationId: "CaptureSignatureButton");

        await Driver.WaitForProperty(Panel, "IsOpen", "True");
        ElementInfo? sign = null;
        await Driver.WaitUntil(async () => (sign = await PadButton("Sign")) != null, "the pad's Sign button");
        // Nothing drawn yet, so there is nothing to sign.
        await Assert.That(sign!.IsEnabled).IsFalse();
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Cancel_Closes_The_Pad_And_Reports_Back()
    {
        await OpenPage();
        await Driver.Tap(automationId: "CaptureSignatureButton");
        await Driver.WaitForProperty(Panel, "IsOpen", "True");

        ElementInfo? cancel = null;
        await Driver.WaitUntil(async () => (cancel = await PadButton("Cancel")) != null, "the pad's Cancel button");
        await Driver.Tap(elementId: cancel!.Id);

        await Driver.WaitForProperty(Panel, "IsOpen", "False");
        await Driver.WaitForProperty("SignatureStatusLabel", "Text", "Signature cancelled");
        // Nothing was captured, so the captured-signature controls stay hidden.
        await Assert.That(await Driver.IsElementVisible("ClearSignatureButton")).IsFalse();
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Pad_Opens_And_Closes_From_Its_Bindable()
    {
        await OpenPage();

        await Driver.SetProperty("IsOpen", "True", automationId: Panel);
        await Driver.WaitForProperty(Panel, "IsVisible", "True");
        await Driver.WaitUntil(async () => await PadButton("Sign") != null, "the pad to show its Sign button");

        // Closing hides the whole control once the panel has animated away.
        await Driver.SetProperty("IsOpen", "False", automationId: Panel);
        await Driver.WaitForProperty(Panel, "IsVisible", "False");
        await AssertNoAppErrors();
    }
}
