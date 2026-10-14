using Microsoft.AspNetCore.Components;

namespace Shiny.Blazor.Controls;

/// <summary>
/// A card whose border shimmers with a slowly drifting multi-colour glow, in the style of Magic UI's
/// shine border.
/// </summary>
/// <example>
/// <code>
/// &lt;ShineBorder ShineColors="@(["#A07CFE", "#FE8FB5", "#FFBE7B"])" BorderWidth="2" Style="padding:20px"&gt;
///     Sign in
/// &lt;/ShineBorder&gt;
/// </code>
/// </example>
public partial class ShineBorder
{
    static readonly IReadOnlyList<string> DefaultColors = ["#A07CFE", "#FE8FB5", "#FFBE7B"];

    /// <summary>The colours the shine passes through, in order, as CSS colours. Default violet, pink and peach.</summary>
    [Parameter] public IReadOnlyList<string>? ShineColors { get; set; }

    /// <summary>How long the shine takes to drift across and back. Default 14 seconds.</summary>
    [Parameter] public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(14);


    private protected override string Block => "shiny-shine-border";

    /// <summary><c>radial-gradient(transparent, transparent, colours…, transparent, transparent)</c>.</summary>
    internal string Gradient
    {
        get
        {
            var colors = this.ShineColors is { Count: > 0 } list ? list : DefaultColors;
            return $"radial-gradient(transparent,transparent,{string.Join(",", colors)},transparent,transparent)";
        }
    }

    private protected override string EffectStyle
        => $"--shiny-shine-border-duration:{Seconds(this.Duration > TimeSpan.Zero ? this.Duration : TimeSpan.FromMilliseconds(10))};" +
           $"--shiny-shine-border-gradient:{this.Gradient};";
}
