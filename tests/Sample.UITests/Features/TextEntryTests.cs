namespace Sample.UITests.Features;

/// <summary>
/// TextEntry: two-way text binding in both directions, validation feeding HasError/HintText,
/// masks, the character counter, the clear and stepper tools. Typing goes through the inner
/// BorderlessEntry (DevFlow fills Entry only); programmatic changes go through TextEntry.Text.
/// Shell caches the page, so every test reads its own starting point instead of assuming one.
/// </summary>
public class TextEntryTests : UITest
{
    const string Route = "textentry";
    const string PageType = "TextEntryPage";

    [Test]
    public async Task Typing_Updates_Control_And_ViewModel()
    {
        await Open(Route, PageType);
        var value = "Jane" + Random.Shared.Next(100, 999);

        await Driver.FillCss("#FirstNameEntry BorderlessEntry", value);

        await Driver.WaitForValue("FirstNameEntry", "Text", value);
        await Driver.WaitForValue("TextEntryPage", "BindingContext.FirstName", value);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Setting_Text_Reaches_The_Inner_Entry()
    {
        await Open(Route, PageType);
        var value = "Doe" + Random.Shared.Next(100, 999);

        await Driver.SetProperty("Text", value, automationId: "LastNameEntry");

        await Driver.WaitForCssValue("#LastNameEntry BorderlessEntry", "Text", value);
        await Driver.WaitForValue("TextEntryPage", "BindingContext.LastName", value);
    }

    [Test]
    public async Task Invalid_Email_Raises_Error_Until_Corrected()
    {
        await Open(Route, PageType);

        await Driver.SetProperty("Text", "not-an-email", automationId: "EmailEntry");
        await Driver.WaitForValue("EmailEntry", "HasError", "True");
        await Driver.WaitForValue("EmailEntry", "HintText", "Please enter a valid email address");

        await Driver.SetProperty("Text", "someone@example.com", automationId: "EmailEntry");
        await Driver.WaitForValue("EmailEntry", "HasError", "False");
        await Driver.WaitForValue("EmailEntry", "HintText", null);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Mask_Formats_Programmatic_Text()
    {
        await Open(Route, PageType);

        await Driver.SetProperty("Text", "5551234567", automationId: "MaskedPhoneEntry");

        await Driver.WaitForValue("MaskedPhoneEntry", "FormattedText", "(555) 123-4567");
        await Driver.WaitForCssValue("#MaskedPhoneEntry BorderlessEntry", "Text", "(555) 123-4567");
        await Driver.WaitForValue("TextEntryPage", "BindingContext.MaskedPhone", "5551234567");
    }

    [Test]
    public async Task Mask_Formats_Typed_Digits_And_Keeps_Raw_Text()
    {
        await Open(Route, PageType);

        await Driver.FillCss("#MaskedCardEntry BorderlessEntry", "4111222233334444");

        await Driver.WaitForValue("MaskedCardEntry", "Text", "4111222233334444");
        await Driver.WaitForValue("MaskedCardEntry", "FormattedText", "4111 2222 3333 4444");
        await Driver.WaitForCssValue("#MaskedCardEntry BorderlessEntry", "Text", "4111 2222 3333 4444");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Character_Count_Follows_Typing()
    {
        await Open(Route, PageType);

        await Driver.FillCss("#BioEntry BorderlessEntry", "Hello there");

        await Driver.WaitUntil(
            async () => (await Driver.CssNow("#BioEntry Label")).Any(l => l.Text == "11/100"),
            "the Bio counter to read 11/100"
        );
    }

    [Test]
    public async Task Clear_Button_Appears_With_Text_And_Empties_The_Field()
    {
        await Open(Route, PageType);

        await Driver.SetProperty("Text", "search me", automationId: "SearchEntry");
        await Driver.WaitForValue("SearchClear", "IsVisible", "True");

        await Driver.Tap(automationId: "SearchClear");

        await Driver.WaitForValue("SearchEntry", "Text", "");
        await Driver.WaitForValue("TextEntryPage", "BindingContext.SearchText", "");
        await Driver.WaitForValue("SearchClear", "IsVisible", "False");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Stepper_Tools_Increment_And_Decrement()
    {
        await Open(Route, PageType);

        var raw = await Driver.GetProperty("Text", automationId: "QuantityEntry");
        var start = Double.TryParse(raw, out var parsed) ? parsed : 0;

        await Driver.Tap(automationId: "QuantityIncrement");
        await Driver.WaitForValue("QuantityEntry", "Text", (start + 1).ToString("G"));
        await Driver.WaitForValue("TextEntryPage", "BindingContext.Quantity", (start + 1).ToString("G"));

        await Driver.Tap(automationId: "QuantityDecrement");
        await Driver.Tap(automationId: "QuantityDecrement");
        await Driver.WaitForValue("QuantityEntry", "Text", (start - 1).ToString("G"));
        await AssertNoAppErrors();
    }

    [Test]
    public async Task ReadOnly_Reaches_The_Inner_Entry()
    {
        await Open(Route, PageType);

        await Driver.WaitForCssValue("#ReadOnlyEntry BorderlessEntry", "IsReadOnly", "True");
        await Driver.WaitForCssValue("#ReadOnlyEntry BorderlessEntry", "Text", "This field is read only");
        await Driver.WaitForCssValue("#FirstNameEntry BorderlessEntry", "IsReadOnly", "False");
    }
}
