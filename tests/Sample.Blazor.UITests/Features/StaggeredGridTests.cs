using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

public class StaggeredGridTests : BlazorTest
{
    ILocator Section => Page.Locator("section.demo-section", new() { Has = Page.Locator("h2", new() { HasTextString = "Pinterest-style Masonry" }) });
    ILocator Grid => Section.Locator(".shiny-staggered");
    ILocator Items => Grid.Locator(".shiny-staggered-item");
    ILocator ColumnLabel => Section.Locator("strong").First;
    ILocator Minus => Section.GetByRole(AriaRole.Button, new() { Name = "-", Exact = true });
    ILocator Plus => Section.GetByRole(AriaRole.Button, new() { Name = "+", Exact = true });

    async Task<int> DistinctColumns()
    {
        var xs = new HashSet<int>();
        var count = await Items.CountAsync();
        for (var i = 0; i < count; i++)
        {
            var box = await Items.Nth(i).BoundingBoxAsync();
            if (box != null)
                xs.Add((int)Math.Round(box.X));
        }
        return xs.Count;
    }

    async Task WaitForColumns(int expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var actual = -1;
        while (DateTime.UtcNow < deadline)
        {
            actual = await DistinctColumns();
            if (actual == expected)
                return;
            await Task.Delay(100);
        }
        Assert.Fail($"Expected items laid out in {expected} columns, saw {actual}");
    }


    [Test]
    public async Task Items_Flow_Into_The_Column_Count()
    {
        await Open("staggeredgrid");
        await Expect(ColumnLabel).ToHaveTextAsync("3");
        await Expect(Items).ToHaveCountAsync(12);
        await WaitForColumns(3);
    }


    [Test]
    public async Task Plus_And_Minus_Change_The_Columns_Within_Limits()
    {
        await Open("staggeredgrid");

        await Plus.ClickAsync();
        await Expect(ColumnLabel).ToHaveTextAsync("4");
        await Expect(Grid).ToHaveAttributeAsync("style", new Regex("column-count:4"));
        await WaitForColumns(4);

        await Plus.ClickAsync();
        await Plus.ClickAsync();
        await Expect(ColumnLabel).ToHaveTextAsync("5");     // capped at 5

        for (var i = 0; i < 6; i++)
            await Minus.ClickAsync();
        await Expect(ColumnLabel).ToHaveTextAsync("1");     // floored at 1
        await Expect(Grid).ToHaveAttributeAsync("style", new Regex("column-count:1"));
        await WaitForColumns(1);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Clicking_An_Item_Selects_It()
    {
        await Open("staggeredgrid");
        await Expect(Section.GetByText("Selected:")).ToHaveCountAsync(0);

        await Items.Filter(new() { HasTextString = "Fitness" }).ClickAsync();
        await Expect(Section.Locator("p", new() { HasTextString = "Selected:" }).Locator("strong")).ToHaveTextAsync("Fitness");

        await Items.Filter(new() { HasTextString = "Books" }).ClickAsync();
        await Expect(Section.Locator("p", new() { HasTextString = "Selected:" }).Locator("strong")).ToHaveTextAsync("Books");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Items_Keep_Their_Own_Heights()
    {
        await Open("staggeredgrid");
        var music = await Items.Filter(new() { HasTextString = "Music" }).Locator(":scope > div").BoundingBoxAsync();
        var art = await Items.Filter(new() { HasTextString = "Art" }).Locator(":scope > div").BoundingBoxAsync();

        // Masonry, not a uniform grid: a 150px pin and a 280px pin stay that tall.
        await Assert.That(Math.Abs(music!.Height - 150)).IsLessThan(2f);
        await Assert.That(Math.Abs(art!.Height - 280)).IsLessThan(2f);
    }
}
