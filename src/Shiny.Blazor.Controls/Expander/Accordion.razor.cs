using System.Collections;
using Microsoft.AspNetCore.Components;

namespace Shiny.Blazor.Controls;

/// <summary>One <see cref="Expander"/> in an <see cref="Accordion"/> changing state.</summary>
/// <param name="Item">The expander that changed.</param>
/// <param name="Data">The <c>Items</c> element behind it, or null when it was written in markup without one.</param>
/// <param name="Index">Its position among the accordion's expanders.</param>
/// <param name="IsExpanded">The state it has just settled into.</param>
public record AccordionItemChangedEventArgs(Expander Item, object? Data, int Index, bool IsExpanded);


/// <summary>
/// A stack of <see cref="Expander"/>s that agree on how many of them may be open.
/// </summary>
/// <remarks>
/// Expanders can be written out one by one, generated from <see cref="Items"/>, or both. The motion
/// and chrome parameters here are <em>defaults</em>: an expander that sets the same parameter itself
/// keeps its own value, so one odd item in the list stays odd.
/// <para>
/// For data-driven lists a plain <c>@foreach</c> of <c>&lt;Expander&gt;</c> inside the accordion is
/// usually nicer than <see cref="Items"/> — the models stay strongly typed. <see cref="Items"/> is
/// there for when the shape is only known at runtime.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// &lt;Accordion SelectionMode="AccordionSelectionMode.Single" AllowCollapseAll="false"&gt;
///     &lt;Expander HeaderText="Account"&gt;…&lt;/Expander&gt;
///     &lt;Expander HeaderText="Billing"&gt;…&lt;/Expander&gt;
/// &lt;/Accordion&gt;
/// </code>
/// </example>
public partial class Accordion : IAccordionHost
{
    readonly List<Expander> items = new();

    bool syncing;
    bool hasRendered;
    bool? appliedAllowCollapseAll;
    int publishedIndex = -1;

    /// <summary>Expanders written straight into the accordion.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Data to generate an expander per element from.</summary>
    [Parameter] public IEnumerable? Items { get; set; }

    /// <summary>Header for each generated expander. Without one the element's <c>ToString()</c> is the title.</summary>
    [Parameter] public RenderFragment<object>? ItemHeader { get; set; }

    /// <summary>Content for each generated expander.</summary>
    [Parameter] public RenderFragment<object>? ItemContent { get; set; }

    /// <summary>One open at a time, or as many as the user likes. Defaults to <see cref="AccordionSelectionMode.Single"/>.</summary>
    [Parameter] public AccordionSelectionMode SelectionMode { get; set; } = AccordionSelectionMode.Single;

    /// <summary>
    /// When false the accordion refuses to end up with nothing open: the last open item stops
    /// responding to clicks, and a list that starts fully closed opens its first item.
    /// </summary>
    [Parameter] public bool AllowCollapseAll { get; set; } = true;

    /// <summary>Index of the open item, or -1 for none. In <see cref="AccordionSelectionMode.Multiple"/> it reports the first.</summary>
    [Parameter] public int ExpandedIndex { get; set; } = -1;

    /// <summary>Two-way binding hook for <see cref="ExpandedIndex"/>.</summary>
    [Parameter] public EventCallback<int> ExpandedIndexChanged { get; set; }

    /// <summary>Raised when an item opens.</summary>
    [Parameter] public EventCallback<AccordionItemChangedEventArgs> OnItemExpanded { get; set; }

    /// <summary>Raised when an item closes.</summary>
    [Parameter] public EventCallback<AccordionItemChangedEventArgs> OnItemCollapsed { get; set; }

    /// <summary>
    /// Hold each item's content out of the DOM until it is first opened. Reaches markup children
    /// (a <c>@foreach</c> of <c>&lt;Expander&gt;</c>) as well as <see cref="Items"/>; an expander that
    /// sets <see cref="Expander.LoadContentOnDemand"/> itself keeps its own value.
    /// </summary>
    [Parameter] public bool LoadContentOnDemand { get; set; }

    /// <summary>Gap between items, as CSS. Bare numbers are read as pixels.</summary>
    [Parameter] public string? Spacing { get; set; }


    // -- pass-through defaults ---------------------------------------------------------------------

    /// <summary>Default <see cref="Expander.Animation"/> for every item.</summary>
    [Parameter] public ExpanderAnimation Animation { get; set; } = ExpanderAnimation.Height | ExpanderAnimation.Fade;

