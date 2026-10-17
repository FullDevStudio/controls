using Shiny.Maui.Controls.Desktop.TrayIcon;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Desktop.Tests;

/// <summary>
/// Pills and progress rows. The split that matters is which changes repaint a row in place and
/// which rebuild the whole menu: a progress value driven from a timer has to take the in-place
/// path, or an open menu would never move (a rebuild replaces a menu the user can't see).
/// </summary>
public class TrayMenuRichItemTests
{
    static (TrayMenu Menu, List<string> Events) Observe(Action<TrayMenuBuilder> build)
    {
        var menu = TrayMenu.Build(build);
        var events = new List<string>();
        menu.Changed += (_, _) => events.Add("rebuild");
        menu.ItemUpdated += (_, item) => events.Add("update:" + item.GetType().Name);
        return (menu, events);
    }

    [Fact]
    public void Progress_value_label_detail_and_tone_update_in_place()
    {
        var usage = new TrayProgressItem("Session", 0.2);
        var (_, events) = Observe(b => b.Progress(usage));

        usage.Value = 0.5;
        usage.Label = "Weekly";
        usage.Detail = "5 of 10";
        usage.Tone = TrayTone.Warning;

        events.ShouldBe(Enumerable.Repeat("update:TrayProgressItem", 4));
    }

    [Fact]
    public void Caption_text_change_updates_in_place_but_adding_or_removing_it_rebuilds()
    {
        var usage = new TrayProgressItem("Session", 0.2, "Resets in 3 hr");
        var (_, events) = Observe(b => b.Progress(usage));

        usage.Caption = "Resets in 2 hr";   // same shape
        usage.Caption = null;               // row gets shorter
        usage.Caption = "Resets soon";      // row gets taller

        events.ShouldBe(new[] { "update:TrayProgressItem", "rebuild", "rebuild" });
    }

    [Fact]
    public void Setting_an_unchanged_value_raises_nothing()
    {
        var usage = new TrayProgressItem("Session", 0.4);
        var (_, events) = Observe(b => b.Progress(usage));

        usage.Value = 0.4;
        usage.Tone = TrayTone.Accent;

        events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(-0.5, 0)]
    [InlineData(1.7, 1)]
    [InlineData(double.NaN, 0)]
    [InlineData(0.25, 0.25)]
    public void Value_is_clamped(double input, double expected)
        => new TrayProgressItem("x", input).Value.ShouldBe(expected);

    [Fact]
    public void Detail_defaults_to_the_percentage_and_an_empty_string_hides_it()
    {
        var item = new TrayProgressItem("x", 0.625);
        item.DisplayDetail.ShouldBe(Math.Round(62.5, MidpointRounding.AwayFromZero).ToString("0") + "%");

        item.Detail = "";
        item.DisplayDetail.ShouldBe("");

        item.Detail = "12 / 50";
        item.DisplayDetail.ShouldBe("12 / 50");
    }

    [Fact]
    public void Pill_change_rebuilds_the_menu()
    {
        var upgrade = new TrayMenuItem("Upgrade");
        var (_, events) = Observe(b => b.Item(upgrade));

        upgrade.Pill = new TrayPill("New", TrayTone.Accent);
        upgrade.Pill = new TrayPill("New", TrayTone.Accent); // record equality: no change

        events.ShouldBe(new[] { "rebuild" });
    }

    [Fact]
    public void Progress_in_a_submenu_is_adopted_and_updates_in_place()
    {
        var usage = new TrayProgressItem("Session", 0.1);
        var (_, events) = Observe(b => b.Submenu("Usage", s => s.Progress(usage)));

        usage.Value = 0.9;

        events.ShouldBe(new[] { "update:TrayProgressItem" });
    }

    [Theory]
    [InlineData(0, "▱▱▱▱▱▱▱▱▱▱")]
    [InlineData(0.62, "▰▰▰▰▰▰▱▱▱▱")]
    [InlineData(0.65, "▰▰▰▰▰▰▰▱▱▱")]
    [InlineData(1, "▰▰▰▰▰▰▰▰▰▰")]
    [InlineData(3, "▰▰▰▰▰▰▰▰▰▰")]
    public void Text_bar_rounds_to_ten_segments(double value, string expected)
        => TrayMenuText.Bar(value).ShouldBe(expected);

    [Fact]
    public void Text_fallbacks_for_linux()
    {
        TrayMenuText.PillLabel("Upgrade", new TrayPill("Pro")).ShouldBe("Upgrade  [Pro]");
        TrayMenuText.ProgressLine(new TrayProgressItem("Session", 1) { Detail = "Full" })
            .ShouldBe("Session   ▰▰▰▰▰▰▰▰▰▰  Full");
        TrayMenuText.ProgressLine(new TrayProgressItem("Session", 0) { Detail = "" })
            .ShouldBe("Session   ▱▱▱▱▱▱▱▱▱▱");
    }
}
