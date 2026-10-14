namespace Sample.UITests.Features;

/// <summary>
/// ChipGroup: single selection (and AllowDeselect), multiple selection written into the bound
/// collection, MaxSelectionCount, SelectionMode.None reporting taps, DisplayMemberPath objects,
/// AllowRemove with a cancelling ChipRemoving, and read-only. Chips are tapped through their own
/// tap gesture (a Command, so DevFlow can invoke it); the ✕ is the ContentView in the chip's row.
/// Shell caches the page, so each test reads the state it arrives to rather than assuming one.
/// </summary>
public class ChipGroupTests : UITest
{
    const string Route = "chipgroup";
    const string PageType = "ChipGroupPage";

    static string Chips(string groupId) => $"#{groupId} ChipView";
    static string RemoveButtons(string groupId) => $"#{groupId} ChipView Grid > HorizontalStackLayout > ContentView";

    Task TapChip(string groupId, int index) => Driver.TapCss(Chips(groupId), index);

    async Task<bool> IsSelected(string groupId, int index)
        => await Driver.GetCss(Chips(groupId), "IsSelected", index) == "True";

    async Task<int> Count(string automationId, string property)
        => Int32.Parse(await Driver.GetProperty(property, automationId: automationId) ?? "0");

    [Test]
    public async Task Single_Selection_Picks_One_Chip()
    {
        await Open(Route, PageType);
        string[] ranges = ["Day", "Week", "Month", "Quarter", "Year"];
        var current = await Driver.GetProperty("SelectedItem", automationId: "RangeChips");
        var index = current == "Month" ? 3 : 2;

        await TapChip("RangeChips", index);

        await Driver.WaitForValue("RangeChips", "SelectedItem", ranges[index]);
        await Driver.WaitForValue("ChipStatus", "Text", $"Range: {ranges[index]}");
        await Driver.WaitUntil(async () => await IsSelected("RangeChips", index), "the tapped chip to show as selected");
        for (var i = 0; i < ranges.Length; i++)
            if (i != index)
                await Assert.That(await IsSelected("RangeChips", i)).IsFalse();

        await AssertNoAppErrors();
    }

    [Test]
    public async Task Single_Selection_Ignores_Retapping_The_Answer()
    {
        await Open(Route, PageType);
        await TapChip("RangeChips", 1);
        await Driver.WaitForValue("RangeChips", "SelectedItem", "Week");

        await TapChip("RangeChips", 1);
        await Task.Delay(400);

        await Driver.WaitForValue("RangeChips", "SelectedItem", "Week");
        await Assert.That(await IsSelected("RangeChips", 1)).IsTrue();
    }

    [Test]
    public async Task AllowDeselect_Clears_On_Second_Tap()
    {
        await Open(Route, PageType);
        if (await Driver.GetProperty("SelectedItem", automationId: "DeselectChips") != "Day")
            await TapChip("DeselectChips", 0);
        await Driver.WaitForValue("DeselectChips", "SelectedItem", "Day");

        await TapChip("DeselectChips", 0);

        await Driver.WaitForValue("DeselectChips", "SelectedItem", null);
        await Driver.WaitUntil(async () => !await IsSelected("DeselectChips", 0), "the chip to show as unselected");
    }

