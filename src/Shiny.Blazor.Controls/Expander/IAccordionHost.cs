namespace Shiny.Blazor.Controls;

/// <summary>
/// What an <see cref="Expander"/> talks to when it is inside an <see cref="Accordion"/>. Cascaded
/// rather than passed, so an expander nested a few layers down in someone's own markup still finds
/// the accordion it belongs to.
/// </summary>
public interface IAccordionHost
{
    /// <summary>Called from the expander's <c>OnInitialized</c>.</summary>
    void Register(Expander item);

    /// <summary>Called from the expander's <c>Dispose</c>.</summary>
    void Unregister(Expander item);

    /// <summary>
    /// Ask the accordion whether this item may change state, and let it close the others first.
    /// Returns the state the item should actually end up in.
    /// </summary>
    bool RequestExpandedChange(Expander item, bool expanded);

    /// <summary>Called by the item once it has actually changed, so the accordion can re-apply its rules.</summary>
    void NotifyExpandedChanged(Expander item, bool expanded);

    /// <summary>Defaults the accordion pushes onto an item that has not set them itself.</summary>
    AccordionDefaults Defaults { get; }
}


/// <summary>Motion and chrome an <see cref="Accordion"/> seeds its items with.</summary>
/// <param name="Animation">Default animation flags.</param>
/// <param name="SlideFrom">Default slide edge.</param>
/// <param name="AnimationDuration">Default duration, in milliseconds.</param>
/// <param name="AnimationEasing">Default CSS timing function.</param>
/// <param name="ExpandDirection">Default reveal direction.</param>
/// <param name="IndicatorMode">Default indicator behaviour.</param>
/// <param name="IndicatorPosition">Default indicator side.</param>
/// <param name="BorderColor">Default outline colour, as CSS.</param>
/// <param name="BorderThickness">Default outline width, as CSS.</param>
/// <param name="CornerRadius">Default corner radius, as CSS.</param>
/// <param name="HeaderBackground">Default header fill, as CSS.</param>
/// <param name="ContentBackground">Default content fill, as CSS.</param>
/// <param name="LoadContentOnDemand">Default <see cref="Expander.LoadContentOnDemand"/>.</param>
public record AccordionDefaults(
    ExpanderAnimation Animation,
    ExpanderSlideFrom SlideFrom,
    int AnimationDuration,
    string AnimationEasing,
    ExpandDirection ExpandDirection,
    ExpanderIndicatorMode IndicatorMode,
    ExpanderIndicatorPosition IndicatorPosition,
    string? BorderColor,
    string? BorderThickness,
    string? CornerRadius,
    string? HeaderBackground,
    string? ContentBackground,
    bool LoadContentOnDemand = false
);
