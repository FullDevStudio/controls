using Microsoft.AspNetCore.Components;
using Shiny.Blazor.Controls;
using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

/// <summary>
/// DragCompleted reports the value once, when the interaction is over — not every step of a drag.
/// The JS drag handler calls OnDragStart / OnDragUpdate / OnDragEnd, so the sliders are driven through those.
/// </summary>
public class SliderDragCompletedTests
{
    // ---------------------------------------------------------------------------------------------
    // Slider
    // ---------------------------------------------------------------------------------------------

    static (Slider Slider, List<double> Completed, List<double> Changed) BuildSlider(double value = 0)
    {
        var completed = new List<double>();
        var changed = new List<double>();
        var receiver = new object();
        var slider = new Slider
        {
            Minimum = 0,
            Maximum = 100,
            Value = value,
            DragCompleted = EventCallback.Factory.Create<double>(receiver, completed.Add),
            ValueChanged = EventCallback.Factory.Create<double>(receiver, changed.Add)
        };
        return (slider, completed, changed);
    }


    [Fact]
    public async Task ADragRaisesItOnceWithTheFinalValue()
    {
        var (slider, completed, changed) = BuildSlider();

        slider.OnDragStart();
        await slider.OnDragUpdate(0.2);
        await slider.OnDragUpdate(0.4);
        await slider.OnDragUpdate(0.6);
        completed.ShouldBeEmpty();

        await slider.OnDragEnd();

        changed.Count.ShouldBe(3);
        completed.ShouldBe([60d]);
    }


    [Fact]
    public async Task ADragThatEndsWhereItStartedIsSilent()
    {
        var (slider, completed, _) = BuildSlider(50);

        slider.OnDragStart();
        await slider.OnDragUpdate(0.8);
        await slider.OnDragUpdate(0.5);
        await slider.OnDragEnd();

        completed.ShouldBeEmpty();
    }


    [Fact]
    public async Task AClickRaisesItStraightAway()
    {
        var (slider, completed, _) = BuildSlider();

        await slider.ClickAtPercent(0.3);
        await slider.ClickAtPercent(0.3);

        completed.ShouldBe([30d]);
    }


    [Fact]
    public async Task AReleaseWithoutAPressIsIgnored()
    {
        // The pointerup that ends a drag is followed by the track's click; neither may report twice.
        var (slider, completed, _) = BuildSlider();

        slider.OnDragStart();
        await slider.OnDragUpdate(0.4);
        await slider.OnDragEnd();
        await slider.OnDragEnd();
        await slider.ClickAtPercent(0.4);

        completed.ShouldBe([40d]);
    }


    // ---------------------------------------------------------------------------------------------
    // RangeSlider
    // ---------------------------------------------------------------------------------------------

    // ApplyPercentToThumb re-renders, which an unattached component cannot do, so the drag here moves
    // the bound values directly between the press and the release — which is all OnDragEnd reads.
    static (RangeSlider Slider, List<(double Lower, double Upper)> Completed) BuildRange()
    {
        var completed = new List<(double Lower, double Upper)>();
        var slider = new RangeSlider
        {
            Minimum = 0,
            Maximum = 100,
            LowerValue = 10,
            UpperValue = 90,
            DragCompleted = EventCallback.Factory.Create<(double Lower, double Upper)>(new object(), completed.Add)
        };
        return (slider, completed);
    }


    [Fact]
    public async Task ARangeDragRaisesItOnceWithTheFinalRange()
    {
        var (slider, completed) = BuildRange();

        slider.OnDragStart();
        slider.UpperValue = 70;
        await slider.OnDragEnd();
        await slider.OnDragEnd();

        completed.ShouldBe([(10d, 70d)]);
    }


    [Fact]
    public async Task ARangeDragThatEndsWhereItStartedIsSilent()
    {
        var (slider, completed) = BuildRange();

        slider.OnDragStart();
        slider.LowerValue = 40;
        slider.LowerValue = 10;
        await slider.OnDragEnd();

        completed.ShouldBeEmpty();
    }
}