    [Test]
    public async Task Multiple_Selection_Toggles_Into_The_Bound_Collection()
    {
        await Open(Route, PageType);
        var wasSelected = await IsSelected("FilterChips", 2);
        var before = await Count("FilterChips", "SelectedItems.Count");

        await TapChip("FilterChips", 2);

        await Driver.WaitForValue("FilterChips", "SelectedItems.Count", (before + (wasSelected ? -1 : 1)).ToString());
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: "FilterSummary") ?? "").Contains("Blocked") != wasSelected,
            "the filter summary to follow the Blocked chip"
        );

        await TapChip("FilterChips", 2);
        await Driver.WaitForValue("FilterChips", "SelectedItems.Count", before.ToString());
        await Assert.That(await IsSelected("FilterChips", 2)).IsEqualTo(wasSelected);
    }

    [Test]
    public async Task MaxSelectionCount_Refuses_A_Fourth_Pick()
    {
        await Open(Route, PageType);
        for (var i = 0; i < 5; i++)
            if (await IsSelected("ToppingChips", i))
                await TapChip("ToppingChips", i);
        await Driver.WaitForValue("ToppingChips", "SelectedItems.Count", "0");

        for (var i = 0; i < 4; i++)
            await TapChip("ToppingChips", i);

        await Driver.WaitForValue("ToppingChips", "SelectedItems.Count", "3");
        await Task.Delay(400);
        await Assert.That(await IsSelected("ToppingChips", 3)).IsFalse();
        await Assert.That(await Count("ToppingChips", "SelectedItems.Count")).IsEqualTo(3);
        await Assert.That(await Count("ChipGroupPage", "BindingContext.SelectedToppings.Count")).IsEqualTo(3);
    }

    [Test]
    public async Task Action_Chips_Report_Taps_Without_Selecting()
    {
        await Open(Route, PageType);

        await TapChip("ActionChips", 2);
        await Driver.WaitForValue("ChipStatus", "Text", "Action: Archive");

        await TapChip("ActionChips", 0);
        await Driver.WaitForValue("ChipStatus", "Text", "Action: Share");

        await Assert.That(await IsSelected("ActionChips", 0)).IsFalse();
        await Assert.That(await IsSelected("ActionChips", 2)).IsFalse();
        await Driver.WaitForValue("ActionChips", "SelectedItem", null);
    }

    [Test]
    public async Task Object_Items_Select_The_Object()
    {
        await Open(Route, PageType);
        var current = await Driver.GetProperty("SelectedItem", automationId: "TeamChips") ?? "";
        var (index, name, members) = current.Contains("Engineering") ? (2, "Sales", 11) : (1, "Engineering", 24);

        await TapChip("TeamChips", index);

        await Driver.WaitForValue("ChipStatus", "Text", $"Team: {name} ({members})");
        await Driver.WaitForValue("ChipGroupPage", "BindingContext.Team.Name", name);
        await Driver.WaitForValue("ChipGroupPage", "BindingContext.Team.Members", members.ToString());
    }

    [Test]
    public async Task Remove_Takes_The_Item_Out_But_Never_The_Last()
    {
        await Open(Route, PageType);
        await Driver.Tap(automationId: "ResetRecipients");
        await Driver.WaitForValue("RecipientChips", "ItemsSource.Count", "3");
        await Driver.Css(Chips("RecipientChips"), minCount: 3);

        await Driver.TapCss(RemoveButtons("RecipientChips"), 0);
        await Driver.WaitForValue("RecipientChips", "ItemsSource.Count", "2");
        await Driver.WaitUntil(async () => (await Driver.CssNow(Chips("RecipientChips"))).Count == 2, "two chips to remain");

        await Driver.TapCss(RemoveButtons("RecipientChips"), 0);
        await Driver.WaitForValue("RecipientChips", "ItemsSource.Count", "1");

        // ChipRemoving is cancelled for the last one
        await Driver.TapCss(RemoveButtons("RecipientChips"), 0);
        await Driver.WaitForValue("ChipStatus", "Text", "The last recipient cannot be removed");
        await Driver.WaitForValue("RecipientChips", "ItemsSource.Count", "1");

        await Driver.Tap(automationId: "ResetRecipients");
        await Driver.WaitForValue("RecipientChips", "ItemsSource.Count", "3");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task ReadOnly_Blocks_Selecting_And_Removing()
    {
        await Open(Route, PageType);
        var wasSelected = await IsSelected("FrozenChips", 0);

        await TapChip("FrozenChips", 0);
        await Driver.TapCss(RemoveButtons("FrozenChips"), 1);
        await Task.Delay(400);

        await Assert.That(await IsSelected("FrozenChips", 0)).IsEqualTo(wasSelected);
        await Driver.WaitForValue("FrozenChips", "ItemsSource.Count", "2");
        await Assert.That((await Driver.CssNow(Chips("FrozenChips"))).Count).IsEqualTo(2);
    }
}
