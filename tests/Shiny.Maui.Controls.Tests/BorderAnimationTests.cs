using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>The border path geometry, where the beam is and what colour it is, and the shine's gradient.</summary>
[Collection(ApplicationResourcesCollection.Name)]
public class BorderAnimationTests
{
    public BorderAnimationTests()
    {
        TestDispatcherProvider.Install();
        _ = new Application();
    }


    // ---------------------------------------------------------------------------------------------
    // Path
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ASquareCorneredPathIsThePerimeterOfTheStrokeCentreLine()
    {
        // A 2-wide border's centre line sits 1 inside a 100 x 50 box: a 98 x 48 rectangle.
        var path = BorderPath.Create(100, 50, 0, 2);

        path.Length.ShouldBe(2 * (98 + 48), 0.01);
    }


    [Fact]
    public void RoundedCornersShortenTheLapByTheirArcs()
    {
        // Radius 10 at the outer edge is 9.5 on a 1-wide stroke's centre line.
        var path = BorderPath.Create(100, 60, 10, 1);
        var r = 9.5;
        var expected = (2 * (99 - (2 * r))) + (2 * (59 - (2 * r))) + (2 * Math.PI * r);

        // The corners are 5° chords, which run a hair short of the true arc.
        path.Length.ShouldBe(expected, 0.1);
    }


    [Fact]
    public void TheRadiusIsCappedAtAPill()
    {
        var path = BorderPath.Create(100, 20, 999, 0);

        path.Length.ShouldBe((2 * 80) + (Math.PI * 20), 0.1);
    }


    [Fact]
    public void TheLapStartsAfterTheTopLeftCornerAndRunsClockwise()
    {
        var path = BorderPath.Create(100, 50, 0, 0);

        path.PointAt(0).ShouldBe(new PointF(0, 0));
        path.PointAt(100).X.ShouldBe(100, 0.01f);   // along the top
        path.PointAt(125).Y.ShouldBe(25, 0.01f);    // halfway down the right
        path.PointAt(300).ShouldBe(new PointF(0, 0)); // a full lap wraps
        path.PointAt(-50).Y.ShouldBe(50, 0.01f);    // and so does a negative distance
    }


    [Fact]
    public void ABoxTooSmallForTheBorderHasNoPath()
    {
        BorderPath.Create(1, 40, 4, 2).Length.ShouldBe(0);
        BorderPath.Create(0, 0, 4, 1).SegmentCount.ShouldBe(0);
    }


    // ---------------------------------------------------------------------------------------------
    // Beam
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TheBeamMovesOneLapPerDuration()
    {
        var beam = new BorderBeam { Duration = TimeSpan.FromSeconds(4) };

        beam.Position.ShouldBe(0);
        beam.Advance(TimeSpan.FromSeconds(1));
        beam.Position.ShouldBe(0.25, 1e-9);
        beam.Advance(TimeSpan.FromSeconds(4));
        beam.Position.ShouldBe(0.25, 1e-9);
    }


    [Fact]
    public void DelayAndOffsetAreAHeadStart()
    {
        new BorderBeam { Duration = TimeSpan.FromSeconds(4), Delay = TimeSpan.FromSeconds(1) }.Position.ShouldBe(0.25, 1e-9);
        new BorderBeam { InitialOffset = 40 }.Position.ShouldBe(0.4, 1e-9);
    }


    [Fact]
    public void ReverseRunsTheOtherWayRound()
    {
        var beam = new BorderBeam { Duration = TimeSpan.FromSeconds(4), Reverse = true, InitialOffset = 10 };

        beam.Position.ShouldBe(0.9, 1e-9);
        beam.Advance(TimeSpan.FromSeconds(1));
        beam.Position.ShouldBe(0.65, 1e-9);
    }


    [Fact]
    public void TheBeamShadesFromHeadToAFadedTail()
    {
        var beam = new BorderBeam { ColorFrom = Colors.Red, ColorTo = Colors.Blue };

        beam.ColorAlong(0).ShouldBe(Colors.Red);
        beam.ColorAlong(0.5).ShouldBe(Colors.Blue);

        // The tail keeps the hue and loses only its alpha.
        var tail = beam.ColorAlong(1);
        tail.Alpha.ShouldBe(0);
        tail.Blue.ShouldBe(1);
    }


