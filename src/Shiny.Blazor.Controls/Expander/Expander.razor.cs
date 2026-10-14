using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Shiny.Blazor.Controls;

/// <summary>
/// A header you click and content that animates in and out beneath (or above) it.
/// </summary>
/// <remarks>
/// The three motion effects are independent and combine. <see cref="ExpanderAnimation.Height"/> is
/// pure CSS — the panel is a grid transitioning <c>grid-template-rows</c> between <c>0fr</c> and
/// <c>1fr</c> — so there is no measuring, no JS interop, and content that changes size while open
/// still lays out normally.
/// <para>
/// Inside an <see cref="Accordion"/> the expander picks up that accordion's motion and chrome for
/// every property it did not set itself, and asks it for permission before changing state.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Expander HeaderText="Shipping" HeaderDetail="Arrives Tuesday"
///           Animation="ExpanderAnimation.Height | ExpanderAnimation.Slide | ExpanderAnimation.Fade"&gt;
///     &lt;p&gt;123 Fake Street&lt;/p&gt;
/// &lt;/Expander&gt;
/// </code>
/// </example>
public partial class Expander : IDisposable
{
    readonly string panelId = "shiny-expander-" + Guid.NewGuid().ToString("N");

    // Which parameters the consumer actually wrote. Everything else is free to take the accordion's
    // default, and there is no other way to tell "they set it to Fade" from "Fade is the default".
    readonly HashSet<string> explicitParameters = new(StringComparer.Ordinal);

    bool contentRealized;
    bool registered;
    bool hasRendered;

    /// <summary>The accordion this expander is inside, when there is one.</summary>
    [CascadingParameter] public IAccordionHost? AccordionHost { get; set; }


    // ---------------------------------------------------------------------------------------------
    // Content
    // ---------------------------------------------------------------------------------------------

    /// <summary>What the expander reveals.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>A header of your own. Replaces <see cref="HeaderText"/> and <see cref="HeaderDetail"/>.</summary>
    [Parameter] public RenderFragment? Header { get; set; }

    /// <summary>Title text for the built-in header.</summary>
    [Parameter] public string? HeaderText { get; set; }

    /// <summary>Optional second line under <see cref="HeaderText"/>.</summary>
    [Parameter] public string? HeaderDetail { get; set; }

    /// <summary>
    /// The model this expander stands for. An <see cref="Accordion"/> generating items from its
    /// <c>Items</c> fills this in, and hands it back on <c>OnItemExpanded</c>.
    /// </summary>
    [Parameter] public object? Item { get; set; }

    /// <summary>Your own indicator markup instead of a glyph. Still rotates under <see cref="ExpanderIndicatorMode.Rotate"/>.</summary>
    [Parameter] public RenderFragment? IndicatorContent { get; set; }

    /// <summary>
    /// Keep <see cref="ChildContent"/> out of the DOM until the first expand. A list of twenty
    /// expanders over twenty forms then renders one form, not twenty.
    /// </summary>
    [Parameter] public bool LoadContentOnDemand { get; set; }


    // ---------------------------------------------------------------------------------------------
    // State
    // ---------------------------------------------------------------------------------------------

    /// <summary>Whether the content is showing.</summary>
    [Parameter] public bool IsExpanded { get; set; }

    /// <summary>Two-way binding hook for <see cref="IsExpanded"/>.</summary>
    [Parameter] public EventCallback<bool> IsExpandedChanged { get; set; }

    /// <summary>Raised when the expander opens.</summary>
    [Parameter] public EventCallback OnExpanded { get; set; }

    /// <summary>Raised when the expander closes.</summary>
    [Parameter] public EventCallback OnCollapsed { get; set; }

    /// <summary>When false the header stops responding; <see cref="IsExpanded"/> still drives it in code.</summary>
    [Parameter] public bool IsToggleEnabled { get; set; } = true;

    /// <summary>
    /// When false, activating an already-open header does nothing. An <see cref="Accordion"/> that is
    /// not allowed to close everything sets this on whichever item is the last one open.
    /// </summary>
    [Parameter] public bool CanCollapse { get; set; } = true;


    // ---------------------------------------------------------------------------------------------
    // Motion
    // ---------------------------------------------------------------------------------------------

    /// <summary>Which effects run on expand and collapse.</summary>
    [Parameter] public ExpanderAnimation Animation { get; set; } = ExpanderAnimation.Height | ExpanderAnimation.Fade;

    /// <summary>Edge the content slides in from when <see cref="ExpanderAnimation.Slide"/> is on.</summary>
    [Parameter] public ExpanderSlideFrom SlideFrom { get; set; } = ExpanderSlideFrom.Top;

