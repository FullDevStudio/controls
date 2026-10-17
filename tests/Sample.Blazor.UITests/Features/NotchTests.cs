using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class NotchTests : BlazorTest
{
    ILocator Notch => Page.GetByTestId("demo-notch");

    Task<double[]> Box()
        => Notch.EvaluateAsync<double[]>("el => [el.offsetWidth, el.offsetHeight]");

    // The morph is a CSS transition; read the size once it has run out.
    async Task<double[]> SettledBox()
    {
        await Page.WaitForTimeoutAsync(700);
        return await Box();
    }

    [Test]
    public async Task Sizes_To_Its_Content_And_Grows_On_A_Swap()
    {
        await Open("notch");
        await Expect(Notch).ToContainTextAsync("0:42");

        var recording = await SettledBox();
        await Assert.That(recording[0]).IsGreaterThanOrEqualTo(120);
        await Assert.That(recording[1]).IsGreaterThanOrEqualTo(36);

        await Page.GetByTestId("notch-playing").ClickAsync();
        await Expect(Notch).ToContainTextAsync("Midnight Drive");
        var playing = await SettledBox();

        // 300px of content plus 14px padding either side
        await Assert.That(playing[0]).IsGreaterThan(recording[0]);
        await Assert.That(Math.Abs(playing[0] - 328)).IsLessThan(2);
        await Assert.That(playing[1]).IsGreaterThan(recording[1]);
        await Expect(Page.GetByTestId("notch-size")).ToContainTextAsync("328");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Empty_Shrinks_To_The_Minimum()
    {
        await Open("notch");
        await Page.GetByTestId("notch-message").ClickAsync();
        await SettledBox();

        await Page.GetByTestId("notch-empty").ClickAsync();
        var empty = await SettledBox();
        await Assert.That(empty[0]).IsEqualTo(120);
        await Assert.That(empty[1]).IsEqualTo(36);
    }

    [Test]
    public async Task Follows_Content_That_Changes_Its_Own_Size()
    {
        await Open("notch");
        await Page.GetByTestId("notch-longer").ClickAsync();
        var shortText = await SettledBox();

        await Page.GetByTestId("notch-longer").ClickAsync();
        await Page.GetByTestId("notch-longer").ClickAsync();
        var longText = await SettledBox();
        await Assert.That(longText[0]).IsGreaterThan(shortText[0]);

        // never wider than the stage it sits in: the last phrase wraps instead
        await Page.GetByTestId("notch-longer").ClickAsync();
        var wrapped = await SettledBox();
        var stage = await Notch.EvaluateAsync<double>("el => el.parentElement.clientWidth");
        await Assert.That(wrapped[0]).IsLessThanOrEqualTo(stage);
    }

    [Test]
    public async Task Notch_Shape_Squares_The_Top_Corners()
    {
        await Open("notch");
        await Page.GetByTestId("notch-shape").CheckAsync();
        await Expect(Notch).ToHaveClassAsync(new Regex("shiny-notch--notch"));
        await SettledBox();

        var topLeft = await Notch.EvaluateAsync<string>("el => getComputedStyle(el).borderTopLeftRadius");
        await Assert.That(topLeft).IsEqualTo("0px");
    }

    [Test]
    public async Task Clicking_The_Surface_Raises_OnClick()
    {
        await Open("notch");
        await Notch.ClickAsync();
        await Expect(Page.GetByTestId("notch-click-status")).ToContainTextAsync("Clicked at");
    }
}
