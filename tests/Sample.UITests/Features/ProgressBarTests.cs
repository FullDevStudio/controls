namespace Sample.UITests.Features;

public class ProgressBarTests : UITest
{
    Task OpenPage() => Open("progressbar", "ProgressBarPage");

    Task<double?> BasicValue() => Driver.GetDouble("Value", "BasicProgressBar");

    async Task WaitForValue(double expected)
    {
        await Driver.WaitUntil(async () => await BasicValue() == expected, $"BasicProgressBar.Value == {expected}");
        await Driver.WaitForProperty("ProgressValueLabel", "Text", $"Value: {expected:0}");
    }

    async Task<bool> PercentShowing(double value)
        => (await Driver.Query(text: $"{value:0}%")).Any(e => e.Text == $"{value:0}%");


    [Test]
    public async Task Increment_Adds_Ten()
    {
        await OpenPage();

        // the page keeps its view model between visits, so start from wherever it is - below the cap
        if (await BasicValue() >= 100)
            await Driver.Tap("DecrementButton");

        var start = (await BasicValue())!.Value;
        await Driver.Tap("IncrementButton");
        await WaitForValue(Math.Min(100, start + 10));
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Decrement_Subtracts_Ten()
    {
        await OpenPage();

        if (await BasicValue() <= 0)
            await Driver.Tap("IncrementButton");

        var start = (await BasicValue())!.Value;
        await Driver.Tap("DecrementButton");
        await WaitForValue(Math.Max(0, start - 10));
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Value_Clamps_At_Zero_And_One_Hundred()
    {
        await OpenPage();

        for (var i = 0; i < 11; i++)
            await Driver.Tap("DecrementButton");
        await WaitForValue(0);

        for (var i = 0; i < 11; i++)
            await Driver.Tap("IncrementButton");
        await WaitForValue(100);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Value_Is_Two_Way_Bound()
    {
        await OpenPage();

        // Value defaults to TwoWay: writing the control pushes into the view model, which every
        // other bar and the label are bound to
        await Driver.SetProperty("Value", "70", automationId: "BasicProgressBar");
        await WaitForValue(70);
        await Driver.WaitUntil(async () => await Driver.GetDouble("Value", "TextProgressBar") == 70, "the text bar follows");

        // ShowText bars render the percentage
        await Driver.WaitUntil(() => PercentShowing(70), "a bar shows 70%");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Indeterminate_Bar_Ignores_The_Value()
    {
        await OpenPage();

        await Driver.WaitForProperty("IndeterminateProgressBar", "IsIndeterminate", "True");
        await Driver.Tap("IncrementButton");
        await Driver.WaitForProperty("IndeterminateProgressBar", "IsIndeterminate", "True");
        await Driver.WaitForProperty("IndeterminateProgressBar", "Value", "0");
        await AssertNoAppErrors();
    }
}