    /// <summary>Default <see cref="Expander.SlideFrom"/> for every item.</summary>
    [Parameter] public ExpanderSlideFrom SlideFrom { get; set; } = ExpanderSlideFrom.Top;

    /// <summary>Default <see cref="Expander.AnimationDuration"/> for every item.</summary>
    [Parameter] public int AnimationDuration { get; set; } = 250;

    /// <summary>Default <see cref="Expander.AnimationEasing"/> for every item.</summary>
    [Parameter] public string AnimationEasing { get; set; } = "cubic-bezier(0.2, 0, 0, 1)";

    /// <summary>Default <see cref="Expander.ExpandDirection"/> for every item.</summary>
    [Parameter] public ExpandDirection ExpandDirection { get; set; } = ExpandDirection.Down;

    /// <summary>Default <see cref="Expander.IndicatorMode"/> for every item.</summary>
    [Parameter] public ExpanderIndicatorMode IndicatorMode { get; set; } = ExpanderIndicatorMode.Rotate;

    /// <summary>Default <see cref="Expander.IndicatorPosition"/> for every item.</summary>
    [Parameter] public ExpanderIndicatorPosition IndicatorPosition { get; set; } = ExpanderIndicatorPosition.End;

    /// <summary>Default <see cref="Expander.BorderColor"/> for every item.</summary>
    [Parameter] public string? BorderColor { get; set; }

    /// <summary>Default <see cref="Expander.BorderThickness"/> for every item.</summary>
    [Parameter] public string? BorderThickness { get; set; }

    /// <summary>Default <see cref="Expander.CornerRadius"/> for every item.</summary>
    [Parameter] public string? CornerRadius { get; set; }

    /// <summary>Default <see cref="Expander.HeaderBackground"/> for every item.</summary>
    [Parameter] public string? HeaderBackground { get; set; }

    /// <summary>Default <see cref="Expander.ContentBackground"/> for every item.</summary>
    [Parameter] public string? ContentBackground { get; set; }

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

    /// <summary>The expanders in this accordion, in registration (visual) order.</summary>
    public IReadOnlyList<Expander> ItemViews => this.items;

    /// <summary>Indexes of every open item.</summary>
    public IReadOnlyList<int> ExpandedIndexes
    {
        get
        {
            var result = new List<int>();
            for (var i = 0; i < this.items.Count; i++)
            {
                if (this.items[i].IsExpanded)
                    result.Add(i);
            }
            return result;
        }
    }

    /// <summary>Open every item. Does nothing in <see cref="AccordionSelectionMode.Single"/>.</summary>
    public void ExpandAll()
    {
        if (this.SelectionMode == AccordionSelectionMode.Single)
            return;

        foreach (var item in this.items)
            item.SetExpandedFromHost(true);

        this.AfterChange();
    }

    /// <summary>Close every item — unless <see cref="AllowCollapseAll"/> is false, which leaves the first one open.</summary>
    public void CollapseAll()
    {
        foreach (var item in this.items)
            item.SetExpandedFromHost(false);

        this.AfterChange();
    }

    /// <summary>Open the item at <paramref name="index"/>. Returns false when out of range.</summary>
    public bool ExpandItem(int index)
    {
        if (index < 0 || index >= this.items.Count)
            return false;

        var target = this.items[index];
        if (this.SelectionMode == AccordionSelectionMode.Single)
        {
            foreach (var item in this.items)
            {
                if (!ReferenceEquals(item, target))
                    item.SetExpandedFromHost(false);
            }
        }

        target.SetExpandedFromHost(true);
        this.AfterChange();
        return true;
    }


    // ---------------------------------------------------------------------------------------------
    // Host
    // ---------------------------------------------------------------------------------------------

    AccordionDefaults IAccordionHost.Defaults => new(
        this.Animation,
        this.SlideFrom,
        this.AnimationDuration,
        this.AnimationEasing,
        this.ExpandDirection,
        this.IndicatorMode,
        this.IndicatorPosition,
        this.BorderColor,
        this.BorderThickness,
        this.CornerRadius,
        this.HeaderBackground,
        this.ContentBackground,
        this.LoadContentOnDemand
    );


