using Shiny.Controls.Media;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Media.Tests;

public class VideoTextLayoutTests
{
    [Fact]
    public void Only_explicit_breaks_split_lines()
    {
        VideoTextLayout.SplitLines("BIG WAVES\r\nSURF\rNOW").ShouldBe(["BIG WAVES", "SURF", "NOW"]);
        VideoTextLayout.SplitLines(null).ShouldBeEmpty();
        VideoTextLayout.SplitLines("").ShouldBeEmpty();
    }

    [Fact]
    public void Auto_fit_is_bound_by_the_tighter_axis()
    {
        // 500x120 at 100px into a 1000x100 box: width allows 180px, height only 75px
        VideoTextLayout.FitFontSize(500, 120, 100, 1000, 100).ShouldBe(75, 0.0001);

        // same text into a tall box: width wins
        VideoTextLayout.FitFontSize(500, 120, 100, 500, 1000).ShouldBe(90, 0.0001);
    }

    [Fact]
    public void Nothing_to_measure_fits_to_zero()
    {
        VideoTextLayout.FitFontSize(0, 120, 100, 1000, 100).ShouldBe(0);
        VideoTextLayout.FitFontSize(500, 120, 100, 0, 100).ShouldBe(0);
    }

    [Fact]
    public void A_fixed_size_wins_over_auto_fit()
        => VideoTextLayout.ResolveFontSize(48, 500, 120, 100, 1000, 100).ShouldBe(48);

    [Fact]
    public void Cover_scale_crops_the_overflowing_axis()
    {
        // 16:9 video in a square view: height fills, width overflows by 16/9
        var (sx, sy) = VideoTextLayout.CoverScale(1920, 1080, 400, 400);
        sx.ShouldBe(16d / 9d, 0.0001);
        sy.ShouldBe(1, 0.0001);

        VideoTextLayout.CoverScale(0, 0, 400, 400).ShouldBe((1d, 1d));
    }

    [Fact]
    public void Auto_fit_svg_sizes_its_viewbox_from_the_measured_widths()
    {
        var svg = VideoTextLayout.BuildSvgMask(["OCEAN"], 0, "sans-serif", "bold", null, [450]);

        // 450 / 0.9 = 500 wide, 120 / 0.9 = 133.333 tall
        svg.ShouldContain("viewBox='0 0 500 133.333'");
        svg.ShouldContain("preserveAspectRatio='xMidYMid meet'");
        svg.ShouldContain("font-size='100'");
        svg.ShouldContain(">OCEAN</text>");
    }

    [Fact]
    public void Fixed_size_svg_has_no_viewbox()
    {
        var svg = VideoTextLayout.BuildSvgMask(["OCEAN"], 64, null, null, null);
        svg.ShouldNotContain("viewBox");
        svg.ShouldContain("font-size='64'");
        svg.ShouldContain("x='50%' y='50%'");
    }

    [Fact]
    public void Lines_are_centred_as_a_block()
    {
        var svg = VideoTextLayout.BuildSvgMask(["A", "B"], 100, null, null, null);
        svg.ShouldContain("dy='-60'>A<");
        svg.ShouldContain("dy='60'>B<");
    }

    [Fact]
    public void Text_and_attributes_are_xml_escaped()
    {
        var svg = VideoTextLayout.BuildSvgMask(["<R&D>"], 40, "Bob's Font", null, null);
        svg.ShouldContain("&lt;R&amp;D&gt;");
        svg.ShouldContain("font-family='Bob&apos;s Font'");
    }

    [Fact]
    public void Data_url_is_fully_percent_encoded()
    {
        var url = VideoTextLayout.ToDataUrl("<svg a='1'>#</svg>");
        url.ShouldStartWith("data:image/svg+xml;charset=utf-8,");
        url.ShouldNotContain("'");
        url.ShouldNotContain("#");
        url.ShouldNotContain("\"");
    }
}


public class VideoTextTests : MediaTestBase
{
    [Fact]
    public void Defaults_are_silent_bold_and_auto_fit()
    {
        var text = new VideoText();
        text.IsMuted.ShouldBeTrue();
        text.FontAttributes.ShouldBe(FontAttributes.Bold);
        text.FontSize.ShouldBe(0);
    }

    [Fact]
    public void Negative_font_size_coerces_to_auto_fit()
        => new VideoText { FontSize = -10 }.FontSize.ShouldBe(0);

    [Fact]
    public void Without_a_mask_the_text_is_shown_as_a_label()
    {
        // the plain net10.0 target (Linux head, and this test host) has no masked output
        VideoText.IsMaskSupported.ShouldBeFalse();

        var text = new VideoText { Text = "OCEAN", FallbackTextColor = Colors.Teal, FontSize = 40 };
        var label = text.Content.ShouldBeOfType<Label>();
        label.Text.ShouldBe("OCEAN");
        label.TextColor.ShouldBe(Colors.Teal);
        label.FontSize.ShouldBe(40);
    }
}
