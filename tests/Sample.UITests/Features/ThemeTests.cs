using Microsoft.Maui.DevFlow.Driver;

namespace Sample.UITests.Features;

/// <summary>
/// App-wide theming: light/dark (from the OS side through the DevFlow agent, and from the app's own
/// flyout switch) flips every AppThemeBinding, and the flyout's pack buttons swap the Shiny theme pack.
/// Replaces the old screenshot-only ThemeTests/ColorReadabilityTests: the assertion here is the state.
/// </summary>
public class ThemeTests : UITest
{
    /// <summary>The home page root is an AppThemeBinding (Light #F5F3FF / Dark #0F0D1A).</summary>
    Task<string?> HomeBackground() => Driver.GetProperty("BackgroundColor", automationId: "HomeRoot");

    async Task<DevFlowTheme> EffectiveTheme()
    {
        var result = await Driver.Client.GetThemeAsync();
        return result?.EffectiveTheme ?? result?.Theme ?? DevFlowTheme.System;
    }

    async Task RestoreTheme(DevFlowTheme? original)
        => await Driver.Client.SetThemeAsync(original ?? DevFlowTheme.System);


    [Test]
    public async Task Dark_Theme_Flips_AppThemeBindings()
    {
        var original = (await Driver.Client.GetThemeAsync())?.UserAppTheme;
        try
        {
            await Open("home", "HomePage");

            await Driver.Client.SetThemeAsync(DevFlowTheme.Light);
            await Driver.WaitUntil(async () => await EffectiveTheme() == DevFlowTheme.Light, "the app to report Light");
            string? light = null;
            await Driver.WaitUntil(async () => (light = await HomeBackground()) != null, "the light background");

            await Driver.Client.SetThemeAsync(DevFlowTheme.Dark);
            await Driver.WaitUntil(async () => await EffectiveTheme() == DevFlowTheme.Dark, "the app to report Dark");
            await Driver.WaitUntil(async () => await HomeBackground() is { } dark && dark != light, "the home background to follow Dark");

            await Driver.Client.SetThemeAsync(DevFlowTheme.Light);
            await Driver.WaitUntil(async () => await HomeBackground() == light, "the home background to come back for Light");
            await AssertNoAppErrors();
        }
        finally
        {
            await RestoreTheme(original);
        }
    }


    [Test]
    public async Task Theme_Change_Reaches_A_Page_Opened_Afterwards()
    {
        var original = (await Driver.Client.GetThemeAsync())?.UserAppTheme;
        try
        {
            await Open("home", "HomePage");
            await Driver.Client.SetThemeAsync(DevFlowTheme.Light);
            await Driver.WaitUntil(async () => await EffectiveTheme() == DevFlowTheme.Light, "the app to report Light");
            string? light = null;
            await Driver.WaitUntil(async () => (light = await HomeBackground()) != null, "the light background");

            // switch while another page is on screen, then come back
            await Open("mediapicker", "MediaPickerPage");
            await Driver.Client.SetThemeAsync(DevFlowTheme.Dark);
            await Driver.WaitUntil(async () => await EffectiveTheme() == DevFlowTheme.Dark, "the app to report Dark");

            await Open("home", "HomePage");
            await Driver.WaitUntil(async () => await HomeBackground() is { } dark && dark != light, "the returning page to be dark");
            await AssertNoAppErrors();
        }
        finally
        {
            await RestoreTheme(original);
        }
    }


    [Test]
    [SkipOn(UIPlatform.MacOS, "the macOS head's Shell has no dark-mode switch")]
    [SkipOn(UIPlatform.Linux, "the Linux head's Shell has no dark-mode switch")]
    public async Task Flyout_Dark_Switch_Sets_The_App_Theme()
    {
        var original = (await Driver.Client.GetThemeAsync())?.UserAppTheme;
        try
        {
            await Open("home", "HomePage");
            await Driver.SetProperty("IsToggled", "False", automationId: "DarkModeSwitch");

            // 30s: late in a full run Shell holds dozens of live pages, and a theme flip restyles all of
            // them on the UI thread - the agent can't answer until that is done
            await Driver.SetProperty("IsToggled", "True", automationId: "DarkModeSwitch");
            await Driver.WaitUntil(async () => (await Driver.Client.GetThemeAsync())?.UserAppTheme == DevFlowTheme.Dark, "the switch to set UserAppTheme Dark", 30);

            await Driver.SetProperty("IsToggled", "False", automationId: "DarkModeSwitch");
            await Driver.WaitUntil(async () => (await Driver.Client.GetThemeAsync())?.UserAppTheme == DevFlowTheme.Light, "the switch to set UserAppTheme Light", 30);
            await AssertNoAppErrors();
        }
        finally
        {
            // the switch also persists a preference; leave it off, then hand the OS theme back
            try { await Driver.SetProperty("IsToggled", "False", automationId: "DarkModeSwitch"); } catch { }
            await RestoreTheme(original);
        }
    }


    [Test]
    [SkipOn(UIPlatform.MacOS, "the macOS head's Shell has no theme pack buttons")]
    [SkipOn(UIPlatform.Linux, "the Linux head's Shell has no theme pack buttons")]
    public async Task Flyout_Pack_Buttons_Select_A_Theme_Pack()
    {
        await Open("home", "HomePage");
        try
        {
            await Driver.Tap(automationId: "theme-ocean");

            // the selected pack's button is marked: full opacity, the rest dimmed
            await Driver.WaitUntil(async () => await Driver.GetDouble("Opacity", "theme-ocean") == 1.0, "Ocean to be marked selected");
            await Driver.WaitUntil(async () => await Driver.GetDouble("Opacity", "theme-basic") < 1.0, "Basic to be dimmed");

            await Driver.Tap(automationId: "theme-material");
            await Driver.WaitUntil(async () => await Driver.GetDouble("Opacity", "theme-material") == 1.0, "Material to be marked selected");
            await Driver.WaitUntil(async () => await Driver.GetDouble("Opacity", "theme-ocean") < 1.0, "Ocean to be dimmed again");
            await AssertNoAppErrors();
        }
        finally
        {
            // the pack choice is persisted, so put Basic back
            await Driver.Tap(automationId: "theme-basic");
            await Driver.WaitUntil(async () => await Driver.GetDouble("Opacity", "theme-basic") == 1.0, "Basic to be restored");
        }
    }
}
