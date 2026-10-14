namespace Sample.UITests.Features;

public class TreeViewTests : UITest
{
    /// <summary>Opens the page collapsed and in single-select mode - Shell keeps the page (and its expansion) for the session.</summary>
    async Task OpenPage()
    {
        await Open("treeview", "TreeViewPage");
        await Driver.SetProperty("IsToggled", "False", automationId: "TreeMultiSelectSwitch");
        await Driver.WaitForProperty("FileTree", "SelectionMode", "Single");
        await Driver.Tap("TreeCollapseAllButton");
        await Driver.WaitUntil(async () => !await RowExists("readme.txt"), "the tree to start collapsed");
    }

    /// <summary>A row label on this page - other pages Shell keeps alive could show the same file names.</summary>
    async Task<bool> RowExists(string name)
        => (await Driver.QuerySelector($"TreeViewPage Label[Text={MauiDevFlowDriver.Quote(name)}]")).Count > 0;

    Task WaitForRow(string name, int timeoutSeconds)
        => Driver.WaitUntil(() => RowExists(name), $"the '{name}' row", timeoutSeconds);


    [Test]
    public async Task Expand_All_Materializes_Every_Branch_Including_Lazy_Ones()
    {
        await OpenPage();
        await WaitForRow("Documents", 10);
        await Assert.That(await RowExists("Program.cs")).IsFalse();

        await Driver.Tap("TreeExpandAllButton");

        // Collapsed rows are not in the tree at all, so their text appearing proves the expansion.
        await WaitForRow("Program.cs", 10);
        await WaitForRow("photo.png", 10);
        // Cloud loads after 900ms, and its "shared" folder after another 900ms.
        await WaitForRow("remote-backup.zip", 15);
        await WaitForRow("team-notes.md", 15);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Collapse_All_Removes_Child_Rows()
    {
        await OpenPage();
        await Driver.Tap("TreeExpandAllButton");
        await WaitForRow("Program.cs", 10);

        await Driver.Tap("TreeCollapseAllButton");
        await Driver.WaitUntil(async () => !await RowExists("Program.cs") && !await RowExists("readme.txt"), "child rows to be removed");
        await Assert.That(await RowExists("Documents")).IsTrue();
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Multi_Select_Switch_Changes_Mode_And_Shows_Bulk_Buttons()
    {
        await OpenPage();
        await Driver.WaitForProperty("FileTree", "SelectionMode", "Single");
        await Driver.WaitForProperty("TreeSelectAllButton", "IsVisible", "False");

        await Driver.SetProperty("IsToggled", "True", automationId: "TreeMultiSelectSwitch");
        await Driver.WaitForProperty("FileTree", "SelectionMode", "Multiple");
        await Driver.WaitForProperty("TreeStatusLabel", "Text", "Selection mode: Multiple");
        await Driver.WaitForProperty("TreeSelectAllButton", "IsVisible", "True");
        await Driver.WaitForProperty("TreeDeselectAllButton", "IsVisible", "True");

        await Driver.SetProperty("IsToggled", "False", automationId: "TreeMultiSelectSwitch");
        await Driver.WaitForProperty("FileTree", "SelectionMode", "Single");
        await Driver.WaitForProperty("TreeSelectAllButton", "IsVisible", "False");
    }


    [Test]
    public async Task Select_All_Checks_Every_Selectable_Node_And_Deselect_Clears()
    {
        await OpenPage();
        await Driver.Tap("TreeExpandAllButton");
        await WaitForRow("team-notes.md", 15);

        await Driver.SetProperty("IsToggled", "True", automationId: "TreeMultiSelectSwitch");
        await Driver.WaitForProperty("TreeSelectAllButton", "IsVisible", "True");

        await Driver.Tap("TreeSelectAllButton");
        // 19 nodes once both lazy levels are loaded, minus the locked secret.key.
        await Driver.WaitForProperty("TreeStatusLabel", "Text", "18 checked");

        await Driver.Tap("TreeDeselectAllButton");
        await Driver.WaitForProperty("TreeStatusLabel", "Text", "Selection cleared");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Refresh_Cloud_Reloads_The_Lazy_Branch()
    {
        await OpenPage();
        await Driver.Tap("TreeExpandAllButton");
        await WaitForRow("remote-backup.zip", 15);

        await Driver.Tap("TreeRefreshCloudButton");
        // Refresh drops the cached children and loads them again (900ms) — they come back.
        await WaitForRow("presentation.pptx", 15);
        await Assert.That(await Driver.IsAlive()).IsTrue();
        await AssertNoAppErrors();
    }
}
