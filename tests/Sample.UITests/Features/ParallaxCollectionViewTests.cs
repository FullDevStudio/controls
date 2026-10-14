namespace Sample.UITests.Features;

public class ParallaxCollectionViewTests : UITest
{
    Task OpenPage() => Open("parallaxcollectionview", "ParallaxCollectionViewPage");


    [Test]
    public async Task Factor_Button_Cycles_The_Parallax_Factor()
    {
        await OpenPage();
        await Driver.WaitForProperty("ParallaxList", "ParallaxFactor", "0.5");

        await Driver.Tap("ParallaxFactorButton");
        await Driver.WaitForProperty("ParallaxList", "ParallaxFactor", "0.8");
        await Driver.WaitForProperty("ParallaxStatusLabel", "Text", "Parallax factor: 0.8");

        await Driver.Tap("ParallaxFactorButton");
        await Driver.WaitForProperty("ParallaxList", "ParallaxFactor", "0.2");

        await Driver.Tap("ParallaxFactorButton");
        await Driver.WaitForProperty("ParallaxList", "ParallaxFactor", "0.5");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Collapse_And_Fade_Toggles_Reach_The_Control()
    {
        await OpenPage();
        await Driver.WaitForProperty("ParallaxList", "CollapseToSticky", "False");
        await Driver.WaitForProperty("ParallaxList", "FadeHeaderOnScroll", "False");

        await Driver.Tap("ParallaxCollapseButton");
        await Driver.WaitForProperty("ParallaxList", "CollapseToSticky", "True");

        await Driver.Tap("ParallaxFadeButton");
        await Driver.WaitForProperty("ParallaxList", "FadeHeaderOnScroll", "True");

        await Driver.Tap("ParallaxCollapseButton");
        await Driver.WaitForProperty("ParallaxList", "CollapseToSticky", "False");
        await AssertNoAppErrors();
    }


    // devflow on the macOS head: scrolling the list (item index 10, or a -400 delta) moves the item rows
    // (Tuscan Hills from off-screen to y=-64) but the hero's TranslationY stays 0 - CollectionView.Scrolled,
    // which ParallaxCollectionView translates the hero from, is never raised there.
    [Test, SkipOn(UIPlatform.MacOS, "BUG: AppKit CollectionView raises no Scrolled event, so ParallaxCollectionView never translates the hero")]
    public async Task Scrolling_The_List_Moves_The_Hero_Up()
    {
        await OpenPage();
        var before = (await Driver.WaitUntilTextExists("Destinations")).WindowBounds?.Y;
        await Assert.That(before).IsNotNull();

        var list = await Driver.FindByType("CollectionView");
        await Driver.Scroll(elementId: list.Id, itemIndex: 10);

        await Driver.WaitUntil(async () =>
        {
            var hero = (await Driver.Query(text: "Destinations")).FirstOrDefault();
            return hero?.WindowBounds?.Y is { } y && y < before!.Value - 20;
        }, "the hero title to translate upwards with the scroll", timeoutSeconds: 10);
        await AssertNoAppErrors();
    }
}
