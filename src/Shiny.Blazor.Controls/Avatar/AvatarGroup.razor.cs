using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Shiny.Blazor.Controls;

/// <summary>
/// A row of overlapping <see cref="Avatar"/>s - a "facepile" - that collapses everyone past
/// <see cref="Max"/> into a single "+N" chip.
/// </summary>
/// <remarks>
/// Put the avatars directly inside the group (a <c>@foreach</c> is fine). Each one must be a direct
/// child: an avatar wrapped in another element (a tooltip, a link) is counted but cannot be hidden
/// past <see cref="Max"/>, because hiding is done by position among the group's children.
/// </remarks>
public partial class AvatarGroup
{
    readonly string groupId = "ag" + Guid.NewGuid().ToString("N")[..10];
    readonly List<Avatar> avatars = new();

    /// <summary>The avatars.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>The most avatars drawn before the rest collapse into the "+N" chip (the chip is not counted). <c>0</c> draws them all.</summary>
    [Parameter] public int Max { get; set; }

    /// <summary>The real number of people when the avatars given are only a sample - "+N" counts against this. <c>0</c> uses the number of avatars.</summary>
    [Parameter] public int TotalCount { get; set; }

    /// <summary>When positive, the size in px of every avatar that does not set its own. The default, <c>-1</c>, leaves the avatars' default.</summary>
    [Parameter] public double Size { get; set; } = -1;

    /// <summary>When set, the shape of every avatar that does not set its own.</summary>
    [Parameter] public AvatarShape? Shape { get; set; }

    /// <summary>How far in px each avatar tucks under the one before it. The default, <c>-1</c>, is a quarter of the avatar size; <c>0</c> lays them side by side.</summary>
    [Parameter] public double Overlap { get; set; } = -1;

    /// <summary>The separator ring around each avatar (any CSS colour). Unset follows the theme surface - set it to whatever the group sits on.</summary>
    [Parameter] public string? RingColor { get; set; }

    /// <summary>Thickness of the separator ring in px. <c>0</c> draws none.</summary>
    [Parameter] public double RingThickness { get; set; } = 2;

    /// <summary>Composite format for the overflow chip; <c>{0}</c> is the hidden count.</summary>
    [Parameter] public string OverflowFormat { get; set; } = "+{0}";

    /// <summary>Raised with the hidden count when the "+N" chip is clicked. Handling it renders the chip as a button.</summary>
    [Parameter] public EventCallback<int> OnOverflowClick { get; set; }

    [Parameter] public string? CssClass { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? AdditionalAttributes { get; set; }


    /// <summary>How many avatars are in the group.</summary>
    public int Count => avatars.Count;

    /// <summary>How many avatars are drawn - not counting the "+N" chip.</summary>
    public int VisibleCount => Max > 0 ? Math.Min(Max, avatars.Count) : avatars.Count;

    /// <summary>How many people the "+N" chip stands for. <c>0</c> when everyone is drawn.</summary>
    public int OverflowCount => Math.Max(0, Math.Max(TotalCount, avatars.Count) - VisibleCount);


    /// <remarks>
    /// Called from each avatar's <c>OnInitialized</c>, never from <c>OnParametersSet</c>: the group's
    /// re-render re-renders its ChildContent, and a child that re-registered on every parameter pass
    /// would spin the renderer forever.
    /// </remarks>
    internal void Register(Avatar avatar)
    {
        if (avatars.Contains(avatar))
            return;

        avatars.Add(avatar);
        StateHasChanged();
    }


    internal void Unregister(Avatar avatar)
    {
        if (avatars.Remove(avatar))
            StateHasChanged();
    }


    internal string OverflowText => AvatarText.FormatOverflow(OverflowFormat, OverflowCount);

    // The chip matches the row it closes, even when the group itself leaves size and shape alone.
    double ChipSize => Size > 0 ? Size : avatars.Count > 0 ? avatars[0].Size : -1;

    AvatarShape? ChipShape => Shape ?? (avatars.Count > 0 ? avatars[0].Shape : null);

    EventCallback<MouseEventArgs> OverflowClickCallback
        => OnOverflowClick.HasDelegate
            ? EventCallback.Factory.Create<MouseEventArgs>(this, () => OnOverflowClick.InvokeAsync(OverflowCount))
            : default;


    internal string? HiddenRule
        => Max > 0 && avatars.Count > Max
            ? $"[data-shiny-avatar-group=\"{groupId}\"] > .shiny-avatar:nth-child(n+{(Max + 1).ToString(CultureInfo.InvariantCulture)}):not(.shiny-avatar-overflow){{display:none;}}"
            : null;


    string AccessibleLabel
        => OverflowCount > 0 ? $"{VisibleCount} people and {OverflowCount} more" : $"{VisibleCount} people";


    string RootClass
    {
        get
        {
            var css = "shiny-avatar-group";
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
            var style = $"--shiny-avatar-ring-width:{Math.Max(0, RingThickness).ToString(ci)}px;";

            if (Size > 0)
                style += $"--shiny-avatar-size:{Size.ToString(ci)}px;";
            if (Shape is AvatarShape shape)
                style += $"--shiny-avatar-radius:{Avatar.Radius(shape)};";
            if (Overlap >= 0)
                style += $"--shiny-avatar-overlap:{Overlap.ToString(ci)}px;";
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
}
