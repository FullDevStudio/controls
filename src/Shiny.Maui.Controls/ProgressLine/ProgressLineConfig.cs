namespace Shiny.Maui.Controls;

/// <summary>Per-run settings for <see cref="IProgressLineService.Start"/>.</summary>
public class ProgressLineConfig
{
    /// <summary>
    /// Names the service's line for UI tests, which otherwise have nothing to find it by: it is docked
    /// into the page's overlay layer, under no element of the app's. Null (the default) leaves it unnamed.
    /// The line is shared by overlapping runs, so the last run's id wins, as its look does.
    /// </summary>
    public string? AutomationId { get; set; }

    /// <inheritdoc cref="ProgressLine.Position"/>
    public ProgressLinePosition Position { get; set; } = ProgressLinePosition.Top;

    /// <inheritdoc cref="ProgressLine.BarColor"/>
    public Color? BarColor { get; set; }

    /// <inheritdoc cref="ProgressLine.TrackColor"/>
    public Color? TrackColor { get; set; } = Colors.Transparent;

    /// <inheritdoc cref="ProgressLine.LineHeight"/>
    public double LineHeight { get; set; } = 3;

    /// <inheritdoc cref="ProgressLine.CornerRadius"/>
    public double CornerRadius { get; set; }

    /// <inheritdoc cref="ProgressLine.UseGradient"/>
    public bool UseGradient { get; set; }

    /// <inheritdoc cref="ProgressLine.GradientStartColor"/>
    public Color? GradientStartColor { get; set; }

    /// <inheritdoc cref="ProgressLine.GradientEndColor"/>
    public Color? GradientEndColor { get; set; }

    /// <inheritdoc cref="ProgressLine.PulseEnabled"/>
    public bool PulseEnabled { get; set; }

    /// <inheritdoc cref="ProgressLine.AutoInset"/>
    public bool AutoInset { get; set; } = true;

    /// <inheritdoc cref="ProgressLine.Offset"/>
    public Thickness Offset { get; set; }

    /// <inheritdoc cref="ProgressLine.FadeDuration"/>
    public int FadeDuration { get; set; } = 200;

    /// <inheritdoc cref="ProgressLine.ProgressAnimationDuration"/>
    public int ProgressAnimationDuration { get; set; } = 250;

    /// <summary>
    /// Run the sweeping indeterminate animation instead of a fill. Use it when the work genuinely has
    /// no measurable progress; otherwise the trickle below is a better lie than a sweep, because it
    /// still moves toward completion.
    /// </summary>
    public bool Indeterminate { get; set; }

    /// <summary>
    /// Whether the line creeps forward on its own between <see cref="IProgressLineHandle.SetProgress"/>
    /// calls, so a request that reports nothing for two seconds still looks alive.
    /// </summary>
    public bool Trickle { get; set; } = true;

    /// <summary>Where the line jumps to the instant it appears, so it is never a zero-width nothing.</summary>
    public double StartProgress { get; set; } = 0.08;

    /// <summary>
    /// The asymptote the trickle approaches but never reaches. Completion has to come from the caller
    /// — a line that trickles to 100% on its own has told the user the work finished when it has not.
    /// </summary>
    public double TrickleCeiling { get; set; } = 0.9;

    /// <summary>How often the trickle advances.</summary>
    public TimeSpan TrickleInterval { get; set; } = TimeSpan.FromMilliseconds(400);

    /// <summary>
    /// Fraction of the remaining distance to the ceiling covered per tick. Lower creeps for longer.
    /// </summary>
    public double TrickleRate { get; set; } = 0.12;

    /// <summary>
    /// Last word on the line before it is shown, for anything this config does not surface.
    /// </summary>
    public Action<ProgressLine>? Configure { get; set; }
}
