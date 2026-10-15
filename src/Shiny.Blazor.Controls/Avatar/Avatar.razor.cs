using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Shiny.Blazor.Controls;

/// <summary>
/// A person (or thing) in a small round frame: their picture when there is one, their initials when
/// there is not, and a generic silhouette when there is not even a name. An optional presence dot sits
/// on the bottom-right corner.
/// </summary>
/// <remarks>
/// <para>The initials are drawn underneath the picture, so they show while it loads and stay put if it
/// fails - a broken URL degrades to initials rather than to a hole.</para>
/// <para>Size, shape and ring are CSS custom properties (<c>--shiny-avatar-size</c>,
/// <c>--shiny-avatar-radius</c>, <c>--shiny-avatar-ring-width</c>, <c>--shiny-avatar-ring-color</c>),
/// which is how an <see cref="AvatarGroup"/> sizes everything inside it without touching a parameter.
/// A parameter set on the avatar itself still wins.</para>
/// </remarks>
public partial class Avatar
{
    string? failedUri;

    /// <summary>The person's name. Drives the initials, the accessible label and, with <see cref="ColorFromName"/>, the fill colour.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Initials to show instead of the ones derived from <see cref="Name"/>.</summary>
    [Parameter] public string? Initials { get; set; }

    /// <summary>The picture's URL. While it loads, and if it fails, the initials show.</summary>
    [Parameter] public string? Uri { get; set; }

    /// <summary>Width and height in px. The default, <c>-1</c>, is 40px - or whatever the enclosing <see cref="AvatarGroup"/> says.</summary>
    [Parameter] public double Size { get; set; } = -1;

    /// <summary>Circle, rounded square or square. Unset is a circle - or whatever the enclosing <see cref="AvatarGroup"/> says.</summary>
    [Parameter] public AvatarShape? Shape { get; set; }

    /// <summary>The presence dot on the bottom-right corner.</summary>
    [Parameter] public AvatarStatus Status { get; set; }

    /// <summary>Overrides the presence dot's colour (any CSS colour). Unset follows the theme's success / warning / error / outline tokens.</summary>
    [Parameter] public string? StatusColor { get; set; }

    /// <summary>Background behind the initials or silhouette (any CSS colour). Unset follows the theme's primary container.</summary>
    [Parameter] public string? FillColor { get; set; }

    /// <summary>Colour of the initials and silhouette (any CSS colour). Unset follows the theme's on-primary-container.</summary>
    [Parameter] public string? TextColor { get; set; }

    /// <summary>When true and <see cref="FillColor"/> is unset, the fill is a stable hue derived from <see cref="Name"/> - the same name always gets the same colour, on every host.</summary>
    [Parameter] public bool ColorFromName { get; set; }

    /// <summary>Colour of the ring around the avatar (any CSS colour). Unset follows the theme surface.</summary>
    [Parameter] public string? RingColor { get; set; }

    /// <summary>Ring thickness in px. The default, <c>-1</c>, draws none - or the enclosing <see cref="AvatarGroup"/>'s separator ring.</summary>
    [Parameter] public double RingThickness { get; set; } = -1;

    /// <summary>Size of the initials in px. The default, <c>-1</c>, scales with <see cref="Size"/>.</summary>
    [Parameter] public double FontSize { get; set; } = -1;

    /// <summary>Raised when the avatar is clicked. Handling it renders the avatar as a button.</summary>
    [Parameter] public EventCallback<MouseEventArgs> OnClick { get; set; }

    [Parameter] public string? CssClass { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? AdditionalAttributes { get; set; }

    /// <summary>The group this avatar sits in, if any. Public because Blazor silently skips a private cascading parameter.</summary>
    [CascadingParameter] public AvatarGroup? Group { get; set; }


    /// <summary>The initials actually shown: <see cref="Initials"/> when set, otherwise derived from <see cref="Name"/>.</summary>
    internal string DisplayInitials
        => !String.IsNullOrWhiteSpace(Initials) ? Initials! : AvatarText.GetInitials(Name);


    internal bool ShowImage
        => !String.IsNullOrWhiteSpace(Uri) && !String.Equals(failedUri, Uri, StringComparison.Ordinal);


    protected override void OnInitialized() => Group?.Register(this);


    public void Dispose() => Group?.Unregister(this);


    void OnImageError() => failedUri = Uri;


    internal string AccessibleLabel
    {
        get
        {
            var label = !String.IsNullOrWhiteSpace(Name) ? Name! : DisplayInitials;
            if (Status != AvatarStatus.None)
                label = String.IsNullOrEmpty(label) ? Status.ToString() : $"{label}, {Status}";
            return String.IsNullOrEmpty(label) ? "Avatar" : label;
        }
    }


    string StatusClass => Status.ToString().ToLowerInvariant();


    string? StatusStyle => String.IsNullOrWhiteSpace(StatusColor) ? null : $"background:{StatusColor};";


    string RootClass
    {
        get
        {
            var css = "shiny-avatar";
            if (!String.IsNullOrWhiteSpace(CssClass))
                css += " " + CssClass;
            if (AdditionalAttributes?.TryGetValue("class", out var extra) == true && extra is not null)
                css += " " + extra;
            return css;
        }
    }


    internal string RootStyle
    {
        get
        {
            var ci = CultureInfo.InvariantCulture;
            var style = "";

            if (Size > 0)
                style += $"--shiny-avatar-size:{Size.ToString(ci)}px;";

            if (Shape is AvatarShape shape)
                style += $"--shiny-avatar-radius:{Radius(shape)};";

            if (FontSize > 0)
                style += $"--shiny-avatar-font-size:{FontSize.ToString(ci)}px;";
            else if (DisplayInitials.Length > 2)
                style += "--shiny-avatar-font-size:calc(var(--shiny-avatar-size, 40px) * 0.32);";

            var fill = FillColor;
            var ink = TextColor;
            if (String.IsNullOrWhiteSpace(fill) && ColorFromName && !String.IsNullOrWhiteSpace(Name))
            {
                // Saturation and lightness are fixed so white initials read on every hue, in both schemes.
                fill = $"hsl({AvatarText.GetHue(Name).ToString(ci)} 55% 42%)";
                if (String.IsNullOrWhiteSpace(ink))
                    ink = "white";
            }
            if (!String.IsNullOrWhiteSpace(fill))
                style += $"--shiny-avatar-fill:{fill};";
            if (!String.IsNullOrWhiteSpace(ink))
                style += $"--shiny-avatar-ink:{ink};";

            if (RingThickness >= 0)
                style += $"--shiny-avatar-ring-width:{RingThickness.ToString(ci)}px;";
            if (!String.IsNullOrWhiteSpace(RingColor))
                style += $"--shiny-avatar-ring-color:{RingColor};";

            if (AdditionalAttributes?.TryGetValue("style", out var caller) == true && caller is not null)
                style += caller;

            return style;
        }
    }


    IReadOnlyDictionary<string, object>? Splatted
        => AdditionalAttributes?
            .Where(x => x.Key != "style" && x.Key != "class")
            .ToDictionary(x => x.Key, x => x.Value);


    internal static string Radius(AvatarShape shape) => shape switch
    {
        AvatarShape.Rounded => "25%",
        AvatarShape.Square => "0",
        _ => "50%"
    };
}
