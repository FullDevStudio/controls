using Microsoft.AspNetCore.Components;

namespace Shiny.Blazor.Controls;

/// <summary>
/// A card with a short beam of light that travels endlessly around its border, in the style of
/// Magic UI's border beam.
/// </summary>
/// <example>
/// <code>
/// &lt;BorderBeam CornerRadius="16" BorderWidth="2" Style="padding:20px"&gt;
///     Upgrade to Pro
/// &lt;/BorderBeam&gt;
/// </code>
/// </example>
/// <remarks>
/// The beam rides a CSS <c>offset-path: rect()</c> (Chrome 116, Safari 17.2, Firefox 122 and later);
/// older browsers show the track without the beam.
/// </remarks>
public partial class BorderBeam
{
    /// <summary>How long the beam is, in CSS pixels. Default 50.</summary>
    [Parameter] public double Size { get; set; } = 50;

    /// <summary>How long one lap takes. Default 6 seconds.</summary>
    [Parameter] public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(6);

    /// <summary>
    /// How far into its lap the beam already is when it appears - as in Magic UI, this is a head start
    /// rather than a wait, so the beam is never missing.
    /// </summary>
    [Parameter] public TimeSpan Delay { get; set; }

    /// <summary>The colour at the head of the beam, as any CSS colour. Default #ffaa40.</summary>
    [Parameter] public string ColorFrom { get; set; } = "#ffaa40";

    /// <summary>The colour the beam shades into before its tail fades out. Default #9c40ff.</summary>
    [Parameter] public string ColorTo { get; set; } = "#9c40ff";

    /// <summary>Travels anticlockwise instead of clockwise.</summary>
    [Parameter] public bool Reverse { get; set; }

    /// <summary>Where on the lap the beam starts, as a percentage (0-100) of the way round from the top-left.</summary>
    [Parameter] public double InitialOffset { get; set; }


    private protected override string Block => "shiny-border-beam";

    /// <summary>The offset-distance the lap runs from and to: Magic UI's keyframes, reversed by swapping ends.</summary>
    internal (string Start, string End) Lap
        => this.Reverse
            ? (Percent(100 - this.InitialOffset), Percent(-this.InitialOffset))
            : (Percent(this.InitialOffset), Percent(100 + this.InitialOffset));

    private protected override string EffectStyle
    {
        get
        {
            var (start, end) = this.Lap;
            var size = Math.Max(0, this.Size).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            return
                $"--shiny-border-beam-size:{size}px;" +
                $"--shiny-border-beam-duration:{Seconds(this.Duration > TimeSpan.Zero ? this.Duration : TimeSpan.FromMilliseconds(10))};" +
                $"--shiny-border-beam-delay:{Seconds(this.Delay)};" +
                $"--shiny-border-beam-from:{this.ColorFrom};" +
                $"--shiny-border-beam-to:{this.ColorTo};" +
                $"--shiny-border-beam-start:{start};" +
                $"--shiny-border-beam-end:{end};";
        }
    }
}
