using System.Windows.Input;
using Microsoft.Maui.Controls;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// DragCompleted reports the value once, when the interaction is over — not every step of a drag.
/// The pan itself cannot be raised headlessly, so the drag is driven through the same seams it calls.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class SliderDragCompletedTests
{
    public SliderDragCompletedTests()
    {
        TestDispatcherProvider.Install();
        _ = new Application();
    }


    // ---------------------------------------------------------------------------------------------
    // Slider
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ADragRaisesItOnceWithTheFinalValue()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Step = 1 };
        var completed = new List<double>();
        var changed = 0;
        slider.DragCompleted += (_, v) => completed.Add(v);
        slider.ValueChangedEvent += (_, _) => changed++;

        slider.BeginDrag();
        slider.SetValueFromPercent(0.2);
        slider.SetValueFromPercent(0.4);
        slider.SetValueFromPercent(0.6);
        completed.ShouldBeEmpty();

        slider.EndDrag();

        changed.ShouldBe(3);
        completed.ShouldBe([60d]);
    }


    [Fact]
    public void TheCommandReceivesTheFinalValue()
    {
        object? parameter = null;
        var slider = new Slider { Minimum = 0, Maximum = 100, DragCompletedCommand = new Command(p => parameter = p) };

        slider.BeginDrag();
        slider.SetValueFromPercent(0.25);
        slider.EndDrag();

        parameter.ShouldBe(25d);
    }


    [Fact]
    public void ADragThatEndsWhereItStartedIsSilent()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100, Value = 50 };
        var raised = 0;
        slider.DragCompleted += (_, _) => raised++;

        slider.BeginDrag();
        slider.SetValueFromPercent(0.8);
        slider.SetValueFromPercent(0.5);
        slider.EndDrag();

        raised.ShouldBe(0);
    }


    [Fact]
    public void ATapRaisesItStraightAway()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100 };
        var completed = new List<double>();
        slider.DragCompleted += (_, v) => completed.Add(v);

        slider.TapAtPercent(0.3);
        slider.TapAtPercent(0.3);

        completed.ShouldBe([30d]);
    }


    [Fact]
    public void ASecondEndWithoutAStartIsIgnored()
    {
        var slider = new Slider { Minimum = 0, Maximum = 100 };
        var raised = 0;
        slider.DragCompleted += (_, _) => raised++;

        slider.BeginDrag();
        slider.SetValueFromPercent(0.4);
        slider.EndDrag();
        slider.EndDrag();

        raised.ShouldBe(1);
    }


    // ---------------------------------------------------------------------------------------------
    // RangeSlider
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ARangeDragRaisesItOnceWithTheFinalRange()
    {
        var slider = new RangeSlider { Minimum = 0, Maximum = 100, LowerValue = 10, UpperValue = 90 };
        var completed = new List<SliderRange>();
        var changed = 0;
        slider.DragCompleted += (_, r) => completed.Add(r);
        slider.RangeChanged += (_, _) => changed++;

        slider.BeginDrag();
        slider.ApplyPercentToThumb(0.6, isUpper: true);
        slider.ApplyPercentToThumb(0.7, isUpper: true);
        completed.ShouldBeEmpty();

        slider.EndDrag();

        changed.ShouldBe(2);
        completed.ShouldBe([new SliderRange(10, 70)]);
    }


    [Fact]
    public void TheRangeCommandReceivesTheFinalRange()
    {
        object? parameter = null;
        var slider = new RangeSlider
        {
            Minimum = 0, Maximum = 100, LowerValue = 10, UpperValue = 90,
            DragCompletedCommand = new Command(p => parameter = p)
        };

        slider.BeginDrag();
        slider.ApplyPercentToThumb(0.3, isUpper: false);
        slider.EndDrag();

        parameter.ShouldBe(new SliderRange(30, 90));
    }


    [Fact]
    public void ARangeDragThatEndsWhereItStartedIsSilent()
    {
        var slider = new RangeSlider { Minimum = 0, Maximum = 100, LowerValue = 10, UpperValue = 90 };
        var raised = 0;
        slider.DragCompleted += (_, _) => raised++;

        slider.BeginDrag();
        slider.ApplyPercentToThumb(0.5, isUpper: false);
        slider.ApplyPercentToThumb(0.1, isUpper: false);
        slider.EndDrag();

        raised.ShouldBe(0);
    }


    [Fact]
    public void ARangeTapRaisesItStraightAway()
    {
        var slider = new RangeSlider { Minimum = 0, Maximum = 100, LowerValue = 10, UpperValue = 90 };
        var completed = new List<SliderRange>();
        slider.DragCompleted += (_, r) => completed.Add(r);

        slider.TapAtPercent(0.8, isUpper: true);

        completed.ShouldBe([new SliderRange(10, 80)]);
    }
}
