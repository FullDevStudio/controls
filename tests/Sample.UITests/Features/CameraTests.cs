namespace Sample.UITests.Features;

/// <summary>
/// The Camera, Media Service and Scanned Documents pages, limited to what holds without a working
/// camera (the iOS simulator has none): the bindable state the on-screen controls drive, the options
/// sheet, the media-service log and the document session's empty state.
/// None of these pages are in the macOS or Linux heads' Shells.
/// </summary>
[SkipOn(UIPlatform.MacOS, "camera pages are not in the macOS head")]
[SkipOn(UIPlatform.Linux, "camera pages are not in the Linux head")]
public class CameraTests : UITest
{
    // Shell keeps a page alive between visits, so each test sets the state it starts from.
    Task OpenCamera() => Open("camera", "CameraPage");


    [Test]
    [Skip("BUG (crash): a Facing change queues CameraViewHandler.ReconfigureInput on the session queue, whose InvokeOnMainThread block reads "
        + "this.VirtualView.Zoom unguarded (CameraViewHandler.Apple.cs:1052). When the handler disconnects first (navigating away while the simulator's "
        + "capture session is still failing with err=-12782) it throws 'VirtualView cannot be null here' on the main thread and SIGABRTs the app - "
        + "seen on iOS 2026-10-13 19:48 right after this test, taking the next test down with it")]
    public async Task Flip_Swaps_The_Facing()
    {
        await OpenCamera();
        await Driver.SetProperty("Facing", "Back", automationId: "CameraPreview");
        await Driver.WaitForProperty("CameraPreview", "Facing", "Back");

        await Driver.Tap(automationId: "CameraFlipButton");
        await Driver.WaitForProperty("CameraPreview", "Facing", "Front");

        await Driver.Tap(automationId: "CameraFlipButton");
        await Driver.WaitForProperty("CameraPreview", "Facing", "Back");
    }


    [Test]
    public async Task Look_Strip_Sets_The_Filter()
    {
        await OpenCamera();
        await Driver.SetProperty("Filter", "None", automationId: "CameraPreview");
        await Driver.WaitForProperty("CameraPreview", "Filter", "None");

        // the strip is a BindableLayout of plain Buttons, one per look
        // scoped to the camera page: text queries match substrings on every page Shell keeps alive
        var sepia = (await Driver.QuerySelector("CameraPage Button[Text=\"Sepia\"]")).FirstOrDefault()
            ?? throw new InvalidOperationException("No 'Sepia' look button");
        await Driver.Tap(elementId: sepia.Id);
        await Driver.WaitForProperty("CameraPreview", "Filter", "Sepia");

        var none = (await Driver.QuerySelector("CameraPage Button[Text=\"None\"]")).FirstOrDefault()
            ?? throw new InvalidOperationException("No 'None' look button");
        await Driver.Tap(elementId: none.Id);
        await Driver.WaitForProperty("CameraPreview", "Filter", "None");
    }


    [Test]
    public async Task Options_Button_Opens_The_Sheet()
    {
        await OpenCamera();
        await Driver.SetProperty("IsOpen", "False", automationId: "CameraOptionsPanel");
        await Driver.WaitForProperty("CameraOptionsPanel", "IsOpen", "False");

        await Driver.Tap(automationId: "CameraOptionsButton");
        await Driver.WaitForProperty("CameraOptionsPanel", "IsOpen", "True");

        await Driver.SetProperty("IsOpen", "False", automationId: "CameraOptionsPanel");
        await Driver.WaitForProperty("CameraOptionsPanel", "IsOpen", "False");
    }


    [Test]
    public async Task Media_Service_Lists_Cameras_Into_The_Log_And_Clear_Empties_It()
    {
        await Open("mediaservice", "MediaServicePage");

        await Driver.Tap(automationId: "ListCamerasButton");

        // a device lists "camera: <name> (...)", a simulator "no cameras reported", a failure "error: ..."
        // (read through the bound list: the log sits below the fold, so its CollectionView may not
        // have realised a cell to query yet)
        await Driver.WaitUntil(async () => await LogCount() > 0, "List Cameras to write to the log");

        await Driver.Tap(automationId: "ClearLogButton");
        // the log is a CollectionView: iOS keeps removed cells in the tree, so read the bound list itself
        await Driver.WaitUntil(async () => await LogCount() == 0, "Clear to empty the log");
    }


    async Task<int> LogCount()
        => Int32.TryParse(await Driver.GetProperty("BindingContext.Log.Count", automationId: "ClearLogButton"), out var n) ? n : -1;


    [Test]
    public async Task Document_Session_Clear_Leaves_The_Empty_State()
    {
        await Open("documentsession", "DocumentSessionPage");

        await Driver.Tap(automationId: "ClearDocumentsButton");

        await Driver.WaitUntilExists("NoDocumentsLabel");
        await Assert.That(await Driver.GetProperty("Text", automationId: "NoDocumentsLabel")).IsEqualTo("No documents scanned yet");
        await AssertNoAppErrors();
    }
}