    /// <summary>Animation length in milliseconds. Zero snaps.</summary>
    [Parameter] public int AnimationDuration { get; set; } = 250;

    /// <summary>CSS timing function for the animation.</summary>
    [Parameter] public string AnimationEasing { get; set; } = "cubic-bezier(0.2, 0, 0, 1)";

    /// <summary>Whether the content is revealed below the header or above it.</summary>
    [Parameter] public ExpandDirection ExpandDirection { get; set; } = ExpandDirection.Down;


    // ---------------------------------------------------------------------------------------------
    // Chrome
    // ---------------------------------------------------------------------------------------------

    /// <summary>Outline colour, as CSS. Unset falls back to the <c>--shiny-color-outline-variant</c> token.</summary>
    [Parameter] public string? BorderColor { get; set; }

    /// <summary>Outline width, as CSS. Unset falls back to the <c>--shiny-border-thin</c> token.</summary>
    [Parameter] public string? BorderThickness { get; set; }

    /// <summary>Corner radius, as CSS. Unset falls back to the <c>--shiny-shape-corner-medium</c> token.</summary>
    [Parameter] public string? CornerRadius { get; set; }

    /// <summary>Lift the expander off the page with the theme's level-1 elevation.</summary>
    [Parameter] public bool HasShadow { get; set; }

    /// <summary>Header fill, as CSS.</summary>
    [Parameter] public string? HeaderBackground { get; set; }

    /// <summary>Header text colour, as CSS.</summary>
    [Parameter] public string? HeaderTextColor { get; set; }

    /// <summary>Colour of <see cref="HeaderDetail"/>, as CSS.</summary>
    [Parameter] public string? HeaderDetailColor { get; set; }

    /// <summary>Header font size, as CSS.</summary>
    [Parameter] public string? HeaderFontSize { get; set; }

    /// <summary>Header padding. Bare numbers are read as pixels, so <c>"12 16"</c> works.</summary>
    [Parameter] public string? HeaderPadding { get; set; }

    /// <summary>Minimum header height, as CSS. Unset falls back to the theme touch target.</summary>
    [Parameter] public string? HeaderHeight { get; set; }

    /// <summary>Content fill, as CSS.</summary>
    [Parameter] public string? ContentBackground { get; set; }

    /// <summary>Content padding. Bare numbers are read as pixels.</summary>
    [Parameter] public string? ContentPadding { get; set; }

    /// <summary>Rotate one glyph, swap two, or show none at all.</summary>
    [Parameter] public ExpanderIndicatorMode IndicatorMode { get; set; } = ExpanderIndicatorMode.Rotate;

    /// <summary>Leading or trailing edge of the header.</summary>
    [Parameter] public ExpanderIndicatorPosition IndicatorPosition { get; set; } = ExpanderIndicatorPosition.End;

    /// <summary>
    /// Glyph shown when collapsed — and, under <see cref="ExpanderIndicatorMode.Rotate"/>, the only glyph.
    /// Defaults to ▶ carrying U+FE0E, the text-presentation selector: without it, WebKit on iOS draws
    /// U+25B6 as the glossy blue play-button emoji.
    /// </summary>
    [Parameter] public string CollapsedIcon { get; set; } = "\u25B6\uFE0E";

    /// <summary>Glyph shown when expanded under <see cref="ExpanderIndicatorMode.Swap"/>.</summary>
    [Parameter] public string ExpandedIcon { get; set; } = "\u25BC\uFE0E";

    /// <summary>Glyph colour, as CSS.</summary>
    [Parameter] public string? IndicatorColor { get; set; }

    /// <summary>Glyph size, as CSS.</summary>
    [Parameter] public string? IndicatorSize { get; set; }

    /// <summary>Draw a hairline between the header and the content.</summary>
    [Parameter] public bool ShowSeparator { get; set; } = true;

    /// <summary>Separator colour, as CSS.</summary>
    [Parameter] public string? SeparatorColor { get; set; }

