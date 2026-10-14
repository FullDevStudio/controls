namespace Sample.UITests.Features;

/// <summary>
/// Shiny Slider. The thumb is driven by a pan gesture DevFlow cannot synthesise, so these set
/// <c>Value</c> (the bindable the drag writes) and assert what the page does with it. Snapping and
/// DragCompleted only run for real input; the Blazor suite covers those with a mouse.
/// </summary>
public class SliderTests : UITest
{
    Task OpenSlider() => Open("slider", "SliderPage");

    async Task<string> WaitForText(string automationId, Func<string, bool> condition, string description)
    {
        string? text = null;
        await Driver.WaitUntil(async () =>
        {
            text = await Driver.GetProperty("Text", automationId: automationId);
            return text != null && condition(text);
        }, $"{automationId}.Text {description} (last: '{text}')");
        return text!;
    }

    async Task<bool> MarkLabelVisible(string text)
        => (await Driver.Query(text: text)).Any(x => x.IsVisible && x.Text == text);


    [Test]
    public async Task Value_Flows_Through_The_Binding_To_The_Label()
    {
        await OpenSlider();

        await Driver.SetProperty("Value", "73", automationId: "TemperatureSlider");

        await Driver.WaitForProperty("TemperatureSlider", "Value", "73");
        await WaitForText("TemperatureLabel", t => t.StartsWith("73"), "starts with 73");

        await Driver.SetProperty("Value", "12", automationId: "TemperatureSlider");
        await WaitForText("TemperatureLabel", t => t.StartsWith("12"), "starts with 12");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Fractional_Values_Keep_Their_Precision()
    {
        await OpenSlider();

        await Driver.SetProperty("Value", "7.5", automationId: "IntensitySlider");

        await WaitForText("IntensityLabel", t => t == "Intensity: 7.5", "is 'Intensity: 7.5'");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Bounds_Are_Read_Back_As_Declared()
    {
        await OpenSlider();

        // 0..3 stop-point slider and the 0..60 reference-tick slider, as declared in the page.
        await Assert.That(await Driver.GetDouble("Maximum", "QualitySlider")).IsEqualTo(3d);
        await Assert.That(await Driver.GetProperty("SnapToMarks", automationId: "QualitySlider")).IsEqualTo("True");
        await Assert.That(await Driver.GetDouble("Maximum", "PressureSlider")).IsEqualTo(60d);
        await Assert.That(await Driver.GetProperty("SnapToMarks", automationId: "PressureSlider")).IsEqualTo("False");

        await Driver.SetProperty("Value", "1", automationId: "QualitySlider");
        await WaitForText("QualityLabel", t => t == "Quality: 1", "is 'Quality: 1'");

        await Driver.SetProperty("Value", "18", automationId: "PressureSlider");
        await WaitForText("PressureLabel", t => t == "Pressure: 18 psi", "is 'Pressure: 18 psi'");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Programmatic_Value_Does_Not_Raise_DragCompleted()
    {
        await OpenSlider();
        var committedBefore = await Driver.GetProperty("Text", automationId: "SetpointCommitted");

        var current = await Driver.GetDouble("Value", "SetpointSlider") ?? 40;
        var next = current >= 90 ? 25 : current + 10;
        await Driver.SetProperty("Value", next.ToString(System.Globalization.CultureInfo.InvariantCulture), automationId: "SetpointSlider");

        // The live binding follows...
        await WaitForText("SetpointLiveLabel", t => t == $"Live: {next:0}", $"is 'Live: {next:0}'");

        // ...but a value set from code is not a user letting go of the thumb, so nothing commits.
        await Task.Delay(500);
        await Assert.That(await Driver.GetProperty("Text", automationId: "SetpointCommitted")).IsEqualTo(committedBefore);
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Custom_Thumb_Template_And_Label_Follow_The_Value()
    {
        await OpenSlider();

        await Driver.SetProperty("Value", "25", automationId: "BrightnessSlider");

        await WaitForText("BrightnessLabel", t => t == "Brightness: 25%", "is 'Brightness: 25%'");
        // The thumb template binds to the slider's own value, so a "25%" label rides in the thumb.
        await Driver.WaitUntil(async () => (await Driver.Query(text: "25%")).Any(x => x.Text == "25%"), "a '25%' thumb label");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Vertical_Slider_Is_Vertical_And_Binds()
    {
        await OpenSlider();

        await Assert.That(await Driver.GetProperty("Orientation", automationId: "BassSlider")).IsEqualTo("Vertical");

        await Driver.SetProperty("Value", "9", automationId: "BassSlider");
        await WaitForText("BassLabel", t => t == "Bass: 9", "is 'Bass: 9'");
        await AssertNoAppErrors();
    }


    [Test]
    [SkipOn(UIPlatform.MacOS, "AppKit never realises views added after the first layout, so new mark labels do not appear")]
    public async Task Bound_MarksSource_Adds_And_Clears_Marks()
    {
        await OpenSlider();

        await Driver.Tap(automationId: "ClearBookmarksButton");
        await Driver.WaitUntil(async () => !await MarkLabelVisible("Intro") && !await MarkLabelVisible("Chorus"), "the Intro/Chorus marks to be removed");

        // "Bookmark here" adds a mark at the slider's current position, labelled by count.
        await Driver.SetProperty("Value", "70", automationId: "BookmarkSlider");
        await Driver.Tap(automationId: "AddBookmarkButton");
        await Driver.WaitUntil(() => MarkLabelVisible("#1"), "a '#1' mark label");

        await Driver.Tap(automationId: "ClearBookmarksButton");
        await Driver.WaitUntil(async () => !await MarkLabelVisible("#1"), "the '#1' mark to be removed");
        await AssertNoAppErrors();
    }
}


/// <summary>Shiny RangeSlider — the same seam: set LowerValue/UpperValue and assert the bindings.</summary>
public class RangeSliderTests : UITest
{
    Task OpenRange() => Open("rangeslider", "RangeSliderPage");

    async Task WaitForText(string automationId, Func<string, bool> condition, string description)
    {
        string? text = null;
        await Driver.WaitUntil(async () =>
        {
            text = await Driver.GetProperty("Text", automationId: automationId);
            return text != null && condition(text);
        }, $"{automationId}.Text {description} (last: '{text}')");
    }


    [Test]
    public async Task Both_Thumbs_Bind_Independently()
    {
        await OpenRange();

        await Driver.SetProperty("LowerValue", "300", automationId: "PriceRange");
        await Driver.SetProperty("UpperValue", "900", automationId: "PriceRange");

        await Driver.WaitForProperty("PriceRange", "LowerValue", "300");
        await Driver.WaitForProperty("PriceRange", "UpperValue", "900");
        // Currency-formatted ("$300 — " on an en-US device); assert the number, not the symbol.
        await WaitForText("PriceLabel", t => t.Contains("300"), "contains 300");
        await WaitForText("PriceUpperLabel", t => t.Contains("900"), "contains 900");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Per_End_Thumb_Templates_Follow_Their_Values()
    {
        await OpenRange();

        await Driver.SetProperty("LowerValue", "6", automationId: "ShiftRange");
        await Driver.SetProperty("UpperValue", "18", automationId: "ShiftRange");

        await WaitForText("ShiftStartLabel", t => t == "Shift: 06:00 –", "is 'Shift: 06:00 –'");
        await WaitForText("ShiftEndLabel", t => t == "18:00", "is '18:00'");
        // Each thumb renders its own template with its own value.
        await Driver.WaitUntil(async () => (await Driver.Query(text: "06:00")).Any(x => x.Text == "06:00"), "a '06:00' thumb label");
        await AssertNoAppErrors();
    }


    [Test]
    public async Task Programmatic_Range_Does_Not_Raise_DragCompleted()
    {
        await OpenRange();
        var before = await Driver.GetProperty("Text", automationId: "SearchCommitted");

        var lower = await Driver.GetDouble("LowerValue", "SearchRange") ?? 100;
        var next = lower >= 300 ? 100 : lower + 50;
        await Driver.SetProperty("LowerValue", next.ToString(System.Globalization.CultureInfo.InvariantCulture), automationId: "SearchRange");
        await Driver.WaitForProperty("SearchRange", "LowerValue", next.ToString(System.Globalization.CultureInfo.InvariantCulture));

        await Task.Delay(500);
        await Assert.That(await Driver.GetProperty("Text", automationId: "SearchCommitted")).IsEqualTo(before);
        await AssertNoAppErrors();
    }
}