    void IAccordionHost.Register(Expander item)
    {
        if (this.items.Contains(item))
            return;

        this.items.Add(item);

        // Registration happens while this component is already rendering its children, so anything
        // the new item changes cannot be shown in this pass - queue another one. Before the first
        // render there is no handle to queue against, and none is needed: OnAfterRender applies the
        // rules once the whole set has registered.
        this.QueueRules();
    }


    void IAccordionHost.Unregister(Expander item)
    {
        if (!this.items.Remove(item))
            return;

        this.QueueRules();
    }


    bool IAccordionHost.RequestExpandedChange(Expander item, bool expanded)
    {
        if (this.syncing)
            return expanded;

        // Closing the last open one when the accordion is not allowed to be empty: refuse, and leave
        // the item where it is.
        if (!expanded && !this.AllowCollapseAll && this.items.Count(x => x.IsExpanded) <= 1 && item.IsExpanded)
            return true;

        if (expanded && this.SelectionMode == AccordionSelectionMode.Single)
        {
            this.syncing = true;
            try
            {
                foreach (var other in this.items)
                {
                    if (!ReferenceEquals(other, item))
                        other.SetExpandedFromHost(false);
                }
            }
            finally
            {
                this.syncing = false;
            }
        }

        return expanded;
    }


    void IAccordionHost.NotifyExpandedChanged(Expander item, bool expanded)
    {
        this.AfterChange();

        var args = new AccordionItemChangedEventArgs(item, item.Item, this.items.IndexOf(item), expanded);

        if (expanded)
            _ = this.OnItemExpanded.InvokeAsync(args);
        else
            _ = this.OnItemCollapsed.InvokeAsync(args);

        if (this.hasRendered)
            this.StateHasChanged();
    }


    // ---------------------------------------------------------------------------------------------
    // Rules
    // ---------------------------------------------------------------------------------------------

    protected override void OnParametersSet()
    {
        this.ExtraAttributes = LayoutAttributes.Split(this.AdditionalAttributes, out var userClass, out var userStyle);
        this.UserClass = userClass;
        this.UserStyle = userStyle;

        // A bound ExpandedIndex that no longer matches what we last published is the caller driving
        // the accordion from their own state, so follow it.
        if (this.ExpandedIndex != this.publishedIndex)
        {
            if (this.ExpandedIndex < 0)
                this.CollapseAll();
            else
                this.ExpandItem(this.ExpandedIndex);
        }
        else if (this.appliedAllowCollapseAll is { } applied && applied != this.AllowCollapseAll)
        {
            // Turning AllowCollapseAll off at runtime on a fully closed list has to open the first
            // item (and turning it back on gives the open item its close affordance back) - the rules
            // otherwise only ran on first render and on item changes.
            this.AfterChange();
        }

        this.appliedAllowCollapseAll = this.AllowCollapseAll;
    }


    protected override void OnAfterRender(bool firstRender)
    {
        if (!firstRender)
            return;

        this.hasRendered = true;
        this.AfterChange();
    }


    void QueueRules()
    {
        if (!this.hasRendered)
            return;

        _ = this.InvokeAsync(() =>
        {
            this.AfterChange();
            this.StateHasChanged();
        });
    }


    /// <summary>Bring the items back in line with the rules, then republish <see cref="ExpandedIndex"/>.</summary>
    void AfterChange()
    {
        if (this.items.Count == 0)
            return;

        if (!this.AllowCollapseAll && !this.items.Any(x => x.IsExpanded))
            this.items[0].SetExpandedFromHost(true);

        // The open item loses its close affordance only when closing it would leave nothing open.
        var openCount = this.items.Count(x => x.IsExpanded);
        foreach (var item in this.items)
            item.SetCanCollapse(this.AllowCollapseAll || !item.IsExpanded || openCount > 1);

        var index = -1;
        for (var i = 0; i < this.items.Count; i++)
        {
            if (this.items[i].IsExpanded)
            {
                index = i;
                break;
            }
        }

        this.publishedIndex = index;
        if (this.ExpandedIndex == index)
            return;

        this.ExpandedIndex = index;
        _ = this.ExpandedIndexChanged.InvokeAsync(index);
    }


    string RootStyle
    {
        get
        {
            var spacing = LayoutAttributes.Spacing(this.Spacing);
            var style = String.IsNullOrWhiteSpace(spacing) ? String.Empty : $"--shiny-accordion-gap:{spacing};";
            return LayoutAttributes.Append(style, this.UserStyle);
        }
    }
}
