using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// The bubble's card gets a fresh brush whenever its fill or outline colour changes, instead of the bound
/// brush changing colour underneath it - on Windows the card kept the first colour it was drawn with.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class TooltipBubbleFillTests
{
    public TooltipBubbleFillTests()
    {
        TestDispatcherProvider.Install();
        _ = new Application();
    }


    [Fact]
    public void ANewFillColourGivesTheCardANewBrush()
    {
        var bubble = new TooltipBubble();
        var before = bubble.CardFill;

        bubble.BubbleColor = Colors.Red;

        bubble.CardFill.ShouldNotBeSameAs(before);
        ((SolidColorBrush)bubble.CardFill).Color.ShouldBe(Colors.Red);
    }


    [Fact]
    public void EveryLaterChangeReplacesTheBrushAgain()
    {
        var bubble = new TooltipBubble { BubbleColor = Colors.Red };
        var red = bubble.CardFill;

        bubble.BubbleColor = Colors.Green;

        bubble.CardFill.ShouldNotBeSameAs(red);
        ((SolidColorBrush)bubble.CardFill).Color.ShouldBe(Colors.Green);
    }
}
