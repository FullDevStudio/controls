using System.Reflection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>The animated background host, and the moving state behind each effect.</summary>
[Collection(ApplicationResourcesCollection.Name)]
public class AnimatedBackgroundTests
{
    public AnimatedBackgroundTests()
    {
        TestDispatcherProvider.Install();
        _ = new Application();
    }


    public static TheoryData<string> AllEffects => new(EffectsByName.Keys);

    static readonly Dictionary<string, Func<BackgroundEffect>> EffectsByName = new()
    {
        ["aurora"] = () => new AuroraEffect(),
        ["particles"] = () => new ParticlesEffect(),
        ["snow"] = () => new SnowEffect(),
        ["rain"] = () => new RainEffect(),
        ["meteors"] = () => new MeteorsEffect(),
        ["dots"] = () => new DotPatternEffect(),
        ["dots-pulse"] = () => new DotPatternEffect { Pulse = true },
        ["noise"] = () => new NoiseEffect(),
        ["starfield"] = () => new StarfieldEffect(),
        ["warp"] = () => new StarfieldEffect { Warp = true },
        ["fireflies"] = () => new FirefliesEffect(),
        ["bubbles"] = () => new BubblesEffect(),
        ["waves"] = () => new WavesEffect(),
        ["matrix"] = () => new MatrixEffect()
    };


    // ---------------------------------------------------------------------------------------------
    // Host
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void ItDoesNotTickUntilLoaded()
        => new AnimatedBackground { Effect = new SnowEffect() }.IsTicking.ShouldBeFalse();


    [Fact]
    public void ContentIsLaidOverTheCanvasInsideThePadding()
    {
        var content = new Fixed();
        var bg = new AnimatedBackground { Padding = new Thickness(10, 5), Content = content };

        // The canvas first, so the content paints on top of the effect.
        bg.Children.Count.ShouldBe(2);
        bg.Children[0].ShouldBeOfType<GraphicsView>();
        bg.Children[1].ShouldBe(content);

        var size = ((Microsoft.Maui.ILayout)bg).CrossPlatformMeasure(500, 500);
        size.ShouldBe(new Size(70, 30));

        ((Microsoft.Maui.ILayout)bg).CrossPlatformArrange(new Rect(0, 0, 200, 100));
        content.Frame.ShouldBe(new Rect(10, 5, 180, 90));
    }


    [Fact]
    public void ReplacingTheContentRemovesTheOldOne()
    {
        var first = new Fixed();
        var bg = new AnimatedBackground { Content = first };
        bg.Content = new Fixed();

        bg.Children.ShouldNotContain(first);
        bg.Children.Count.ShouldBe(2);
    }


    [Fact]
    public void SettingAnEffectGivesItARendererSizedToTheBox()
    {
        var bg = new AnimatedBackground { Random = new Random(1) };
        ((Microsoft.Maui.ILayout)bg).CrossPlatformArrange(new Rect(0, 0, 200, 100));

        bg.Effect = new SnowEffect { Density = 1 };

        var renderer = bg.Renderer.ShouldBeOfType<SnowEffect.Renderer>();
        renderer.Flakes.Count.ShouldBe(2);

        bg.Effect = null;
        bg.Renderer.ShouldBeNull();
    }


    [Fact]
    public void AdvanceMovesTheEffectByItsSpeed()
    {
        var bg = new AnimatedBackground { Random = new Random(1), Effect = new SnowEffect { Speed = 2 } };
        ((Microsoft.Maui.ILayout)bg).CrossPlatformArrange(new Rect(0, 0, 200, 200));

        bg.Advance(TimeSpan.FromMilliseconds(100));
        bg.Renderer!.Time.ShouldBe(0.2, 1e-9);

        bg.Effect!.Speed = 0;
        bg.Advance(TimeSpan.FromMilliseconds(100));
        bg.Renderer.Time.ShouldBe(0.2, 1e-9);
    }


    // ---------------------------------------------------------------------------------------------
    // Renderers
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void DensityIsPerHundredByHundredArea()
    {
        BackgroundEffect.CountFor(1.5, 200, 100).ShouldBe(3);
        BackgroundEffect.CountFor(0, 200, 100).ShouldBe(0);
        BackgroundEffect.CountFor(100, 1000, 1000, cap: 400).ShouldBe(400);
    }


    [Fact]
    public void AResizeTopsUpWithoutReseeding()
    {
        var renderer = (SnowEffect.Renderer)new SnowEffect { Density = 1 }.CreateRenderer(new Random(3));
        renderer.Resize(100, 100);
        var first = renderer.Flakes[0];

        renderer.Resize(100, 300);

        renderer.Flakes.Count.ShouldBe(3);
        renderer.Flakes[0].ShouldBeSameAs(first);
    }


