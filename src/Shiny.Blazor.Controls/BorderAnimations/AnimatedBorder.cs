using System.Globalization;
using Microsoft.AspNetCore.Components;

namespace Shiny.Blazor.Controls;

/// <summary>
/// A rounded card whose border is animated - the base of <see cref="BorderBeam"/> and
/// <see cref="ShineBorder"/>. The effect is pure CSS: a ring laid over the content and masked down to
/// the border, so it is never hidden by what the content paints and no script runs per frame.
/// </summary>
/// <remarks>
/// The card's own padding defaults to <see cref="BorderWidth"/>; set <c>padding</c> through
/// <see cref="Style"/> or <see cref="Class"/> for more. Background, shadow and size are styled the
/// same way - the card clips its content to <see cref="CornerRadius"/>.
/// </remarks>
public abstract class AnimatedBorder : ComponentBase
{
    /// <summary>What sits inside the border. Leave it empty with <see cref="Overlay"/> to decorate an existing element instead.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The radius of the outer corners, in CSS pixels. Default 12.</summary>
    [Parameter] public double CornerRadius { get; set; } = 12;

    /// <summary>How thick the border is - both the track and the animation - in CSS pixels. Default 1.</summary>
    [Parameter] public double BorderWidth { get; set; } = 1;

    /// <summary>
    /// The static border the animation runs over, as any CSS colour. Unset follows the theme's
    /// outline-variant colour; <c>transparent</c> leaves only the animation.
    /// </summary>
    [Parameter] public string? TrackColor { get; set; }

    /// <summary>Whether it animates. False holds the effect where it is.</summary>
    [Parameter] public bool IsRunning { get; set; } = true;

    /// <summary>Holds still under <c>prefers-reduced-motion: reduce</c>. On by default.</summary>
    [Parameter] public bool RespectReducedMotion { get; set; } = true;

    /// <summary>
    /// Renders only the animated border, absolutely positioned over its nearest positioned ancestor
    /// (Magic UI's usage) - for decorating an element you already have, or stacking a second effect
    /// over a card. The ancestor needs <c>position: relative</c>.
    /// </summary>
    [Parameter] public bool Overlay { get; set; }

    [Parameter] public string? Class { get; set; }

    [Parameter] public string? Style { get; set; }


    /// <summary>The block name every class of the component starts with.</summary>
    private protected abstract string Block { get; }

    /// <summary>The component's own custom properties, appended to the shared ones.</summary>
    private protected abstract string EffectStyle { get; }

    internal string StateClasses
    {
        get
        {
            var classes = new List<string>(3);
            if (this.Overlay) classes.Add($"{this.Block}--overlay");
            if (!this.IsRunning) classes.Add($"{this.Block}--paused");
            if (this.RespectReducedMotion) classes.Add($"{this.Block}--respect-motion");
            return string.Join(' ', classes);
        }
    }

    internal string RootStyle
    {
        get
        {
            var c = CultureInfo.InvariantCulture;
            var style =
                $"--shiny-border-radius:{Math.Max(0, this.CornerRadius).ToString("0.##", c)}px;" +
                $"--shiny-border-width:{Math.Max(0, this.BorderWidth).ToString("0.##", c)}px;";

            if (!string.IsNullOrWhiteSpace(this.TrackColor))
                style += $"--shiny-border-track:{this.TrackColor};";

            style += this.EffectStyle;
            return string.IsNullOrWhiteSpace(this.Style) ? style : style + this.Style;
        }
    }


    private protected static string Seconds(TimeSpan time)
        => time.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture) + "s";

    private protected static string Percent(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture) + "%";
}
