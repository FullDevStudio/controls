using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The floor plan editor (/floorplan) and the read-only seating chart (/seatingchart). The plan is a
/// Skia canvas, so elements have no DOM: tests click real canvas coordinates and read the page's
/// status line (the selected element, or "&lt;tool&gt; tool"). Where an element sits depends on the
/// viewport, so tests scan the canvas for a point that lands on what they need instead of hardcoding one.
/// </summary>
public class FloorPlanTests : BlazorTest
{
    static readonly Regex Desk = new(@"^Desk \d+ — \S");

    ILocator Status => Page.Locator(".demo-section p strong");
    ILocator Canvas => Page.Locator("canvas.shiny-floorplan-canvas");
    ILocator Tool(string name) => Page.Locator(".demo-row button.demo-button", new() { HasTextRegex = new Regex($"^{Regex.Escape(name)}$") });

    async Task ClickCanvas(double fx, double fy)
    {
        await Canvas.ScrollIntoViewIfNeededAsync();
        var box = (await Canvas.BoundingBoxAsync())!;
        await Page.Mouse.ClickAsync((float)(box.X + box.Width * fx), (float)(box.Y + box.Height * fy));
    }

    /// <summary>Clicks a grid of canvas points until the status line matches; returns the hit point.</summary>
    async Task<(double X, double Y, string Text)?> ScanFor(Regex match)
    {
        // The plan is fitted to the canvas width, so on a phone it - and every desk - is a third of
        // its desktop size: rows that step 5% of the height would pass straight between them.
        await Canvas.ScrollIntoViewIfNeededAsync();
        var width = (await Canvas.BoundingBoxAsync())!.Width;
        var rowStep = 0.05 * Math.Min(1.0, width / 700.0);
        for (var fy = 0.15; fy <= 0.85; fy += rowStep)
        {
            for (var fx = 0.15; fx <= 0.85; fx += 0.03)
            {
                await ClickCanvas(fx, fy);
                await Page.WaitForTimeoutAsync(60);
                var text = await Status.InnerTextAsync();
                if (match.IsMatch(text))
                    return (fx, fy, text);
            }
        }
        return null;
    }

    [Test]
    public async Task Tool_Buttons_Switch_The_Active_Tool()
    {
        await Open("floorplan");
        await Expect(Status).ToHaveTextAsync("Select tool");
        await Expect(Tool("Select")).ToHaveClassAsync(new Regex("is-active"));

        foreach (var tool in new[] { "Pan", "Room", "Wall", "Cubicle", "Door", "Desk", "Chair", "Outlet", "Data port" })
        {
            await Tool(tool).ClickAsync();
            await Expect(Status).ToHaveTextAsync($"{tool} tool");
            await Expect(Tool(tool)).ToHaveClassAsync(new Regex("is-active"));
            await Expect(Tool("Select")).Not.ToHaveClassAsync(new Regex("is-active"));
        }

        await Tool("Select").ClickAsync();
        await Expect(Status).ToHaveTextAsync("Select tool");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Clicking_A_Desk_Selects_It_And_Empty_Space_Clears_It()
    {
        await Open("floorplan");
        var hit = await ScanFor(Desk);
        await Assert.That(hit).IsNotNull().Because("some point on the plan should land on an occupied desk");

        // The canvas corner is outside the drawn plan.
        await ClickCanvas(0.01, 0.01);
        await Expect(Status).ToHaveTextAsync("Select tool");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Delete_Removes_The_Selected_Desk()
    {
        await Open("floorplan");
        var hit = await ScanFor(Desk);
        await Assert.That(hit).IsNotNull();
        var desk = hit!.Value.Text;

        await Page.GetByRole(AriaRole.Button, new() { Name = "Delete", Exact = true }).ClickAsync();
        await Expect(Status).Not.ToHaveTextAsync(desk);

        // The same point now lands on whatever was under the desk (the room), never the desk again.
        await ClickCanvas(hit.Value.X, hit.Value.Y);
        await Page.WaitForTimeoutAsync(200);
        await Expect(Status).Not.ToHaveTextAsync(desk);
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Placing_Furniture_Adds_And_Selects_It()
    {
        await Open("floorplan");
        await Tool("Desk").ClickAsync();
        await Expect(Status).ToHaveTextAsync("Desk tool");

        await ClickCanvas(0.05, 0.05);

        await Expect(Status).ToHaveTextAsync("Furniture");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Seating_Chart_Tap_Reports_Desk_And_Occupant()
    {
        await Open("seatingchart");
        await Expect(Status).ToHaveTextAsync("Tap a desk or a room");

        var hit = await ScanFor(Desk);

        await Assert.That(hit).IsNotNull().Because("tapping a desk in View mode should report its name and occupant");
        await AssertNoConsoleErrors();
    }

    [Test]
    public async Task Seating_Chart_Tap_Reports_Room()
    {
        await Open("seatingchart");
        var hit = await ScanFor(new Regex("^(Main Office|Conference.*)$"));
        await Assert.That(hit).IsNotNull();
        await AssertNoConsoleErrors();
    }
}