    [Fact]
    public void ChangingTheDensityReconcilesTheCount()
    {
        var effect = new ParticlesEffect { Density = 1 };
        var bg = new AnimatedBackground { Random = new Random(1), Effect = effect };
        ((Microsoft.Maui.ILayout)bg).CrossPlatformArrange(new Rect(0, 0, 400, 100));
        var renderer = (ParticlesEffect.Renderer)bg.Renderer!;
        renderer.Particles.Count.ShouldBe(4);

        // The host only listens while loaded; a setting change is what Sync reconciles.
        effect.Density = 0.5;
        renderer.Sync();
        renderer.Particles.Count.ShouldBe(2);
    }


    [Fact]
    public void SnowThatFallsOutOfTheBoxComesBackAtTheTop()
    {
        var renderer = (SnowEffect.Renderer)new SnowEffect { Density = 1 }.CreateRenderer(new Random(5));
        renderer.Resize(100, 100);
        renderer.Flakes[0].Y = 200;

        renderer.Step(1d / 60);

        renderer.Flakes[0].Y.ShouldBeLessThan(0);
    }


    [Fact]
    public void WindCarriesSnowRoundTheSides()
    {
        var renderer = (SnowEffect.Renderer)new SnowEffect { Density = 1, Wind = 1, Sway = 0 }.CreateRenderer(new Random(5));
        renderer.Resize(100, 100);
        var flake = renderer.Flakes[0];
        flake.X = 100 + flake.Radius - 0.01;
        flake.Y = 50;

        renderer.Step(0.1);

        flake.X.ShouldBeLessThan(10);
    }


    [Fact]
    public void ParticlesBounceOffTheEdges()
    {
        var renderer = (ParticlesEffect.Renderer)new ParticlesEffect { Density = 1, PointerMode = ParticlePointerMode.None }.CreateRenderer(new Random(2));
        renderer.Resize(100, 100);
        var p = renderer.Particles[0];
        p.X = 0.5;
        p.VX = -60;

        // One frame: a longer step is split up, and the particle would already be on its way back.
        renderer.Step(1d / 60);

        p.X.ShouldBe(0);
        p.VX.ShouldBeGreaterThan(0);
    }


    [Fact]
    public void ThePointerPushesParticlesAway()
    {
        var renderer = (ParticlesEffect.Renderer)new ParticlesEffect { Density = 1, PointerRadius = 100 }.CreateRenderer(new Random(2));
        renderer.Resize(100, 100);
        var p = renderer.Particles[0];
        p.X = 60; p.Y = 50; p.VX = 0; p.VY = 0;
        renderer.Pointer = new PointF(50, 50);

        renderer.Step(0.1);

        p.X.ShouldBeGreaterThan(60);
    }


    [Fact]
    public void APointerSpotlightGoesOutWhenThePointerLeaves()
    {
        var renderer = (DotPatternEffect.Renderer)new DotPatternEffect { Spotlight = SpotlightMode.Pointer }.CreateRenderer(new Random(1));
        renderer.Resize(200, 200);

        renderer.Pointer = new PointF(40, 60);
        renderer.Settle();
        renderer.Spot.ShouldBe(new PointF(40, 60));
        renderer.SpotLevel.ShouldBe(1);

        renderer.Pointer = null;
        renderer.Settle();
        renderer.SpotLevel.ShouldBe(0);
    }


    [Fact]
    public void AnAutoSpotlightWandersWithoutAPointerAndEasesTowardsOne()
    {
        var renderer = (DotPatternEffect.Renderer)new DotPatternEffect { Spotlight = SpotlightMode.Auto }.CreateRenderer(new Random(1));
        renderer.Resize(200, 200);
        renderer.Settle();
        renderer.SpotLevel.ShouldBe(1);

        renderer.Pointer = new PointF(190, 190);
        var before = renderer.Spot;
        renderer.Step(0.05);

        // Partway there, not snapped.
        var distanceBefore = Distance(before, new PointF(190, 190));
        var distanceAfter = Distance(renderer.Spot, new PointF(190, 190));
        distanceAfter.ShouldBeLessThan(distanceBefore);
        distanceAfter.ShouldBeGreaterThan(0);
    }


    [Fact]
    public void MeteorsWaitTheirTurnThenRelaunchOnceAcross()
    {
        var renderer = (MeteorsEffect.Renderer)new MeteorsEffect { Density = 0.01 }.CreateRenderer(new Random(4));
        renderer.Resize(100, 100);

        // Even a small card gets one.
        renderer.Meteors.Count.ShouldBe(1);
        var m = renderer.Meteors[0];
        m.Wait = 0;
        m.Travelled = m.Distance - 1;

        renderer.Step(0.1);

        m.Travelled.ShouldBeLessThan(1);
    }


