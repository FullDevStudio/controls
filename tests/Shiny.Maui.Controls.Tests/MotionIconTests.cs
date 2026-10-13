using Microsoft.Maui.Graphics;
using Shiny.Controls.Keyframe;
using Shiny.Controls.Keyframe.Graphics;
using Shiny.Controls.MotionIcons;
using Shiny.Maui.Controls;
using Shiny.Maui.Controls.MotionIcons;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// Covers the MAUI half of motion icons: the artwork's integrity against MAUI's path parser, the
/// scene the keyframe engine draws, and the property surface that decides what gets drawn.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class MotionIconTests
{
    public MotionIconTests()
    {
        // A fresh Application, so an implicit MotionIconView style another test installed (one sets
        // IsPlaying) cannot reach the views built here.
        _ = new Application();
    }

    /// <summary>Records what a scene asks for, so drawing can be asserted without a real surface.</summary>
    sealed class RecordingCanvas : ICanvas
    {
        public int StrokedPaths { get; private set; }
        public int FilledPaths { get; private set; }
        public List<float> AlphaValues { get; } = [];

        public float Alpha { set => AlphaValues.Add(value); }

        public void DrawPath(PathF path) => StrokedPaths++;
        public void FillPath(PathF path, WindingMode windingMode) => FilledPaths++;

        // --- Everything below is inert plumbing the interface requires. ---
        public Color FillColor { set { } }
        public Color StrokeColor { set { } }
        public Color FontColor { set { } }
        public IFont Font { set { } }
        public float FontSize { set { } }
        public float StrokeSize { set { } }
        public float MiterLimit { set { } }
        public LineCap StrokeLineCap { set { } }
        public LineJoin StrokeLineJoin { set { } }
        public float[]? StrokeDashPattern { set { } }
        public float StrokeDashOffset { set { } }
        public BlendMode BlendMode { set { } }
        public bool Antialias { set { } }
        public float DisplayScale { get; set; } = 1f;

        public void Translate(float tx, float ty) { }
        public void Scale(float sx, float sy) { }
        public void Rotate(float degrees) { }
        public void Rotate(float degrees, float x, float y) { }
        public void SaveState() { }
        public bool RestoreState() => true;
        public void ResetState() { }
        public void ClipPath(PathF path, WindingMode windingMode = WindingMode.NonZero) { }
        public void ClipRectangle(float x, float y, float width, float height) { }
        public void ConcatenateTransform(System.Numerics.Matrix3x2 transform) { }
        public void DrawArc(float x, float y, float w, float h, float a1, float a2, bool clockwise, bool closed) { }
        public void DrawEllipse(float x, float y, float width, float height) { }
        public void DrawImage(Microsoft.Maui.Graphics.IImage image, float x, float y, float width, float height) { }
        public void DrawLine(float x1, float y1, float x2, float y2) { }
        public void DrawRectangle(float x, float y, float width, float height) { }
        public void DrawRoundedRectangle(float x, float y, float width, float height, float cornerRadius) { }
        public void DrawString(string value, float x, float y, HorizontalAlignment alignment) { }
        public void DrawString(string value, float x, float y, float width, float height,
            HorizontalAlignment h, VerticalAlignment v, TextFlow f = TextFlow.ClipBounds, float lineSpacing = 0) { }
        public void DrawText(Microsoft.Maui.Graphics.Text.IAttributedText value, float x, float y, float width, float height) { }
        public SizeF GetStringSize(string value, IFont font, float fontSize) => SizeF.Zero;
        public SizeF GetStringSize(string value, IFont font, float fontSize, HorizontalAlignment h, VerticalAlignment v) => SizeF.Zero;
        public void FillArc(float x, float y, float w, float h, float a1, float a2, bool clockwise) { }
        public void FillEllipse(float x, float y, float width, float height) { }
        public void FillRectangle(float x, float y, float width, float height) { }
        public void FillRoundedRectangle(float x, float y, float width, float height, float cornerRadius) { }
        public void SetFillPaint(Paint paint, RectF rectangle) { }
        public void SetShadow(SizeF offset, float blur, Color color) { }
        public void SubtractFromClip(float x, float y, float width, float height) { }
    }

    /// <summary>Compiles an icon the same way the view does, so the tests exercise the real path.</summary>
    static Rendered Build(string icon, MotionPreset preset = MotionPreset.Default)
    {
        var definition = MotionIconLibrary.Get(icon);
        var spec = MotionResolver.ResolveMotion(definition, preset)!;
        var scene = MotionSceneBuilder.BuildScene(definition, spec, Colors.Black, null, 2f);

        return new Rendered(
            scene,
            MotionSceneBuilder.BuildTimeline(definition, spec, scene, Colors.Black, 2f, double.PositiveInfinity)!,
            spec.Duration);
    }

    sealed record Rendered(KeyframeScene Scene, Timeline Timeline, TimeSpan Duration)
    {
        public void Seek(double progress) => Timeline.Evaluate(Duration * progress);

        public void Draw(ICanvas canvas) => Scene.Draw(canvas, new RectF(0f, 0f, 48f, 48f));
    }



    [Fact]
    public void ViewDefaultsToTwentyFourPointsAndHoverOrPress()
    {
        var view = new MotionIconView();

        view.WidthRequest.ShouldBe(24d);
        view.HeightRequest.ShouldBe(24d);
        view.StrokeWidth.ShouldBe(2d);
        view.Motion.ShouldBe(MotionPreset.Default);
        view.Trigger.ShouldBe(MotionTrigger.Hover | MotionTrigger.Press);
        view.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void ViewAttachesAPointerRecogniserOnlyWhenHoverIsWanted()
    {
        var view = new MotionIconView { Trigger = MotionTrigger.Loop };

        view.GestureRecognizers.OfType<PointerGestureRecognizer>().ShouldBeEmpty();

        view.Trigger = MotionTrigger.Hover;
        view.GestureRecognizers.OfType<PointerGestureRecognizer>().Count().ShouldBe(1);
    }

    [Fact]
    public void ViewAttachesATapRecogniserForPressOrForACommand()
    {
        new MotionIconView { Trigger = MotionTrigger.Loop }
            .GestureRecognizers.OfType<TapGestureRecognizer>().ShouldBeEmpty();

        new MotionIconView { Trigger = MotionTrigger.Press }
            .GestureRecognizers.OfType<TapGestureRecognizer>().Count().ShouldBe(1);
    }

    [Fact]
    public void UnknownIconNamesDoNotThrow()
    {
        var view = new MotionIconView { Icon = "definitely-not-an-icon" };

        view.Icon.ShouldBe("definitely-not-an-icon");
        view.IsPlaying.ShouldBeFalse();
    }

    [Fact]
    public void MalformedPathDataDoesNotThrow()
    {
        // Artwork can arrive from a caller at runtime; a bad path should leave a hole in the layout,
        // not take down the page it happens to be on.
        var view = new MotionIconView { PathData = "not a path at all" };

        view.PathData.ShouldBe("not a path at all");
    }

    [Fact]
    public void NoIconReliesOnSvgsImplicitLineTo()
    {
        // SVG says a bare pair of coordinates after a moveto is a *lineto*: "M6 6 18 18" is a
        // diagonal line. Browsers do that. Microsoft.Maui.Graphics does not — its parser reads each
        // pair as another moveto, so the same string becomes two moves and draws nothing at all.
        //
        // Nothing warns about this. The web rendered the whole icon set perfectly while MAUI was
        // quietly drawing sixteen of the paths as bare dots, and it only surfaced by putting the two
        // side by side. Back-to-back moves are that bug's fingerprint, so the artwork is checked for
        // them here rather than trusted to review.
        foreach (var icon in MotionIconLibrary.All)
        {
            foreach (var part in icon.Parts)
            {
                var path = new PathBuilder().BuildPath(part.Path);
                var operations = path.SegmentTypes.ToList();

                for (var i = 1; i < operations.Count; i++)
                {
                    (operations[i] is PathOperation.Move && operations[i - 1] is PathOperation.Move)
                        .ShouldBeFalse(
                            $"{icon.Name}/{part.Id} has an implicit lineto that MAUI drops — write it as an " +
                            $"explicit L: {part.Path}");
                }
            }
        }
    }

    [Fact]
    public void NoPathIsTruncatedByTheParser()
    {
        // The other way MAUI's path parser differs from a browser: it cannot read SVG's
        // run-together decimals, so "l.06.06" stops it dead and the rest of the path is silently
        // dropped. That is how the settings cog spent a while rendering as a single tiny arc while
        // looking perfect on the web.
        //
        // Every command letter has to yield at least one operation — arcs yield several — so the
        // operation count can never legitimately fall below the number of commands. Anything less
        // means the parser gave up part way through.
        foreach (var icon in MotionIconLibrary.All)
        {
            foreach (var part in icon.Parts)
            {
                var commands = part.Path.Count(c => "MmLlHhVvCcSsQqTtAaZz".Contains(c, StringComparison.Ordinal));
                var operations = new PathBuilder().BuildPath(part.Path).OperationCount;

                operations.ShouldBeGreaterThanOrEqualTo(commands,
                    $"{icon.Name}/{part.Id}: {commands} commands produced only {operations} operations — " +
                    $"the parser stopped early: {part.Path}");
            }
        }
    }

    [Fact]
    public void EveryIconActuallyCoversItsBox()
    {
        // A path that parses but collapses to a point still "renders" without throwing. Requiring
        // each part to span a sensible part of the 24-unit box catches artwork that silently
        // degenerated, which is what the dropped linetos did.
        foreach (var icon in MotionIconLibrary.All)
        {
            foreach (var part in icon.Parts)
            {
                var bounds = new PathBuilder().BuildPath(part.Path).Bounds;

                Math.Max(bounds.Width, bounds.Height)
                    .ShouldBeGreaterThan(1.5f, $"{icon.Name}/{part.Id} collapsed to {bounds}");
            }
        }
    }

    [Fact]
    public void EveryBuiltInIconRendersAtEveryPointInItsCycle()
    {
        var canvas = new RecordingCanvas();

        foreach (var icon in MotionIconLibrary.All)
        {
            var rendered = Build(icon.Name);

            // Walk the whole cycle rather than sampling one pose: the step-end teleports and the
            // trims only misbehave part way through.
            for (var i = 0; i <= 20; i++)
            {
                var progress = i / 20d;

                Should.NotThrow(() =>
                {
                    rendered.Seek(progress);
                    rendered.Draw(canvas);
                }, $"{icon.Name} failed at {progress:P0}");
            }
        }
    }

    [Fact]
    public void EveryPresetRendersOnEveryIcon()
    {
        var canvas = new RecordingCanvas();

        foreach (var preset in Enum.GetValues<MotionPreset>())
        {
            if (preset is MotionPreset.None)
                continue;

            foreach (var icon in MotionIconLibrary.All)
            {
                var rendered = Build(icon.Name, preset);

                for (var i = 0; i <= 8; i++)
                {
                    var progress = i / 8d;

                    Should.NotThrow(() =>
                    {
                        rendered.Seek(progress);
                        rendered.Draw(canvas);
                    }, $"{icon.Name} + {preset} failed at {progress:P0}");
                }
            }
        }
    }

    [Fact]
    public void AtRestAnIconDrawsAllOfItsStrokes()
    {
        var canvas = new RecordingCanvas();

        // Never seeked: the scene is the artwork exactly as drawn.
        Build("check").Draw(canvas);

        canvas.StrokedPaths.ShouldBe(1);
    }

    [Fact]
    public void ADrawOnStartsWithNothingOnTheCanvas()
    {
        // The check is fully trimmed away at the start of its cycle. Trimming to nothing has to
        // produce no stroke at all — a zero-length path drawn with a round cap would otherwise
        // leave a visible dot sitting where the tick begins.
        var canvas = new RecordingCanvas();
        var rendered = Build("check");

        rendered.Seek(0d);
        rendered.Draw(canvas);

        canvas.StrokedPaths.ShouldBe(0);
    }

    [Fact]
    public void FullyFadedPartsAreSkippedEntirely()
    {
        // The middle bar of the hamburger is invisible in the middle of the morph. Skipping it
        // rather than drawing it transparent is what keeps a screen full of icons cheap.
        var canvas = new RecordingCanvas();
        var rendered = Build("menu");

        rendered.Seek(0.5d);
        rendered.Draw(canvas);

        canvas.StrokedPaths.ShouldBe(2);
    }

    [Fact]
    public void FilledPartsAreFilledAndStrokedPartsAreStroked()
    {
        // The typing indicator is a stroked bubble plus three filled dots.
        var canvas = new RecordingCanvas();
        Build("message").Draw(canvas);

        canvas.StrokedPaths.ShouldBe(1);
        canvas.FilledPaths.ShouldBe(3);
    }

    [Fact]
    public void SeekingIsAPureFunctionOfProgress()
    {
        // Nothing reads the previous frame, so arriving at 0.5 from 0.9 has to look identical to
        // arriving at it from 0.1. This is what lets the view scrub and reverse for free.
        var forwards = new RecordingCanvas();
        var backwards = new RecordingCanvas();

        var a = Build("bell");
        a.Seek(0.1d);
        a.Seek(0.5d);
        a.Draw(forwards);

        var b = Build("bell");
        b.Seek(0.9d);
        b.Seek(0.5d);
        b.Draw(backwards);

        backwards.AlphaValues.ShouldBe(forwards.AlphaValues);
        backwards.StrokedPaths.ShouldBe(forwards.StrokedPaths);
    }
}
