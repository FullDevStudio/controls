namespace Sample.UITests.Features;

/// <summary>Overlay, LoadingOverlay and ShinyContentPage.IsLoading on <c>overlay</c>.</summary>
public class OverlayTests : UITest
{
    const string Custom = "CustomOverlay";
    const string Loading = "LoadingOverlay";

    async Task OpenPage()
    {
        await Open("overlay", "OverlayPage");

        // the timed demos hide themselves; make sure an earlier one has finished before starting
        await Driver.WaitForProperty(Loading, "IsShown", "False", timeoutSeconds: 10);
        if (await Driver.GetProperty("IsShown", automationId: Custom) != "False")
            await Driver.SetProperty("IsShown", "False", automationId: Custom);
        await Driver.WaitForProperty(Custom, "IsShown", "False");
    }

    [Test]
    public async Task Custom_Overlay_Shows_Its_Template_And_Dismisses_From_Inside()
    {
        await OpenPage();

        await Driver.Tap(automationId: "ShowCustomOverlayButton");
        await Driver.WaitForProperty(Custom, "IsShown", "True");
        await Driver.WaitUntilExists("DismissOverlayButton");

        await Driver.Tap(automationId: "DismissOverlayButton");
        await Driver.WaitForProperty(Custom, "IsShown", "False");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Indeterminate_Loading_Shows_A_Message_Then_Hides_Itself()
    {
        await OpenPage();

        await Driver.Tap(automationId: "ShowSpinnerButton");
        await Driver.WaitForProperty(Loading, "IsShown", "True");
        await Driver.WaitForProperty(Loading, "IsIndeterminate", "True");
        await Driver.WaitForProperty(Loading, "Message", "Loading, please wait...");

        await Driver.WaitForProperty(Loading, "IsShown", "False", timeoutSeconds: 10);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Determinate_Loading_Fills_To_100_And_Completes()
    {
        await OpenPage();

        await Driver.Tap(automationId: "ShowProgressButton");
        await Driver.WaitForProperty(Loading, "IsShown", "True");
        await Driver.WaitForProperty(Loading, "IsIndeterminate", "False");

        await Driver.WaitForProperty(Loading, "Message", "Complete!", timeoutSeconds: 10);
        await Assert.That(await Driver.GetDouble("Progress", Loading)).IsEqualTo(100);

        await Driver.WaitForProperty(Loading, "IsShown", "False", timeoutSeconds: 10);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Page_IsLoading_Raises_The_Built_In_Overlay()
    {
        await OpenPage();

        await Driver.Tap(automationId: "ShowPageLoadingButton");
        await Driver.WaitForProperty("OverlayPage", "IsLoading", "True");
        await Driver.WaitForProperty("OverlayPage", "IsLoading", "False", timeoutSeconds: 10);
        await AssertNoAppErrors();
    }
}
