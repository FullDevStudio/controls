using System.Globalization;
using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

/// <summary>
/// What the border animations write to the stylesheet. The movement itself is CSS, so this is
/// everything the components decide.
/// </summary>
public class BorderAnimationTests
{
    [Fact]
    public void TheBeamLapRunsFromTheOffsetOnceRound()
        => new BorderBeam { InitialOffset = 25 }.Lap.ShouldBe(("25%", "125%"));


    [Fact]
    public void AReversedLapRunsBackwards()
        => new BorderBeam { InitialOffset = 25, Reverse = true }.Lap.ShouldBe(("75%", "-25%"));


    [Fact]
    public void TheStyleIsWrittenInvariantly()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            // A comma decimal separator would make every one of these custom properties invalid CSS.
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var style = new BorderBeam
            {
                CornerRadius = 12.5,
                BorderWidth = 1.5,
                Size = 60.5,
                Duration = TimeSpan.FromSeconds(6.5),
                Delay = TimeSpan.FromSeconds(1.25)
            }.RootStyle;

            style.ShouldContain("--shiny-border-radius:12.5px;");
            style.ShouldContain("--shiny-border-width:1.5px;");
            style.ShouldContain("--shiny-border-beam-size:60.5px;");
            style.ShouldContain("--shiny-border-beam-duration:6.5s;");
            style.ShouldContain("--shiny-border-beam-delay:1.25s;");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }


    [Fact]
    public void TheTrackFollowsTheThemeUnlessSet()
    {
        new BorderBeam().RootStyle.ShouldNotContain("--shiny-border-track");
        new ShineBorder { TrackColor = "transparent" }.RootStyle.ShouldContain("--shiny-border-track:transparent;");
    }


    [Fact]
    public void TheCallersStyleComesLastSoItWins()
        => new ShineBorder { Style = "padding:20px" }.RootStyle.ShouldEndWith("padding:20px");


    [Fact]
    public void TheShineGradientWrapsTheColoursInTransparency()
    {
        new ShineBorder { ShineColors = ["red", "blue"] }.Gradient
            .ShouldBe("radial-gradient(transparent,transparent,red,blue,transparent,transparent)");

        new ShineBorder().Gradient.ShouldContain("#A07CFE,#FE8FB5,#FFBE7B");
        new ShineBorder { ShineColors = [] }.Gradient.ShouldContain("#A07CFE");
    }


    [Fact]
    public void StateBecomesModifierClasses()
    {
        var classes = new BorderBeam { Overlay = true, IsRunning = false }.StateClasses;

        classes.ShouldContain("shiny-border-beam--overlay");
        classes.ShouldContain("shiny-border-beam--paused");
        classes.ShouldContain("shiny-border-beam--respect-motion");

        new ShineBorder { RespectReducedMotion = false }.StateClasses.ShouldBeEmpty();
    }
}
