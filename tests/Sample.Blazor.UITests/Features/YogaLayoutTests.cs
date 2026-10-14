using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class YogaLayoutTests : BlazorTest
{
    ILocator Section(string heading)
        => Page.Locator("section.demo-section", new() { Has = Page.Locator("h2", new() { HasTextRegex = new Regex("^" + Regex.Escape(heading) + "$") }) });

    ILocator Playground => Section("Playground").Locator(".shiny-yoga").First;
    ILocator PlaygroundChild(int index) => Playground.Locator(":scope > .shiny-yoga").Nth(index);

    ILocator Select(string label) => Section("Playground").Locator("label", new() { HasTextString = label }).Locator("select");

    static async Task<LocatorBoundingBoxResult> Box(ILocator locator)
        => await locator.BoundingBoxAsync() ?? throw new InvalidOperationException("element has no box");

    static async Task WaitFor(Func<Task<bool>> condition, string what)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (await condition())
                return;
            await Task.Delay(100);
        }
        Assert.Fail("Timed out waiting for: " + what);
    }


    [Test]
    public async Task Default_Is_A_Column_And_Row_Lays_Children_Side_By_Side()
    {
        await Open("yoga");

        var a = await Box(PlaygroundChild(0));
        var b = await Box(PlaygroundChild(1));
        await Assert.That(b.Y).IsGreaterThan(a.Y + 50);           // stacked under the 60px box
        await Assert.That(Math.Abs(b.X - a.X)).IsLessThan(1);

        await Select("FlexDirection").SelectOptionAsync("Row");
        await Expect(Playground).ToHaveAttributeAsync("style", new Regex("flex-direction:row"));
        await WaitFor(async () =>
        {
            var a2 = await Box(PlaygroundChild(0));
            var b2 = await Box(PlaygroundChild(1));
            return b2.X > a2.X + 50 && Math.Abs(b2.Y - a2.Y) < 1;
        }, "row layout");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Gap_Slider_Sets_The_Space_Between_Children()
    {
        await Open("yoga");

        await Section("Playground").Locator("input[type=range]").FillAsync("20");
        await Expect(Section("Playground").Locator("label", new() { HasTextString = "Gap:" })).ToContainTextAsync("Gap: 20 px");

        await WaitFor(async () =>
        {
            var a = await Box(PlaygroundChild(0));
            var b = await Box(PlaygroundChild(1));
            return Math.Abs(b.Y - (a.Y + a.Height) - 20) < 1;
        }, "a 20px gap between the first two boxes");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Justify_FlexEnd_Pushes_Children_To_The_End()
    {
        await Open("yoga");

        await Select("JustifyContent").SelectOptionAsync("FlexEnd");
        await WaitFor(async () =>
        {
            var container = await Box(Playground);
            var last = await Box(PlaygroundChild(3));
            // 8px padding at the bottom of a 240px column
            return Math.Abs(container.Y + container.Height - 8 - (last.Y + last.Height)) < 1;
        }, "last child flush with the bottom padding");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task AlignItems_Center_Centres_Children_Across_The_Column()
    {
        await Open("yoga");

        // Stretch is the default, but the children carry explicit widths so they keep them.
        await Select("AlignItems").SelectOptionAsync("Center");
        await WaitFor(async () =>
        {
            var container = await Box(Playground);
            var first = await Box(PlaygroundChild(0));
            var mid = container.X + container.Width / 2;
            return Math.Abs(first.X + first.Width / 2 - mid) < 1.5;
        }, "first child centred horizontally");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Auto_Start_Margin_Pushes_Done_To_The_End()
    {
        await Open("yoga");
        var bar = Section("App bar: grow and an auto margin").Locator(":scope > .shiny-yoga").Nth(1);
        var done = bar.GetByText("Done");

        var barBox = await Box(bar);
        var doneBox = await Box(done);
        // 12px horizontal padding on the bar
        await Assert.That(Math.Abs(barBox.X + barBox.Width - 12 - (doneBox.X + doneBox.Width))).IsLessThan(1.5f);
    }


    [Test]
    public async Task Percentage_Widths_Resolve_Against_The_Container()
    {
        await Open("yoga");
        var row = Section("Percentages").Locator(".shiny-yoga").First;
        var rowBox = await Box(row);
        var quarter = await Box(row.Locator(":scope > .shiny-yoga").Nth(0));
        var half = await Box(row.Locator(":scope > .shiny-yoga").Nth(1));

        await Assert.That(Math.Abs(quarter.Width - rowBox.Width * 0.25)).IsLessThan(1.5f);
        await Assert.That(Math.Abs(half.Width - rowBox.Width * 0.5)).IsLessThan(1.5f);
    }


    [Test]
    public async Task Absolute_Badge_Is_Inset_From_The_Card_Corner()
    {
        await Open("yoga");
        var section = Section("Card: aspect ratio and an absolute badge");
        var card = section.Locator(".shiny-yoga").First.Locator(":scope > .shiny-yoga").First;
        var badge = card.Locator(":scope > .shiny-yoga", new() { HasTextString = "NEW" });

        var cardBox = await Box(card);
        var badgeBox = await Box(badge);
        await Assert.That(Math.Abs(badgeBox.Y - cardBox.Y - 8)).IsLessThan(1.5f);
        await Assert.That(Math.Abs(cardBox.X + cardBox.Width - 8 - (badgeBox.X + badgeBox.Width))).IsLessThan(1.5f);

        // AspectRatio 1.5: the image block is two thirds as tall as it is wide.
        var image = await Box(card.Locator(":scope > .shiny-yoga").First);
        await Assert.That(Math.Abs(image.Width / image.Height - 1.5)).IsLessThan(0.05f);
    }
}
