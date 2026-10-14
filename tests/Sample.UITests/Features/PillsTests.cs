namespace Sample.UITests.Features;

public class PillsTests : UITest
{
    Task OpenPage() => Open("pills", "PillPage");

    // PillView = ContentView > Border (the fill) > Label (the text)
    Task<string?> PillLabelText(string automationId) => Driver.GetProperty("Content.Content.Text", automationId);
    Task<string?> PillFill(string automationId) => Driver.GetProperty("Content.BackgroundColor", automationId);


    [Test]
    public async Task Typing_Updates_The_Bound_Pill()
    {
        await OpenPage();

        await Driver.SetProperty("Text", "Shipped", automationId: "PillTextEntry");

        await Driver.WaitForProperty("BoundPill", "Text", "Shipped");
        await Driver.WaitUntil(async () => await PillLabelText("BoundPill") == "Shipped", "the pill's label shows the new text");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Clearing_The_Text_Empties_The_Pill()
    {
        await OpenPage();

        await Driver.SetProperty("Text", "Temporary", automationId: "PillTextEntry");
        await Driver.WaitForProperty("BoundPill", "Text", "Temporary");

        await Driver.SetProperty("Text", "", automationId: "PillTextEntry");
        await Driver.WaitUntil(async () => String.IsNullOrEmpty(await Driver.GetProperty("Text", automationId: "BoundPill")), "the pill text is empty");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Changing_Type_Recolours_The_Pill()
    {
        await OpenPage();

        // BoundPill starts as Type="Info"
        await Driver.WaitForProperty("BoundPill", "Type", "Info");
        var info = await PillFill("BoundPill");

        await Driver.SetProperty("Type", "Critical", automationId: "BoundPill");
        await Driver.WaitForProperty("BoundPill", "Type", "Critical");
        await Driver.WaitUntil(async () => await PillFill("BoundPill") != info, "the fill changes with the type");

        await Driver.SetProperty("Type", "Info", automationId: "BoundPill");
        await Driver.WaitUntil(async () => await PillFill("BoundPill") == info, "the Info fill comes back");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Custom_PillColor_Fills_The_Pill()
    {
        await OpenPage();

        var fill = await PillFill("PurplePill");
        await Assert.That(fill).IsNotNull();
        await Assert.That(fill!.ToUpperInvariant()).Contains("7C3AED");
    }
}
