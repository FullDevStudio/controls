using System.Globalization;
using System.Text.Json;
using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

/// <summary>
/// What the animated background tells the script. The drawing itself happens in the browser, so the
/// settings payload and the style are everything the component decides.
/// </summary>
public class AnimatedBackgroundTests
{
    public static TheoryData<BackgroundEffect, string> AllEffects => new()
    {
        { new AuroraEffect(), "aurora" },
        { new ParticlesEffect(), "particles" },
        { new SnowEffect(), "snow" },
        { new RainEffect(), "rain" },
        { new MeteorsEffect(), "meteors" },
        { new DotPatternEffect(), "dotPattern" },
        { new NoiseEffect(), "noise" },
        { new StarfieldEffect(), "starfield" },
        { new FirefliesEffect(), "fireflies" },
        { new BubblesEffect(), "bubbles" },
        { new WavesEffect(), "waves" },
        { new MatrixEffect(), "matrix" }
    };


    [Theory]
    [MemberData(nameof(AllEffects))]
    public void EveryEffectNamesItsKindForTheScript(BackgroundEffect effect, string kind)
    {
        using var json = JsonDocument.Parse(effect.ToJson());

        json.RootElement.GetProperty("kind").GetString().ShouldBe(kind);
        json.RootElement.GetProperty("speed").GetDouble().ShouldBe(1);
    }


    [Fact]
    public void OptionsUseTheScriptsNames()
    {
        var effect = new ParticlesEffect
        {
            Colors = ["#fff", "red"],
            LinkDistance = 80,
            PointerMode = ParticlePointerMode.Attract
        };
        using var json = JsonDocument.Parse(effect.ToJson());
        var root = json.RootElement;

        root.GetProperty("colors").EnumerateArray().Select(c => c.GetString()).ShouldBe(["#fff", "red"]);
        root.GetProperty("linkDistance").GetDouble().ShouldBe(80);
        root.GetProperty("linkColor").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("pointerMode").GetString().ShouldBe("attract");
    }


    [Fact]
    public void BlankColoursAreDropped()
    {
        using var json = JsonDocument.Parse(new AuroraEffect { Colors = ["#fff", " ", ""] }.ToJson());

        json.RootElement.GetProperty("colors").GetArrayLength().ShouldBe(1);
    }


    [Fact]
    public void DelaysAreSentInSeconds()
    {
        using var json = JsonDocument.Parse(new MeteorsEffect { MaxDelay = TimeSpan.FromMilliseconds(1500) }.ToJson());

        json.RootElement.GetProperty("maxDelay").GetDouble().ShouldBe(1.5);
    }


    [Fact]
    public void ThePayloadIsWrittenInvariantly()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var text = new SnowEffect { Density = 1.5, Wind = -0.25 }.ToJson();

            text.ShouldContain("\"density\":1.5");
            text.ShouldContain("\"wind\":-0.25");

            new AnimatedBackground { CornerRadius = 12.5 }.RootStyle.ShouldBe("border-radius:12.5px;");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }


    [Fact]
    public void TheStyleCarriesTheBackgroundAndKeepsTheCallersStyleLast()
        => new AnimatedBackground { Background = "linear-gradient(#000, #111)", Style = "padding:8px" }
            .RootStyle.ShouldBe("background:linear-gradient(#000, #111);padding:8px");


    [Fact]
    public void ChangingASettingOnTheSameInstanceChangesTheSignature()
    {
        // Settings objects are mutable, so the component compares what it would send, not the reference.
        var effect = new SnowEffect();
        var background = new AnimatedBackground { Effect = effect };
        var before = background.Signature;

        effect.Density = 3;

        background.Signature.ShouldNotBe(before);
    }


    [Fact]
    public void AnEqualEffectHasTheSameSignature()
        => new AnimatedBackground { Effect = new SnowEffect { Wind = 1 } }.Signature
            .ShouldBe(new AnimatedBackground { Effect = new SnowEffect { Wind = 1 } }.Signature);


    [Fact]
    public void FixedIsAStateClass()
    {
        new AnimatedBackground { Fixed = true }.StateClasses.ShouldBe("shiny-animated-background--fixed");
        new AnimatedBackground().StateClasses.ShouldBeEmpty();
    }
}
