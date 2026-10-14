namespace Sample.UITests.Features;

[SkipOn(UIPlatform.Windows, KnownGaps.WinUiAnimationBlocksCapture)]
public class SkeletonTests : UITest
{
    Task OpenPage() => Open("skeleton", "SkeletonPage");


    [Test]
    public async Task Reload_Shows_The_Skeleton_Then_The_Content()
    {
        await OpenPage();

        await Driver.Tap("ReloadButton");
        await Driver.WaitForProperty("ArticleSkeleton", "IsBusy", "True", 3);

        // Content's parent is the skeleton's real-content host, hidden while the placeholder shows
        await Driver.WaitForProperty("ArticleSkeleton", "Content.Parent.IsVisible", "False", 3);

        // the view model holds it busy for 2.5s
        await Driver.WaitForProperty("ArticleSkeleton", "IsBusy", "False", 10);
        await Driver.WaitForProperty("ArticleSkeleton", "Content.Parent.IsVisible", "True", 3);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Card_Reload_Is_Independent()
    {
        await OpenPage();

        // settle the article first so the card is the only thing loading
        await Driver.Tap("ReloadButton");
        await Driver.WaitForProperty("ArticleSkeleton", "IsBusy", "False", 10);

        await Driver.Tap("ReloadCardButton");
        await Driver.WaitForProperty("CardSkeleton", "IsBusy", "True", 3);
        await Driver.WaitForProperty("ArticleSkeleton", "IsBusy", "False", 1);

        await Driver.WaitForProperty("CardSkeleton", "Content.Parent.IsVisible", "False", 3);
        await Driver.WaitForProperty("ArticleSkeleton", "Content.Parent.IsVisible", "True", 1);

        await Driver.WaitForProperty("CardSkeleton", "IsBusy", "False", 10);
        await Driver.WaitForProperty("CardSkeleton", "Content.Parent.IsVisible", "True", 3);
        await AssertNoAppErrors();
    }
}
