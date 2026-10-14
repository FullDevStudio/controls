using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>/layout — VStack, HStack and the responsive 12-column Grid.</summary>
public class LayoutTests : BlazorTest
{
    ILocator Section(string heading)
        => Page.Locator("section.demo-section", new() { Has = Page.Locator("h2", new() { HasTextRegex = new Regex("^" + Regex.Escape(heading) + "$") }) });

    ILocator HStack => Section("HStack").Locator(".shiny-hstack");
    ILocator HChild(string text) => HStack.Locator(":scope > div", new() { HasTextRegex = new Regex("^" + text + "$") });
    ILocator GridCell(string text) => Section("Responsive Grid").GetByText(text, new() { Exact = true });

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
    public async Task VStack_Stacks_And_Aligns_To_Start()
    {
        await Open("layout");
        var stack = Section("VStack").Locator(".shiny-vstack");
        var first = await Box(stack.GetByText("First", new() { Exact = true }));
        var second = await Box(stack.GetByText("Second", new() { Exact = true }));

        await Assert.That(Math.Abs(second.Y - (first.Y + first.Height) - 8)).IsLessThan(1);   // Spacing="8"
        await Assert.That(Math.Abs(second.X - first.X)).IsLessThan(1);                         // Align Start
    }


    [Test]
    public async Task HStack_Justify_End_Moves_Children_To_The_Right()
    {
        await Open("layout");
        var startX = (await Box(HChild("A"))).X;

        await Section("HStack").Locator("select").Nth(0).SelectOptionAsync("End");
        await Expect(HStack).ToHaveAttributeAsync("style", new Regex("justify-content:flex-end"));

        await WaitFor(async () =>
        {
            var stack = await Box(HStack);
            var d = await Box(HChild("D"));
            return (await Box(HChild("A"))).X > startX + 20 && Math.Abs(stack.X + stack.Width - 12 - (d.X + d.Width)) < 1.5;
        }, "D flush with the right padding");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task HStack_Align_Start_Tops_Children()
    {
        await Open("layout");
        await Section("HStack").Locator("select").Nth(1).SelectOptionAsync("Start");
        await Expect(HStack).ToHaveAttributeAsync("style", new Regex("align-items:flex-start"));

        await WaitFor(async () =>
        {
            var a = await Box(HChild("A"));
            var d = await Box(HChild("D"));
            return Math.Abs(a.Y - d.Y) < 1;
        }, "A (short) and D (tall) to share a top edge");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task HStack_Spacing_Slider_Changes_The_Gap()
    {
        await Open("layout");
        await Section("HStack").Locator("input[type=range]").FillAsync("30");
        await Expect(Section("HStack").GetByText("Spacing: 30 px")).ToBeVisibleAsync();

        await WaitFor(async () =>
        {
            var a = await Box(HChild("A"));
            var b = await Box(HChild("B"));
            return Math.Abs(b.X - (a.X + a.Width) - 30) < 1;
        }, "a 30px gap between A and B");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Grid_Columns_Stack_On_A_Phone_And_Share_A_Row_On_Desktop()
    {
        await Page.SetViewportSizeAsync(1400, 900);
        await Open("layout");
        var cells = Section("Responsive Grid").GetByText("Xs 12 · Md 6 · Lg 3", new() { Exact = true });

        // Lg 3: four across.
        await WaitFor(async () =>
        {
            var c0 = await Box(cells.Nth(0));
            var c3 = await Box(cells.Nth(3));
            return Math.Abs(c0.Y - c3.Y) < 1 && c3.X > c0.X;
        }, "four Lg-3 columns on one row");

        // Xs 12: every column full width, one under another.
        await Page.SetViewportSizeAsync(420, 900);
        await WaitFor(async () =>
        {
            var c0 = await Box(cells.Nth(0));
            var c1 = await Box(cells.Nth(1));
            return c1.Y > c0.Y + c0.Height - 1 && Math.Abs(c1.X - c0.X) < 1;
        }, "Xs-12 columns stacked");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Grid_Order_Puts_The_Sidebar_Last_On_Phones_Only()
    {
        await Page.SetViewportSizeAsync(1400, 900);
        await Open("layout");
        var sidebar = GridCell("Sidebar — last on phones, first from md");
        var content = GridCell("Content");

        await WaitFor(async () => (await Box(sidebar)).X < (await Box(content)).X, "sidebar left of content from md");

        await Page.SetViewportSizeAsync(420, 900);
        await WaitFor(async () => (await Box(sidebar)).Y > (await Box(content)).Y, "sidebar under content on a phone");
        await AssertNoConsoleErrors();
    }
}
