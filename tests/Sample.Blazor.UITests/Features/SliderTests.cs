namespace Sample.Blazor.UITests.Features;

/// <summary>
/// The gradient Slider and RangeSlider. Neither exposes a keyboard or ARIA slider role, so these drive
/// the track with real mouse input: a click jumps the nearest thumb, a press on a thumb drags it.
/// </summary>
public class SliderTests : BlazorTest
{
    ILocator Section(string heading)
        => Page.Locator(".demo-section").Filter(new() { Has = Page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }) });

    /// <summary>Clicks a horizontal track at a fraction of its width.</summary>
    static async Task ClickTrackAt(ILocator track, double fraction)
    {
        var box = (await track.BoundingBoxAsync())!;
        await track.ClickAsync(new() { Position = new() { X = (float)(box.Width * fraction), Y = (float)(box.Height / 2) }, Force = true });
    }

    /// <summary>Presses a thumb and drags it, in steps, to a fraction of the track's width.</summary>
    async Task DragThumbTo(ILocator thumb, ILocator track, double fraction)
    {
        await thumb.ScrollIntoViewIfNeededAsync();
        var thumbBox = (await thumb.BoundingBoxAsync())!;
        var trackBox = (await track.BoundingBoxAsync())!;
        var y = thumbBox.Y + thumbBox.Height / 2;

        await Page.Mouse.MoveAsync(thumbBox.X + thumbBox.Width / 2, y);
        await Page.Mouse.DownAsync();
        await Page.Mouse.MoveAsync((float)(trackBox.X + trackBox.Width * fraction), y, new() { Steps = 12 });
        await Page.Mouse.UpAsync();
    }


    [Test]
    public async Task Clicking_The_Track_Moves_The_Value_And_Its_Binding()
    {
        await Open("slider");
        var section = Section("Basic (Temperature)");

        await ClickTrackAt(section.Locator(".shiny-gs-track"), 0.8);

        await Expect(section.Locator(".state code")).ToHaveTextAsync("80°");
        await Expect(section.Locator(".shiny-gs-tooltip-text")).ToHaveTextAsync("80°");
        await Expect(section.Locator(".shiny-gs-thumb")).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex(@"left:\s*80%"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Value_Snaps_To_Step()
    {
        await Open("slider");
        var section = Section("Custom Colors (Green to Yellow)");

        // 0..10 with a 0.5 step: 36% of the way is 3.6, which rounds to 3.5.
        await ClickTrackAt(section.Locator(".shiny-gs-track"), 0.36);

        await Expect(section.Locator(".state code")).ToHaveTextAsync("3.5");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Stop_Points_Snap_To_The_Nearest_Mark()
    {
        await Open("slider");
        var section = Section("Stop Points (snap to marks)");
        var track = section.Locator(".shiny-gs-track");

        // 0..3 with marks at every integer: 45% is 1.35, which comes to rest on "Good" (1).
        await ClickTrackAt(track, 0.45);
        await Expect(section.Locator(".state code")).ToHaveTextAsync("1");

        // 90% is 2.7 — rests on "Best" (3), never on 2.7.
        await ClickTrackAt(track, 0.9);
        await Expect(section.Locator(".state code")).ToHaveTextAsync("3");
        await Expect(section.Locator(".shiny-gs-thumb")).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex(@"left:\s*100%"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Reference_Ticks_Do_Not_Snap()
    {
        await Open("slider");
        var section = Section("Reference Ticks (no snapping)");

        // 0..60, marks at 15/32/50 but SnapToMarks=false: 30% is 18, not the Low mark at 15.
        await ClickTrackAt(section.Locator(".shiny-gs-track"), 0.3);

        await Expect(section.Locator(".state code")).ToHaveTextAsync("18 psi");
        await Expect(section.Locator(".shiny-gs-mark-label")).ToHaveTextAsync(["Low", "Target", "Max"]);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Dragging_Commits_Once_On_Release()
    {
        await Open("slider");
        var section = Section("Commit on Release (DragCompleted)");
        var state = section.Locator(".state");

        await DragThumbTo(section.Locator(".shiny-gs-thumb"), section.Locator(".shiny-gs-track"), 0.75);

        // Many value changes during the drag, exactly one commit when the thumb is let go.
        await Expect(state).ToContainTextAsync("(1 commits)");
        var live = await state.Locator("code").Nth(0).InnerTextAsync();
        var committed = await state.Locator("code").Nth(1).InnerTextAsync();
        await Assert.That(committed).IsEqualTo(live);
        await Assert.That(Int32.Parse(live)).IsBetween(70, 80);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task A_Track_Click_Is_A_Commit_Of_Its_Own()
    {
        await Open("slider");
        var section = Section("Commit on Release (DragCompleted)");

        await ClickTrackAt(section.Locator(".shiny-gs-track"), 0.1);

        await Expect(section.Locator(".state")).ToContainTextAsync("Committed: 10 (1 commits)");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Vertical_Slider_Runs_Bottom_To_Top()
    {
        await Open("slider");
        var section = Section("Vertical");
        var bass = section.Locator(".shiny-gs-track").First;

        var box = (await bass.BoundingBoxAsync())!;
        // 70% of the way up from the bottom of a 0..10 track is 7.
        await bass.ClickAsync(new() { Position = new() { X = (float)(box.Width / 2), Y = (float)(box.Height * 0.3) }, Force = true });

        await Expect(section.Locator(".state code").First).ToHaveTextAsync("7");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Custom_Thumb_Template_Carries_The_Value()
    {
        await Open("slider");
        var section = Section("Custom Thumb (the value rides in it)");

        await ClickTrackAt(section.Locator(".shiny-gs-track"), 0.25);

        await Expect(section.Locator(".shiny-gs-thumb")).ToHaveTextAsync("25%");
        await Expect(section.Locator(".state code")).ToHaveTextAsync("25%");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Disabled_Slider_Ignores_Clicks()
    {
        await Open("slider");
        var section = Section("Disabled");
        var thumb = section.Locator(".shiny-gs-thumb");

        await ClickTrackAt(section.Locator(".shiny-gs-track"), 0.1);
        await Page.WaitForTimeoutAsync(300);

        await Expect(thumb).ToHaveAttributeAsync("style", new System.Text.RegularExpressions.Regex(@"left:\s*65%"));
        await AssertNoConsoleErrors();
    }


    // ── RangeSlider ────────────────────────────────────────────────────────

    [Test]
    public async Task Range_Click_Moves_The_Nearer_Thumb()
    {
        await Open("rangeslider");
        var section = Section("Price Range");
        var track = section.Locator(".shiny-rs-track");

        // $250 – $750: a click at 10% is nearer the lower thumb.
        await ClickTrackAt(track, 0.1);
        await Expect(section.Locator(".state")).ToHaveTextAsync("Between $100 and $750");

        // ...and one at 90% is nearer the upper.
        await ClickTrackAt(track, 0.9);
        await Expect(section.Locator(".state")).ToHaveTextAsync("Between $100 and $900");
        await Expect(section.Locator(".shiny-rs-tooltip-text")).ToHaveTextAsync(["$100", "$900"]);
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Range_Drag_Commits_Once_On_Release()
    {
        await Open("rangeslider");
        var section = Section("Search on Release (DragCompleted)");

        await DragThumbTo(section.Locator(".shiny-rs-thumb[data-thumb='upper']"), section.Locator(".shiny-rs-track"), 0.8);

        await Expect(section.Locator(".state")).ToContainTextAsync("(1 searches)");
        await Expect(section.Locator(".state code")).ToHaveTextAsync(new System.Text.RegularExpressions.Regex(@"^\$100 – \$(7[5-9]0|8[0-4]0)$"));
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Range_Upper_Thumb_Cannot_Cross_The_Minimum_Gap()
    {
        await Open("rangeslider");
        var section = Section("Comfort Band (min 2°, max 8° apart)");

        // 10..30, currently 18–24 with a 2° minimum gap: dragging the upper thumb down to the
        // bottom of the track stops 2° above the lower thumb.
        await DragThumbTo(section.Locator(".shiny-rs-thumb[data-thumb='upper']"), section.Locator(".shiny-rs-track"), 0.0);

        await Expect(section.Locator(".state")).ToHaveTextAsync("From 18° to 20°");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Range_Maximum_Gap_Drags_The_Other_Thumb_Along()
    {
        await Open("rangeslider");
        var section = Section("Comfort Band (min 2°, max 8° apart)");

        // Upper to the top (30°) with an 8° maximum gap pulls the lower thumb up to 22°.
        await DragThumbTo(section.Locator(".shiny-rs-thumb[data-thumb='upper']"), section.Locator(".shiny-rs-track"), 1.0);

        await Expect(section.Locator(".state")).ToHaveTextAsync("From 22° to 30°");
        await AssertNoConsoleErrors();
    }


    [Test]
    public async Task Range_Per_End_Thumb_Templates_Follow_Their_Values()
    {
        await Open("rangeslider");
        var section = Section("Custom Thumbs (one template per end)");

        // 0..24: a click at 25% (6) is nearer the 09:00 start thumb than the 17:00 end.
        await ClickTrackAt(section.Locator(".shiny-rs-track"), 0.25);

        await Expect(section.Locator(".shiny-rs-thumb[data-thumb='lower']")).ToHaveTextAsync("06:00");
        await Expect(section.Locator(".shiny-rs-thumb[data-thumb='upper']")).ToHaveTextAsync("17:00");
        await Expect(section.Locator(".state")).ToHaveTextAsync("Shift: 06:00 – 17:00");
        await AssertNoConsoleErrors();
    }
}