    /// <summary>Extra classes for the root element.</summary>
    [Parameter] public string? CssClass { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public IDictionary<string, object>? AdditionalAttributes { get; set; }

    IDictionary<string, object>? ExtraAttributes { get; set; }
    string? UserClass { get; set; }
    string? UserStyle { get; set; }


    // ---------------------------------------------------------------------------------------------
    // Public surface
    // ---------------------------------------------------------------------------------------------

    /// <summary>Open the expander.</summary>
    public Task ExpandAsync() => this.SetExpandedAsync(true);

    /// <summary>Close the expander.</summary>
    public Task CollapseAsync() => this.SetExpandedAsync(false);

    /// <summary>Flip the expander between open and closed.</summary>
    public Task ToggleAsync() => this.SetExpandedAsync(!this.IsExpanded);

    /// <summary>Change state without telling the accordion — it is the accordion doing the telling.</summary>
    internal void SetExpandedFromHost(bool expanded)
    {
        if (this.IsExpanded == expanded)
            return;

        this.IsExpanded = expanded;
        if (expanded)
            this.contentRealized = true;

        _ = this.IsExpandedChanged.InvokeAsync(expanded);
        this.Repaint();
    }

    /// <summary>Set by the accordion when closing this item would leave nothing open.</summary>
    internal void SetCanCollapse(bool value)
    {
        if (this.CanCollapse == value)
            return;

        this.CanCollapse = value;
        this.Repaint();
    }

    /// <summary>
    /// Re-render, but only once there is something to re-render. The accordion pushes state onto its
    /// items while they are still registering — during its own first render pass — and asking for a
    /// repaint before the render handle exists throws.
    /// </summary>
    void Repaint()
    {
        if (this.hasRendered)
            this.StateHasChanged();
    }


    // ---------------------------------------------------------------------------------------------
    // Lifecycle
    // ---------------------------------------------------------------------------------------------

    public override Task SetParametersAsync(ParameterView parameters)
    {
        foreach (var parameter in parameters)
            this.explicitParameters.Add(parameter.Name);

        return base.SetParametersAsync(parameters);
    }


    protected override void OnInitialized()
    {
        if (this.AccordionHost is not null)
        {
            this.AccordionHost.Register(this);
            this.registered = true;
        }
    }


    protected override void OnParametersSet()
    {
        this.ExtraAttributes = LayoutAttributes.Split(this.AdditionalAttributes, out var userClass, out var userStyle);
        this.UserClass = userClass;
        this.UserStyle = userStyle;

        if (this.IsExpanded || !this.EffectiveLoadContentOnDemand)
            this.contentRealized = true;
    }


    protected override void OnAfterRender(bool firstRender)
    {
        if (firstRender)
            this.hasRendered = true;
    }


    public void Dispose()
    {
        if (this.registered)
            this.AccordionHost?.Unregister(this);
    }


    // ---------------------------------------------------------------------------------------------
    // Interaction
    // ---------------------------------------------------------------------------------------------

    Task OnHeaderActivated()
    {
        if (!this.IsToggleEnabled)
            return Task.CompletedTask;

        // CanCollapse is an activation guard only. Code that calls CollapseAsync outright still wins,
        // which is what lets an accordion re-point "the one that must stay open" at another item.
        if (this.IsExpanded && !this.CanCollapse)
            return Task.CompletedTask;

        return this.ToggleAsync();
    }


    Task OnHeaderKeyDown(KeyboardEventArgs args)
        => args.Key is "Enter" or " " or "Spacebar" ? this.OnHeaderActivated() : Task.CompletedTask;


    async Task SetExpandedAsync(bool expanded)
    {
        // The accordion gets to veto, and to close whatever else is open first, before this item moves.
        if (this.AccordionHost is not null)
            expanded = this.AccordionHost.RequestExpandedChange(this, expanded);

        if (this.IsExpanded == expanded)
            return;

        this.IsExpanded = expanded;
        if (expanded)
            this.contentRealized = true;

        await this.IsExpandedChanged.InvokeAsync(expanded);

        if (expanded)
            await this.OnExpanded.InvokeAsync();
        else
            await this.OnCollapsed.InvokeAsync();

        this.AccordionHost?.NotifyExpandedChanged(this, expanded);
    }


    // ---------------------------------------------------------------------------------------------
    // Accordion defaults
    // ---------------------------------------------------------------------------------------------

    bool WasSet(string name) => this.explicitParameters.Contains(name);

    AccordionDefaults? Defaults => this.AccordionHost?.Defaults;

    // The accordion's LoadContentOnDemand reaches markup children too, not only the Items it generates.
    bool EffectiveLoadContentOnDemand
        => this.WasSet(nameof(this.LoadContentOnDemand)) || this.Defaults is null ? this.LoadContentOnDemand : this.Defaults.LoadContentOnDemand;

    ExpanderAnimation EffectiveAnimation
        => this.WasSet(nameof(this.Animation)) || this.Defaults is null ? this.Animation : this.Defaults.Animation;

    ExpanderSlideFrom EffectiveSlideFrom
        => this.WasSet(nameof(this.SlideFrom)) || this.Defaults is null ? this.SlideFrom : this.Defaults.SlideFrom;

    int EffectiveDuration
        => this.WasSet(nameof(this.AnimationDuration)) || this.Defaults is null ? this.AnimationDuration : this.Defaults.AnimationDuration;

    string EffectiveEasing
        => this.WasSet(nameof(this.AnimationEasing)) || this.Defaults is null ? this.AnimationEasing : this.Defaults.AnimationEasing;

    ExpandDirection EffectiveDirection
        => this.WasSet(nameof(this.ExpandDirection)) || this.Defaults is null ? this.ExpandDirection : this.Defaults.ExpandDirection;

    ExpanderIndicatorMode EffectiveIndicatorMode
        => this.WasSet(nameof(this.IndicatorMode)) || this.Defaults is null ? this.IndicatorMode : this.Defaults.IndicatorMode;

    ExpanderIndicatorPosition EffectiveIndicatorPosition
        => this.WasSet(nameof(this.IndicatorPosition)) || this.Defaults is null ? this.IndicatorPosition : this.Defaults.IndicatorPosition;

    string? Inherited(string name, string? own, Func<AccordionDefaults, string?> fromHost)
        => this.WasSet(name) || this.Defaults is null ? own : fromHost(this.Defaults) ?? own;


    // ---------------------------------------------------------------------------------------------
    // Rendering
    // ---------------------------------------------------------------------------------------------

    string StateClasses
    {
        get
        {
            var animation = this.EffectiveAnimation;
            var builder = new StringBuilder();

            builder.Append(this.IsExpanded ? "is-expanded" : "is-collapsed");
            builder.Append(this.EffectiveDirection == ExpandDirection.Up ? " dir-up" : " dir-down");

            if (this.EffectiveDuration > 0 && animation.HasFlag(ExpanderAnimation.Height))
                builder.Append(" anim-height");

            if (this.EffectiveDuration > 0 && animation.HasFlag(ExpanderAnimation.Fade))
                builder.Append(" anim-fade");

            if (this.EffectiveDuration > 0 && animation.HasFlag(ExpanderAnimation.Slide))
            {
                builder.Append(this.EffectiveSlideFrom switch
                {
                    ExpanderSlideFrom.Bottom => " anim-slide slide-bottom",
                    ExpanderSlideFrom.Left => " anim-slide slide-left",
                    ExpanderSlideFrom.Right => " anim-slide slide-right",
                    _ => " anim-slide slide-top"
                });
            }

            builder.Append(this.EffectiveIndicatorMode switch
            {
                ExpanderIndicatorMode.Rotate => " ind-rotate",
                ExpanderIndicatorMode.Swap => " ind-swap",
                _ => " ind-none"
            });

            builder.Append(this.EffectiveIndicatorPosition == ExpanderIndicatorPosition.Start ? " ind-start" : " ind-end");

            if (this.HasShadow)
                builder.Append(" has-shadow");

            if (!this.IsToggleEnabled)
                builder.Append(" is-locked");

            return builder.ToString();
        }
    }


    string RootStyle
    {
        get
        {
            var builder = new StringBuilder();
            builder.Append("--shiny-expander-duration:").Append(Math.Max(0, this.EffectiveDuration)).Append("ms;");
            builder.Append("--shiny-expander-easing:").Append(this.EffectiveEasing).Append(';');

            Add("--shiny-expander-border-color", this.Inherited(nameof(this.BorderColor), this.BorderColor, d => d.BorderColor));
            Add("--shiny-expander-border-width", this.Inherited(nameof(this.BorderThickness), this.BorderThickness, d => d.BorderThickness));
            Add("--shiny-expander-radius", this.Inherited(nameof(this.CornerRadius), this.CornerRadius, d => d.CornerRadius));
            Add("--shiny-expander-header-bg", this.Inherited(nameof(this.HeaderBackground), this.HeaderBackground, d => d.HeaderBackground));
            Add("--shiny-expander-content-bg", this.Inherited(nameof(this.ContentBackground), this.ContentBackground, d => d.ContentBackground));
            Add("--shiny-expander-header-color", this.HeaderTextColor);
            Add("--shiny-expander-detail-color", this.HeaderDetailColor);
            Add("--shiny-expander-header-font-size", this.HeaderFontSize);
            Add("--shiny-expander-header-padding", LayoutAttributes.Spacing(this.HeaderPadding));
            Add("--shiny-expander-header-min-height", this.HeaderHeight);
            Add("--shiny-expander-content-padding", LayoutAttributes.Spacing(this.ContentPadding));
            Add("--shiny-expander-indicator-color", this.IndicatorColor);
            Add("--shiny-expander-indicator-size", this.IndicatorSize);
            Add("--shiny-expander-separator-color", this.SeparatorColor);

            return LayoutAttributes.Append(builder.ToString(), this.UserStyle);

            void Add(string name, string? value)
            {
                if (!String.IsNullOrWhiteSpace(value))
                    builder.Append(name).Append(':').Append(value).Append(';');
            }
        }
    }
}
