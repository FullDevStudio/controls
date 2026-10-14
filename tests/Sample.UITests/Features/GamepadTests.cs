namespace Sample.UITests.Features;

/// <summary>
/// The on-screen GamepadView. Multi-touch on the pad is not something DevFlow can synthesise, so
/// these drive the page's option pickers/switches — exactly what a user does — and assert the view's
/// bindables follow, plus that the page's game loop is reading the pad.
/// </summary>
[OnlyOn(UIPlatform.iOS, UIPlatform.Android)] // the page is not in the macOS/Linux heads, and the pad only has iOS/Android handlers
public class GamepadTests : UITest
{
    const string Pad = "GamepadView";

    Task OpenGamepad() => Open("gamepad", "GamepadPage");


    [Test]
    public async Task The_Game_Loop_Reads_An_Idle_Pad()
    {
        await OpenGamepad();

        await Driver.WaitUntil(async () =>
            (await Driver.GetProperty("Text", automationId: "GamepadHud"))?.StartsWith("Buttons: -") == true,
            "the HUD to report an idle pad");
        var hud = await Driver.GetProperty("Text", automationId: "GamepadHud");
        await Assert.That(hud).Contains("Left 0.00, 0.00");
    }


    [Test]
    public async Task Preset_Picker_Changes_The_Layout()
    {
        await OpenGamepad();
        try
        {
            await Driver.SetProperty("SelectedIndex", "0", automationId: "GamepadPresetPicker");
            await Driver.WaitForProperty(Pad, "Preset", "Nes");

            await Driver.SetProperty("SelectedIndex", "1", automationId: "GamepadPresetPicker");
            await Driver.WaitForProperty(Pad, "Preset", "Snes");
        }
        finally
        {
            await Driver.SetProperty("SelectedIndex", "2", automationId: "GamepadPresetPicker");
        }
        await Driver.WaitForProperty(Pad, "Preset", "Standard");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Face_Style_Picker_Relabels_The_Pad()
    {
        await OpenGamepad();
        try
        {
            // Index 0 is "Layout default"; the rest follow GamepadFaceStyle (Xbox, PlayStation, Nintendo, …).
            await Driver.SetProperty("SelectedIndex", "3", automationId: "GamepadFacePicker");
            await Driver.WaitForProperty(Pad, "FaceStyle", "Nintendo");
        }
        finally
        {
            await Driver.SetProperty("SelectedIndex", "0", automationId: "GamepadFacePicker");
        }
        await Driver.WaitUntil(async () => String.IsNullOrEmpty(await Driver.GetProperty("FaceStyle", automationId: Pad)), "FaceStyle back to the layout default");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Idle_Fade_Switch_Sets_The_Idle_Opacity()
    {
        await OpenGamepad();
        try
        {
            await Driver.SetProperty("IsToggled", "True", automationId: "GamepadFadeSwitch");
            await Driver.WaitForProperty(Pad, "IdleOpacity", "0.35");
        }
        finally
        {
            await Driver.SetProperty("IsToggled", "False", automationId: "GamepadFadeSwitch");
        }
        await Driver.WaitForProperty(Pad, "IdleOpacity", "1");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Edit_Switch_Puts_The_Pad_In_Edit_Mode()
    {
        await OpenGamepad();
        try
        {
            await Driver.SetProperty("IsToggled", "True", automationId: "GamepadEditSwitch");
            await Driver.WaitForProperty(Pad, "IsEditing", "True");
        }
        finally
        {
            await Driver.SetProperty("IsToggled", "False", automationId: "GamepadEditSwitch");
        }
        await Driver.WaitForProperty(Pad, "IsEditing", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Inline_Switch_Gives_The_Pad_Uniform_Sizing()
    {
        await OpenGamepad();
        try
        {
            await Driver.SetProperty("IsToggled", "True", automationId: "GamepadInlineSwitch");
            await Driver.WaitForProperty(Pad, "Sizing", "Uniform");
        }
        finally
        {
            await Driver.SetProperty("IsToggled", "False", automationId: "GamepadInlineSwitch");
        }
        await Driver.WaitForProperty(Pad, "Sizing", "Anchored");
        await AssertNoAppErrors();
    }
}
