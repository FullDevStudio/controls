namespace Sample.UITests.Features;

/// <summary>
/// The home page's catalogue search: a query swaps the sectioned browse for one ranked result list,
/// and the ✕ button puts the browse back.
/// </summary>
public class HomeTests : UITest
{
    /// <summary>Shell keeps the page alive between visits, so the search box is emptied on the way in.</summary>
    async Task OpenPage()
    {
        await Open("home", "HomePage");
        await Driver.SetProperty("Text", "", automationId: "CatalogSearch");
    }

    async Task Search(string query)
    {
        await Driver.SetProperty("Text", query, automationId: "CatalogSearch");
        await Driver.WaitForProperty("CatalogResults", "IsVisible", "True");
    }

    async Task<int> ResultCount()
    {
        var raw = await Driver.GetProperty("Text", automationId: "CatalogResultCount");
        return Int32.TryParse(raw, out var n) ? n : -1;
    }


    [Test]
    public async Task Query_Swaps_Browse_For_Results()
    {
        await OpenPage();
        await Driver.WaitForProperty("CatalogBrowse", "IsVisible", "True");
        await Driver.WaitForProperty("CatalogSearchClear", "IsVisible", "False");

        await Search("slider");

        await Driver.WaitForProperty("CatalogBrowse", "IsVisible", "False");
        await Driver.WaitForProperty("CatalogSearchClear", "IsVisible", "True");
        await Driver.WaitForProperty("CatalogNoResults", "IsVisible", "False");
        await Driver.WaitUntil(async () => await ResultCount() > 0, "the result count to report matches");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task No_Match_Says_So()
    {
        await OpenPage();

        await Search("zzqqxx");

        await Driver.WaitForProperty("CatalogResultCount", "Text", "0");
        await Driver.WaitForProperty("CatalogNoResults", "IsVisible", "True");
        await Assert.That(await Driver.GetProperty("Text", automationId: "CatalogNoResults")).Contains("zzqqxx");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Longer_Query_Narrows_The_Results()
    {
        await OpenPage();

        await Search("s");
        await Driver.WaitUntil(async () => await ResultCount() > 0, "a one-letter query to match something");
        var broad = await ResultCount();

        await Search("slider");
        await Driver.WaitUntil(async () =>
        {
            var n = await ResultCount();
            return n > 0 && n < broad;
        }, "the longer query to match fewer demos");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Clear_Button_Restores_Browse()
    {
        await OpenPage();
        await Search("grid");
        await Driver.WaitForProperty("CatalogSearchClear", "IsVisible", "True");

        await Driver.Tap(automationId: "CatalogSearchClear");

        await Driver.WaitUntil(async () => String.IsNullOrEmpty(await Driver.GetProperty("Text", automationId: "CatalogSearch")), "the search box to empty");
        await Driver.WaitForProperty("CatalogBrowse", "IsVisible", "True");
        await Driver.WaitForProperty("CatalogResults", "IsVisible", "False");
        await Driver.WaitForProperty("CatalogSearchClear", "IsVisible", "False");
        await AssertNoAppErrors();
    }
}