    // ---------------------------------------------------------------------------------------------
    // Shine
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void TheShineSweepsAcrossAndBack()
    {
        var shine = new ShineBorder { Duration = TimeSpan.FromSeconds(10) };

        shine.Sweep.ShouldBe(0);
        shine.Advance(TimeSpan.FromSeconds(2.5));
        shine.Sweep.ShouldBe(0.5, 1e-9);
        shine.Advance(TimeSpan.FromSeconds(2.5));
        shine.Sweep.ShouldBe(1, 1e-9);
        shine.Advance(TimeSpan.FromSeconds(2.5));
        shine.Sweep.ShouldBe(0.5, 1e-9);
    }


    [Fact]
    public void TheGradientIsTransparentAtBothEndsWithTheColoursBetween()
    {
        IReadOnlyList<Color> colors = [Colors.Red, Colors.Lime, Colors.Blue];

        // Seven evenly spaced stops: clear, clear, red, lime, blue, clear, clear.
        ShineBorder.GradientAt(colors, 0).Alpha.ShouldBe(0);
        ShineBorder.GradientAt(colors, 2 / 6d).ShouldBe(Colors.Red);
        ShineBorder.GradientAt(colors, 3 / 6d).ShouldBe(Colors.Lime);
        ShineBorder.GradientAt(colors, 4 / 6d).ShouldBe(Colors.Blue);
        ShineBorder.GradientAt(colors, 1).Alpha.ShouldBe(0);
        ShineBorder.GradientAt(colors, 5).Alpha.ShouldBe(0);
    }


    [Fact]
    public void AtRestTheFarCornerCarriesTheFirstColour()
    {
        var shine = new ShineBorder { ShineColors = [Colors.Red, Colors.Blue] };

        // At sweep 0 the gradient centre is off the bottom-right: that corner is a third of the way out.
        // With two colours, six stops: a third is between the first colour (0.4) and clear (0.2).
        shine.ColorAt(100, 100, 100, 100, 0).Red.ShouldBe(1);
        shine.ColorAt(0, 0, 100, 100, 0).Alpha.ShouldBe(0, 0.001f);
    }


    [Fact]
    public void ColoursAreReadFromACommaSeparatedList()
    {
        var colors = (Color[])new ColorListTypeConverter().ConvertFrom(null, null, "#FF0000, rgb(0,255,0) ,Blue");

        colors.Length.ShouldBe(3);
        colors[0].ShouldBe(Colors.Red);
        colors[1].ShouldBe(Colors.Lime);
        colors[2].ShouldBe(Colors.Blue);
    }


    // ---------------------------------------------------------------------------------------------
    // Layout & running
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ContentIsInsetByPaddingAndBorder()
    {
        var content = new FixedView();
        var beam = new BorderBeam { Content = content, Padding = new Thickness(10), BorderWidth = 2 };

        var size = ((Microsoft.Maui.ILayout)beam).CrossPlatformMeasure(500, 500);
        size.ShouldBe(new Size(74, 44));

        ((Microsoft.Maui.ILayout)beam).CrossPlatformArrange(new Rect(0, 0, 74, 44));
        content.Frame.ShouldBe(new Rect(12, 12, 50, 20));
    }


    [Fact]
    public void ContentSitsUnderTheCanvas()
    {
        var first = new Label();
        var beam = new BorderBeam { Content = first };
        beam.Children[0].ShouldBe(first);

        var second = new Label();
        beam.Content = second;
        beam.Children.ShouldNotContain(first);
        beam.Children[0].ShouldBe(second);
        beam.Children.Count.ShouldBe(2);
    }


    [Fact]
    public void AnExplicitTrackColourWinsOverTheTheme()
    {
        var beam = new BorderBeam();
        beam.ResolvedTrackColor.ShouldNotBe(Colors.Transparent);

        beam.TrackColor = Colors.Transparent;
        beam.ResolvedTrackColor.ShouldBe(Colors.Transparent);
    }


    [Fact]
    public void ItDoesNotTickUntilLoaded()
        => new ShineBorder().IsTicking.ShouldBeFalse();


    sealed class FixedView : View
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint) => new(50, 20);
    }
}
