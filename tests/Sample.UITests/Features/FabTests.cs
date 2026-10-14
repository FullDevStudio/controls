using System.Text.RegularExpressions;

namespace Sample.UITests.Features;

/// <summary>
/// Fab and FabMenu. The menu is opened through its two-way <c>IsOpen</c>; taps go to the controls'
/// own tap recognizers (see <see cref="TapControl"/> for why there is a fallback).
/// </summary>
public class FabTests : UITest
{
    const string Status = "FabStatusLabel";
    const string Menu = "FabMenu";

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

    /// <summary>
    /// Fab and FabMenuItem put their TapGestureRecognizer on an inner Border rather than on the
    /// control, so DevFlow finds nothing to invoke on the AutomationId'd element itself - tap the
    /// Border child that owns the recognizer.
    /// </summary>
    async Task TapControl(string automationId)
    {
        var border = await Driver.FindBySelector($"#{automationId} > Border", timeoutSeconds: 10);
        await Driver.Tap(elementId: border.Id);
    }

    /// <summary>
    /// Opens the menu and waits for the open animation to land (every item fully opaque): FabMenu
    /// drops an IsOpen change that arrives mid-animation (see the BUG test below), and a person
    /// can't tap an item before it has appeared anyway.
    /// </summary>
    async Task OpenMenu()
    {
        await Driver.SetProperty("IsOpen", "True", automationId: Menu);
        foreach (var item in new[] { "FabMenuShare", "FabMenuEdit" })
            await Driver.WaitUntil(async () => await Driver.GetDouble("Opacity", item) >= 1, $"{item} to finish animating in");
    }

    async Task OpenFab()
    {
        await Open("fab", "FabPage");
        // left open - or stuck showing its items while IsOpen reads False (the BUG below): open it
        // properly, then close it once nothing is animating
        if (await Driver.GetProperty("IsOpen", automationId: Menu) == "True"
            || await Driver.GetProperty("IsVisible", automationId: "FabMenuShare") == "True")
        {
            await OpenMenu();
            await Driver.SetProperty("IsOpen", "False", automationId: Menu);
            await Driver.WaitForProperty("FabMenuShare", "IsVisible", "False");
        }
    }


    [Test]
    public async Task Fab_Tap_Runs_Its_Command()
    {
        await OpenFab();
        var before = await Driver.GetProperty("Text", automationId: Status) ?? "";
        var match = Regex.Match(before, @"Add tapped \((\d+)\)");
        var count = match.Success ? Int32.Parse(match.Groups[1].Value) : 0;

        await TapControl("InlineFab");
        await WaitForText(Status, t => t == $"Add tapped ({count + 1})", $"is 'Add tapped ({count + 1})'");

        // The extended FAB is bound to the same command.
        await TapControl("ExtendedFab");
        await WaitForText(Status, t => t == $"Add tapped ({count + 2})", $"is 'Add tapped ({count + 2})'");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Menu_Items_Show_Only_While_Open()
    {
        await OpenFab();
        await Assert.That(await Driver.GetProperty("IsVisible", automationId: "FabMenuShare")).IsEqualTo("False");

        await OpenMenu();
        await Driver.WaitForProperty("FabMenuShare", "IsVisible", "True");
        await Driver.WaitForProperty("FabMenuEdit", "IsVisible", "True");

        await Driver.SetProperty("IsOpen", "False", automationId: Menu);
        await Driver.WaitForProperty("FabMenuShare", "IsVisible", "False");
        await Driver.WaitForProperty("FabMenuEdit", "IsVisible", "False");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Picking_An_Item_Runs_Its_Command_And_Closes_The_Menu()
    {
        await OpenFab();
        await OpenMenu();

        await TapControl("FabMenuEdit");

        await WaitForText(Status, t => t == "Edit tapped", "is 'Edit tapped'");
        // CloseOnItemTap (default) closes the menu, which reports back through the two-way IsOpen.
        await Driver.WaitForProperty(Menu, "IsOpen", "False");
        await Driver.WaitForProperty("FabMenuEdit", "IsVisible", "False");
        await AssertNoAppErrors();
    }


    [Test]
    [Skip("BUG: FabMenu.AnimateToStateAsync returns early while isAnimating, so an IsOpen=false that lands during the open animation is dropped - "
        + "IsOpen reads False but every item stays visible (reproduced on iOS: open, then close/tap an item within ~300ms)")]
    public async Task Closing_During_The_Open_Animation_Still_Closes()
    {
        await OpenFab();
        await Driver.SetProperty("IsOpen", "True", automationId: Menu);
        await Driver.SetProperty("IsOpen", "False", automationId: Menu);

        await Driver.WaitForProperty(Menu, "IsOpen", "False");
        await Driver.WaitForProperty("FabMenuShare", "IsVisible", "False");
        await Driver.WaitForProperty("FabMenuEdit", "IsVisible", "False");
    }
}
