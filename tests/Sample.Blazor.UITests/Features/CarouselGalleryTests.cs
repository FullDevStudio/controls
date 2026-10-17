using System.Text.RegularExpressions;

namespace Sample.Blazor.UITests.Features;

/// <summary>/carousel (CarouselGallery) and /carousel-advanced (the drag-engine Carousel).</summary>
public class CarouselGalleryTests : BlazorTest
{
    static readonly Regex Active = new(@"\bactive\b");

    ILocator Section(string heading)
        => Page.Locator("section.demo-section", new() { Has = Page.Locator("h2", new() { HasTextRegex = new Regex("^" + Regex.Escape(heading) + "$") }) });

    ILocator Example(string heading)
        => Page.Locator("article.ex", new() { Has = Page.Locator("h3", new() { HasTextRegex = new Regex("^" + Regex.Escape(heading) + "$") }) });


    // ── CarouselGallery (/carousel) ─────────────────────────────────────────

    [Test]
    public async Task Gallery_Indicator_Moves_Position_And_Focus()
    {
        await Open("carousel");
        var basic = Section("Basic Carousel");
        var position = basic.Locator("p", new() { HasTextString = "Position:" }).Locator("strong");
        await Expect(position).ToHaveTextAsync("0");

        await basic.Locator(".shiny-carousel-dot").Nth(3).ClickAsync();

        await Expect(position).ToHaveTextAsync("3");
        await Expect(basic.Locator(".shiny-carousel-dot").Nth(3)).ToHaveClassAsync(Active);
        await Expect(basic.Locator(".shiny-carousel-dot").Nth(0)).Not.ToHaveClassAsync(Active);
        await Expect(basic.Locator(".shiny-carousel-focused")).ToHaveTextAsync("Sci-Fi");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Gallery_Clicking_An_Item_Focuses_It()
    {
        await Open("carousel");
        var basic = Section("Basic Carousel");
        var drama = basic.Locator(".shiny-carousel-item", new() { HasTextString = "Drama" });

        await Expect(drama).ToHaveAttributeAsync("style", new Regex(@"scale\(0\.85\)"));
        await drama.ClickAsync();

        await Expect(drama).ToHaveClassAsync(new Regex(@"\bshiny-carousel-focused\b"));
        await Expect(drama).ToHaveAttributeAsync("style", new Regex(@"scale\(1\)"));
        await Expect(basic.Locator("p", new() { HasTextString = "Position:" }).Locator("strong")).ToHaveTextAsync("2");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Gallery_Without_Indicators_Renders_No_Dots()
    {
        await Open("carousel");
        await Expect(Section("With Scale Effect").Locator(".shiny-carousel-item")).ToHaveCountAsync(6);
        await Expect(Section("With Scale Effect").Locator(".shiny-carousel-dot")).ToHaveCountAsync(0);
        await Expect(Section("Image Cards").Locator(".shiny-carousel-dot")).ToHaveCountAsync(5);
    }


    // ── Carousel (/carousel-advanced) ───────────────────────────────────────

    [Test]
    public async Task Arrows_Step_Through_Slides()
    {
        await Open("carousel-advanced");
        var carousel = Example("Default");
        var dots = carousel.Locator(".shiny-carousel-dot");
        var prev = carousel.GetByRole(AriaRole.Button, new() { Name = "Previous" });
        var next = carousel.GetByRole(AriaRole.Button, new() { Name = "Next" });

        await Expect(dots.Nth(0)).ToHaveClassAsync(Active);

        await next.ClickAsync();
        await Expect(dots.Nth(1)).ToHaveClassAsync(Active);
        await Expect(dots.Nth(0)).Not.ToHaveClassAsync(Active);
        await Expect(carousel.Locator(".shiny-carousel-container")).Not.ToHaveAttributeAsync("style", new Regex(@"translate3d\(0px"));

        await prev.ClickAsync();
        await Expect(dots.Nth(0)).ToHaveClassAsync(Active);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Loop_Wraps_In_Both_Directions()
    {
        await Open("carousel-advanced");
        var carousel = Example("Loop");
        var dots = carousel.Locator(".shiny-carousel-dot");

        await carousel.GetByRole(AriaRole.Button, new() { Name = "Previous" }).ClickAsync();
        await Expect(dots.Nth(5)).ToHaveClassAsync(Active);

        await carousel.GetByRole(AriaRole.Button, new() { Name = "Next" }).ClickAsync();
        await Expect(dots.Nth(0)).ToHaveClassAsync(Active);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Indicator_Jumps_To_Its_Slide()
    {
        await Open("carousel-advanced");
        var carousel = Example("Default");

        await carousel.GetByRole(AriaRole.Tab, new() { Name = "Go to 5" }).ClickAsync();
        await Expect(carousel.Locator(".shiny-carousel-dot").Nth(4)).ToHaveClassAsync(Active);
        await Expect(carousel.Locator(".shiny-carousel-dot").Nth(0)).Not.ToHaveClassAsync(Active);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Keyboard_Arrows_Move_The_Carousel()
    {
        await Open("carousel-advanced");
        var carousel = Example("Default");
        var region = carousel.GetByRole(AriaRole.Region);

        await region.FocusAsync();
        await region.PressAsync("ArrowRight");
        await Expect(carousel.Locator(".shiny-carousel-dot").Nth(1)).ToHaveClassAsync(Active);
        await region.PressAsync("ArrowRight");
        await Expect(carousel.Locator(".shiny-carousel-dot").Nth(2)).ToHaveClassAsync(Active);

        await region.PressAsync("End");
        await Expect(carousel.Locator(".shiny-carousel-dot").Nth(5)).ToHaveClassAsync(Active);

        await region.PressAsync("ArrowLeft");
        await Expect(carousel.Locator(".shiny-carousel-dot").Nth(4)).ToHaveClassAsync(Active);

        await region.PressAsync("Home");
        await Expect(carousel.Locator(".shiny-carousel-dot").Nth(0)).ToHaveClassAsync(Active);
        await AssertNoConsoleErrors();
    }


    [Test]
    [SkipOnPhone("a mouse drag - a phone swipes, and TouchGestureTests covers the carousel swipe with real touch input")]
    public async Task Mouse_Drag_Advances_A_Slide()
    {
        await Open("carousel-advanced");
        var carousel = Example("Default");
        var viewport = carousel.Locator(".shiny-carousel-viewport");
        await viewport.ScrollIntoViewIfNeededAsync();

        var box = (await viewport.BoundingBoxAsync())!;
        var y = box.Y + box.Height / 2f;
        var startX = box.X + box.Width * 0.75f;

        await Page.Mouse.MoveAsync(startX, y);
        await Page.Mouse.DownAsync();
        for (var i = 1; i <= 10; i++)
            await Page.Mouse.MoveAsync(startX - i * 20, y);
        await Page.Mouse.UpAsync();

        await Expect(carousel.Locator(".shiny-carousel-dot").Nth(1)).ToHaveClassAsync(Active);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Counter_Tracks_Position()
    {
        await Open("carousel-advanced");
        var carousel = Example("Counter + progress");
        var counter = carousel.Locator(".shiny-carousel-counter");

        await Expect(counter).ToHaveTextAsync("1 / 6");
        await carousel.GetByRole(AriaRole.Button, new() { Name = "Next" }).ClickAsync();
        await Expect(counter).ToHaveTextAsync("2 / 6");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Thumbnail_Navigates_To_Its_Slide()
    {
        await Open("carousel-advanced");
        var carousel = Example("Thumbnails");

        await carousel.GetByRole(AriaRole.Button, new() { Name = "Go to slide 4" }).ClickAsync();
        await Expect(carousel.Locator(".shiny-carousel-thumb").Nth(3)).ToHaveClassAsync(Active);
        await Expect(carousel.Locator(".shiny-carousel-thumb").Nth(0)).Not.ToHaveClassAsync(Active);
        await AssertNoConsoleErrors();
    }
}