    [Fact]
    public void NoiseCyclesItsFramesAtTheFrameRate()
    {
        var renderer = (NoiseEffect.Renderer)new NoiseEffect { FrameRate = 10 }.CreateRenderer(new Random(1));
        renderer.Resize(400, 100);

        // The grain is a tile, never bigger than the box.
        renderer.Tile.ShouldBe(new SizeF(NoiseEffect.TileSize, 100));
        renderer.Frame.ShouldBe(0);

        renderer.Step(0.05);
        renderer.Frame.ShouldBe(0);
        renderer.Step(0.06);
        renderer.Frame.ShouldBe(1);
    }


    [Fact]
    public void SwitchingTheStarfieldToWarpScattersItAfresh()
    {
        var effect = new StarfieldEffect { Density = 1 };
        var renderer = (StarfieldEffect.Renderer)effect.CreateRenderer(new Random(1));
        renderer.Resize(100, 100);
        var calm = renderer.Stars[0];

        effect.Warp = true;
        renderer.Sync();

        renderer.Stars.ShouldNotContain(calm);
        renderer.Stars.ShouldAllBe(s => s.X >= -1 && s.X <= 1);
    }


    [Fact]
    public void MatrixHasAColumnPerGlyphWidth()
    {
        var renderer = (MatrixEffect.Renderer)new MatrixEffect { FontSize = 10 }.CreateRenderer(new Random(1));
        renderer.Resize(95, 50);

        renderer.Columns.Count.ShouldBe(10);
        renderer.Columns.ShouldAllBe(c => c.Glyphs.Length == 6);
    }


    [Fact]
    public void MatrixDrawsEachGlyphInABoxTallerThanItsLine()
    {
        // iOS lays a string out in a CoreText frame and drops any line taller than the box. A glyph's
        // line is taller than its cell, so the cell-sized boxes this used to pass drew nothing on iOS
        // (Android draws regardless, which is why only iOS showed an empty black card).
        var renderer = (MatrixEffect.Renderer)new MatrixEffect { FontSize = 12 }.CreateRenderer(new Random(1));
        renderer.Resize(120, 240);
        for (var i = 0; i < 240; i++)
            renderer.Step(1d / 60);

        var canvas = DispatchProxy.Create<ICanvas, DrawStringRecorder>();
        renderer.Draw(canvas);

        var boxes = ((DrawStringRecorder)(object)canvas).Boxes;
        boxes.ShouldNotBeEmpty();
        boxes.ShouldAllBe(b => b.Width >= 24 && b.Height >= 24);
    }


    /// <summary>An <see cref="ICanvas"/> that records the box of every rect-based DrawString.</summary>
    public class DrawStringRecorder : DispatchProxy
    {
        public List<RectF> Boxes { get; } = [];

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (method?.Name == nameof(ICanvas.DrawString) && args is [string, float x, float y, float w, float h, ..])
                this.Boxes.Add(new RectF(x, y, w, h));

            var type = method?.ReturnType;
            return type is null || type == typeof(void) || !type.IsValueType ? null : Activator.CreateInstance(type);
        }
    }


    [Fact]
    public void AuroraGlowsStayRoughlyInsideTheBox()
    {
        for (var i = 0; i < 6; i++)
        for (double t = 0; t < 600; t += 7)
        {
            var (x, y, scale) = AuroraEffect.GlowAt(i, t);
            x.ShouldBeInRange(0.07, 0.93);
            y.ShouldBeInRange(0.07, 0.69);
            scale.ShouldBeInRange(0.7, 1);
        }
    }


    [Theory]
    [MemberData(nameof(AllEffects))]
    public void EveryEffectRunsAndDraws(string name)
    {
        var renderer = EffectsByName[name]().CreateRenderer(new Random(7));
        renderer.Resize(320, 240);
        renderer.Pointer = new PointF(100, 100);

        for (var i = 0; i < 120; i++)
            renderer.Step(1d / 60);

        var canvas = new PictureCanvas(0, 0, 320, 240);
        Should.NotThrow(() => renderer.Draw(canvas));
        renderer.Time.ShouldBe(2, 1e-6);
    }


    [Theory]
    [MemberData(nameof(AllEffects))]
    public void AnEffectWithNoSizeDoesNothing(string name)
    {
        var renderer = EffectsByName[name]().CreateRenderer(new Random(7));
        renderer.Resize(0, 0);
        renderer.Step(1);
        renderer.Time.ShouldBe(0);
    }


    static double Distance(PointF a, PointF b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));


    sealed class Fixed : ContentView
    {
        protected override Size MeasureOverride(double widthConstraint, double heightConstraint) => new(50, 20);
    }
}
