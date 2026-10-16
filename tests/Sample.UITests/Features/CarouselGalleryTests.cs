namespace Sample.UITests.Features;

[SkipOn(UIPlatform.MacOS, KnownGaps.CarouselGalleryNoHandler)]
[SkipOn(UIPlatform.Linux, KnownGaps.CarouselGalleryNoHandler)]
public class CarouselGalleryTests : UITest
{
    Task OpenPage() => Open("carouselgallery", "CarouselGalleryPage");

    [Test]
    public async Task Toggle_Snap_Switches_To_Free_Scroll_And_Back()
    {
        await OpenPage();
        await Driver.WaitForProperty("SnapLabel", "Text", "Snap (1)");

        await Driver.Tap("ToggleSnapButton");
        await Driver.WaitForProperty("SnapLabel", "Text", "Free Scroll");
        await Driver.WaitForProperty("CarouselStatusLabel", "Text", "Free scroll — Netflix-style smooth scrolling");
        await Driver.WaitForProperty("BasicCarousel", "SnapCount", "0");

        await Driver.Tap("ToggleSnapButton");
        await Driver.WaitForProperty("SnapLabel", "Text", "Snap (1)");
        await Driver.WaitForProperty("BasicCarousel", "SnapCount", "1");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Toggle_Scale_Turns_The_Focus_Effect_Off_And_On()
    {
        await OpenPage();
        await Driver.WaitForProperty("ScaleLabel", "Text", "Scale (0.85)");
        await Driver.WaitForProperty("ScaleCarousel", "UnfocusedItemScale", "0.85");

        await Driver.Tap("ToggleScaleButton");
        await Driver.WaitForProperty("ScaleLabel", "Text", "Scale Off");
        await Driver.WaitForProperty("CarouselStatusLabel", "Text", "Scale effect off — all items same size");
        await Driver.WaitForProperty("ScaleCarousel", "UnfocusedItemScale", "1");
        await Driver.WaitForProperty("BasicCarousel", "UnfocusedItemScale", "1");

        await Driver.Tap("ToggleScaleButton");
        await Driver.WaitForProperty("ScaleLabel", "Text", "Scale (0.85)");
        await Driver.WaitForProperty("ScaleCarousel", "UnfocusedItemScale", "0.85");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Setting_CurrentPosition_Scrolls_And_Sticks()
    {
        await OpenPage();

        // Shell keeps the page alive, so an earlier test can leave the carousel elsewhere (on Android,
        // re-enabling snap re-centres it: LinearSnapHelper snaps to the centre item while
        // ScrollToPositionWithOffset start-aligns, so 0 becomes 2). Start from a known position.
        await Driver.SetProperty("CurrentPosition", "0", automationId: "BasicCarousel");
        await Driver.WaitForProperty("CarouselPositionLabel", "Text", "Position: 0");

        try
        {
            await Driver.SetProperty("CurrentPosition", "5", automationId: "BasicCarousel");
            await Driver.WaitForProperty("CarouselPositionLabel", "Text", "Position: 5");

            // The native scroll it triggers must not report a different position back.
            await Task.Delay(1500);
            await Assert.That(await Driver.GetProperty("CurrentPosition", automationId: "BasicCarousel")).IsEqualTo("5");
            await Assert.That(await Driver.GetProperty("Text", automationId: "CarouselPositionLabel")).IsEqualTo("Position: 5");
        }
        finally
        {
            await Driver.SetProperty("CurrentPosition", "0", automationId: "BasicCarousel");
        }
        await AssertNoAppErrors();
    }


    // ── Android: native layout and real finger input ───────────────────────

    /// <summary>The item frames of the RecyclerView whose bounds match <paramref name="automationId"/>.</summary>
    async Task<List<(int Width, int Height)>> NativeItems(string automationId)
    {
        await Driver.Scroll(automationId);
        await Task.Delay(800);
        var host = (AndroidAppHost)App.Host;
        var density = await host.Density();
        var b = (await Driver.Find(automationId)).WindowBounds!;

        static (int L, int T, int R, int B) Rect(System.Xml.Linq.XElement e)
        {
            var n = System.Text.RegularExpressions.Regex.Matches((string)e.Attribute("bounds")!, @"\d+").Select(m => Int32.Parse(m.Value)).ToArray();
            return (n[0], n[1], n[2], n[3]);
        }

        var recycler = (await host.NativeHierarchy())
            .Descendants("node")
            .Where(e => ((string?)e.Attribute("class"))?.EndsWith("RecyclerView") == true)
            .First(e =>
            {
                var r = Rect(e);
                return Math.Abs(r.T - b.Y * density) < 8 * density && Math.Abs((r.B - r.T) - b.Height * density) < 8 * density;
            });

        return recycler.Elements("node").Select(Rect).Select(r => (r.R - r.L, r.B - r.T)).ToList();
    }


    [Test]
    public async Task Items_Are_ItemWidth_By_ItemHeight()
    {
        // The regression: the shared view holder sized each item to its content after the carousel had
        // set ItemWidth x ItemHeight, so the 280x160 cards came out as ~48x70 tiles huddled together.
        if (!HasRealTouch)
            Skip.Test("reads the native RecyclerView through uiautomator (Android)");

        await OpenPage();
        var density = await ((AndroidAppHost)App.Host).Density();
        var items = await NativeItems("BasicCarousel");

        await Assert.That(items.Count).IsGreaterThan(0);
        // the centred item is at full scale; the others are scaled down by UnfocusedItemScale (0.85)
        await Assert.That(items.Max(i => i.Width)).IsGreaterThanOrEqualTo((int)(280 * density * 0.97));
        await Assert.That(items.Max(i => i.Height)).IsGreaterThanOrEqualTo((int)(160 * density * 0.97));
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Real_Swipe_Moves_To_The_Next_Item()
    {
        if (!HasRealTouch)
            Skip.Test("needs real touch input (Android)");

        await OpenPage();
        await Driver.SetProperty("CurrentPosition", "0", automationId: "BasicCarousel");
        await Driver.WaitForProperty("CarouselPositionLabel", "Text", "Position: 0");
        await Task.Delay(800);

        await TouchSwipe("BasicCarousel", 0.8, 0.3, 300);
        await Driver.WaitUntil(
            async () => await Driver.GetProperty("Text", automationId: "CarouselPositionLabel") is { } t && t != "Position: 0",
            "a real swipe to move the carousel on", timeoutSeconds: 5);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Real_Tap_On_An_Item_Selects_It()
    {
        // Android never raised ItemSelected for a tapped item; iOS always did.
        if (!HasRealTouch)
            Skip.Test("needs real touch input (Android)");

        await OpenPage();
        await TouchTap("DestinationsCarousel", 0.5, 0.5);
        await Driver.WaitUntil(
            async () => (await Driver.GetProperty("Text", automationId: "CarouselStatusLabel"))?.StartsWith("Selected: ") == true,
            "tapping a destination to select it", timeoutSeconds: 5);
        await AssertNoAppErrors();
    }
}
