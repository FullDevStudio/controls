namespace Sample.UITests.Features;

/// <summary>
/// AutoCompleteEntry: local filtering, picking a suggestion (string and TextMemberPath objects),
/// typing again clearing the selection, and the SearchCommand path that fills ItemsSource later.
/// Suggestions are the Labels (or template roots) inside the dropdown, which floats in the page overlay
/// (not under the control) and carries the control's AutomationId plus "_DropDown"; their
/// tap gesture is what a finger would hit.
/// </summary>
public class AutoCompleteTests : UITest
{
    const string Route = "autocomplete";
    const string PageType = "AutoCompletePage";

    static readonly string[] Berries = ["Blackberry", "Blueberry", "Cranberry", "Raspberry", "Strawberry"];

    async Task<string[]> Suggestions(string autoCompleteId)
        => (await Driver.CssNow($"#{autoCompleteId}_DropDown ScrollView Label"))
            .Select(l => l.Text ?? "")
            .ToArray();

    async Task WaitForSuggestions(string autoCompleteId, params string[] expected)
        => await Driver.WaitUntil(
            async () => (await Suggestions(autoCompleteId)).OrderBy(x => x).SequenceEqual(expected.OrderBy(x => x)),
            $"{autoCompleteId} suggestions to be [{String.Join(", ", expected)}]",
            timeoutSeconds: 15
        );

    async Task TapSuggestion(string autoCompleteId, string text)
    {
        var items = await Driver.Css($"#{autoCompleteId}_DropDown ScrollView Label");
        var index = items.FindIndex(l => l.Text == text);
        if (index < 0)
            throw new InvalidOperationException($"'{text}' is not among the suggestions of {autoCompleteId}");

        await Driver.TapCss($"#{autoCompleteId}_DropDown ScrollView Label", index);
    }

    [Test]
    public async Task Typing_Filters_Local_Items()
    {
        await Open(Route, PageType);

        await Driver.FillCss("#FruitAuto BorderlessEntry", "berry");

        await WaitForSuggestions("FruitAuto", Berries);
        await Driver.WaitForValue("FruitAuto", "Text", "berry");
    }

    [Test, SkipOn(UIPlatform.Linux, KnownGaps.LinuxAutoCompleteGtkWedge)]
    public async Task Picking_A_Suggestion_Selects_It()
    {
        await Open(Route, PageType);
        await Driver.FillCss("#FruitAuto BorderlessEntry", "berry");
        await WaitForSuggestions("FruitAuto", Berries);

        await TapSuggestion("FruitAuto", "Blueberry");

        await Driver.WaitForValue("FruitAuto", "SelectedItem", "Blueberry");
        await Driver.WaitForValue("FruitAuto", "Text", "Blueberry");
        await Driver.WaitForValue("FruitSelected", "Text", "Selected: Blueberry");
        await AssertNoAppErrors();
    }

    [Test, SkipOn(UIPlatform.Linux, KnownGaps.LinuxAutoCompleteGtkWedge)]
    public async Task Typing_Again_Clears_The_Selection()
    {
        await Open(Route, PageType);
        await Driver.FillCss("#FruitAuto BorderlessEntry", "berry");
        await WaitForSuggestions("FruitAuto", Berries);
        await TapSuggestion("FruitAuto", "Raspberry");
        await Driver.WaitForValue("FruitAuto", "SelectedItem", "Raspberry");

        await Driver.FillCss("#FruitAuto BorderlessEntry", "Kiw");

        await Driver.WaitForValue("FruitAuto", "SelectedItem", null);
        await Driver.WaitForValue("AutoCompletePage", "BindingContext.SelectedFruit", null);
        await WaitForSuggestions("FruitAuto", "Kiwi");
    }

    [Test, SkipOn(UIPlatform.Linux, KnownGaps.LinuxAutoCompleteGtkWedge)]
    public async Task TextMemberPath_Shows_And_Selects_Objects()
    {
        await Open(Route, PageType);

        await Driver.FillCss("#LanguageAuto BorderlessEntry", "Script");
        await WaitForSuggestions("LanguageAuto", "JavaScript", "TypeScript");

        await TapSuggestion("LanguageAuto", "TypeScript");

        await Driver.WaitForValue("LanguageAuto", "Text", "TypeScript");
        await Driver.WaitForValue("LanguageSelected", "Text", "Selected: TypeScript (2012) - Multi-paradigm");
        await AssertNoAppErrors();
    }

    [Test, SkipOn(UIPlatform.Linux, KnownGaps.LinuxAutoCompleteGtkWedge)]
    public async Task SearchCommand_Results_Arrive_Asynchronously()
    {
        await Open(Route, PageType);

        await Driver.FillCss("#CityAuto BorderlessEntry", "to");

        // 300ms debounce + the view model's simulated 800ms round trip, then ItemsSource is replaced
        await WaitForSuggestions("CityAuto", "Tokyo", "Toronto", "Stockholm");
        await Driver.WaitForValue("CityAuto", "IsBusy", "False");

        await TapSuggestion("CityAuto", "Toronto");
        await Driver.WaitForValue("CitySelected", "Text", "Selected: Toronto (founded 1793) - Americas");
        await AssertNoAppErrors();
    }

    [Test, SkipOn(UIPlatform.Linux, KnownGaps.LinuxAutoCompleteGtkWedge)]
    public async Task Below_Threshold_Does_Not_Search()
    {
        await Open(Route, PageType);
        await Driver.FillCss("#CityAuto BorderlessEntry", "lond"); // "lon" would also match Barcelona
        await WaitForSuggestions("CityAuto", "London");

        // Threshold is 2: a single character never reaches the SearchCommand, so the last results stand
        await Driver.FillCss("#CityAuto BorderlessEntry", "x");
        await Task.Delay(1500);

        await Driver.WaitForValue("AutoCompletePage", "BindingContext.CityResults.Count", "1");
        await Driver.WaitForValue("CityAuto", "IsBusy", "False");
        await Driver.WaitForValue("CityAuto", "Text", "x");
    }
}
