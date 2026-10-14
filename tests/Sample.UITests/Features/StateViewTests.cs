namespace Sample.UITests.Features;

/// <summary>StateView on <c>stateview</c>: one bound string picks the branch, through Loading to Loaded or Error.</summary>
public class StateViewTests : UITest
{
    const string View = "DemoStateView";
    const string Label = "StateChangeLabel";

    async Task OpenAtEmpty()
    {
        await Open("stateview", "StateViewPage");
        // a Load/Fail from an earlier test may still be in flight (1.5s)
        await Driver.WaitUntil(async () => await Driver.GetProperty("CurrentState", automationId: View) != "Loading", "no load in flight");
        await Driver.Tap(automationId: "StateEmptyButton");
        await Driver.WaitForProperty(View, "CurrentState", "Empty");
    }

    async Task<bool> TextShowing(string text)
        => (await Driver.Query(text: text)).Any(e => e.IsVisible);

    [Test]
    public async Task Load_Goes_Through_Loading_To_Loaded()
    {
        await OpenAtEmpty();

        await Driver.Tap(automationId: "StateLoadButton");
        await Driver.WaitForProperty(View, "CurrentState", "Loading");
        await Driver.WaitForProperty(Label, "Text", "CurrentState = Loading");

        await Driver.WaitForProperty(View, "CurrentState", "Loaded", timeoutSeconds: 10);
        await Driver.WaitForProperty(Label, "Text", "CurrentState = Loaded");

        // the Loaded branch is a ContentTemplate, built when first reached
        await Driver.WaitUntil(() => TextShowing("This branch came from a ContentTemplate"), "the Loaded template's content");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Fail_Shows_Error_And_Try_Again_Recovers()
    {
        await OpenAtEmpty();

        await Driver.Tap(automationId: "StateFailButton");
        await Driver.WaitForProperty(View, "CurrentState", "Error", timeoutSeconds: 10);
        await Driver.WaitUntilExists("StateTryAgainButton");

        await Driver.Tap(automationId: "StateTryAgainButton");
        await Driver.WaitForProperty(View, "CurrentState", "Loading");
        await Driver.WaitForProperty(View, "CurrentState", "Loaded", timeoutSeconds: 10);
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Setting_CurrentState_On_The_View_Writes_Back()
    {
        await OpenAtEmpty();

        // CurrentState is two-way: the page's status line comes from the view-model
        await Driver.SetProperty("CurrentState", "Error", automationId: View);
        await Driver.WaitForProperty(Label, "Text", "CurrentState = Error");

        await Driver.Tap(automationId: "StateEmptyButton");
        await Driver.WaitForProperty(View, "CurrentState", "Empty");
        await Driver.WaitForProperty(Label, "Text", "CurrentState = Empty");
        await AssertNoAppErrors();
    }

    [Test]
    public async Task Unknown_State_Falls_Back_Without_Throwing()
    {
        await OpenAtEmpty();

        await Driver.Tap(automationId: "StateFailButton");
        await Driver.WaitForProperty(View, "CurrentState", "Error", timeoutSeconds: 10);

        // a typo resolves to DefaultState / the first declared state instead of throwing or blanking;
        // the name itself still round-trips to the view-model
        await Driver.SetProperty("CurrentState", "NoSuchState", automationId: View);
        await Driver.WaitForProperty(Label, "Text", "CurrentState = NoSuchState");

        await Driver.Tap(automationId: "StateEmptyButton");
        await Driver.WaitForProperty(View, "CurrentState", "Empty");
        await AssertNoAppErrors();
    }
}
