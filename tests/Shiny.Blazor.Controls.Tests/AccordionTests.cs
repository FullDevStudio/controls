using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shiny.Blazor.Controls;
using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

/// <summary>
/// The accordion's rules run entirely in the component, not the renderer, so they can be driven
/// directly: register a few expanders against the host interface and toggle them.
/// </summary>
public class AccordionTests
{
    static (Accordion Accordion, IAccordionHost Host, List<Expander> Items) Build(
        AccordionSelectionMode mode = AccordionSelectionMode.Single,
        bool allowCollapseAll = true,
        int count = 3
    )
    {
        var accordion = new Accordion { SelectionMode = mode, AllowCollapseAll = allowCollapseAll };
        var host = (IAccordionHost)accordion;
        var items = new List<Expander>();

        for (var i = 0; i < count; i++)
        {
            var item = new Expander { AccordionHost = accordion, HeaderText = "Item " + i };
            host.Register(item);
            items.Add(item);
        }

        return (accordion, host, items);
    }


    [Fact]
    public async Task SingleModeClosesWhateverElseWasOpen()
    {
        var (_, _, items) = Build();

        await items[0].ExpandAsync();
        await items[2].ExpandAsync();

        items[0].IsExpanded.ShouldBeFalse();
        items[1].IsExpanded.ShouldBeFalse();
        items[2].IsExpanded.ShouldBeTrue();
    }


    [Fact]
    public async Task MultipleModeLeavesTheOthersAlone()
    {
        var (accordion, _, items) = Build(AccordionSelectionMode.Multiple);

        await items[0].ExpandAsync();
        await items[2].ExpandAsync();

        accordion.ExpandedIndexes.ShouldBe([0, 2]);
    }


    [Fact]
    public async Task ExpandedIndexFollowsWhicheverIsOpen()
    {
        var (accordion, _, items) = Build();

        await items[1].ExpandAsync();
        accordion.ExpandedIndex.ShouldBe(1);

        await items[1].CollapseAsync();
        accordion.ExpandedIndex.ShouldBe(-1);
    }


    [Fact]
    public async Task RefusingToCollapseAllKeepsTheLastOneOpen()
    {
        var (_, _, items) = Build(allowCollapseAll: false);

        await items[1].ExpandAsync();
        await items[1].CollapseAsync();

        items[1].IsExpanded.ShouldBeTrue();
    }


    [Fact]
    public async Task TheLastOpenItemLosesItsCollapseAffordance()
    {
        var (_, _, items) = Build(allowCollapseAll: false);

        await items[1].ExpandAsync();

        items[1].CanCollapse.ShouldBeFalse();
        items[0].CanCollapse.ShouldBeTrue();
    }


    [Fact]
    public async Task CollapsingIsFineWhenSomethingElseIsStillOpen()
    {
        var (_, _, items) = Build(AccordionSelectionMode.Multiple, allowCollapseAll: false);

        await items[0].ExpandAsync();
        await items[1].ExpandAsync();

        items[0].CanCollapse.ShouldBeTrue();

        await items[0].CollapseAsync();
        items[0].IsExpanded.ShouldBeFalse();
    }


    [Fact]
    public void ExpandItemOpensByIndex()
    {
        var (accordion, _, items) = Build();

        accordion.ExpandItem(1).ShouldBeTrue();
        items[1].IsExpanded.ShouldBeTrue();

        accordion.ExpandItem(99).ShouldBeFalse();
    }


    [Fact]
    public void ExpandAllIsIgnoredInSingleMode()
    {
        var (accordion, _, _) = Build();

        accordion.ExpandAll();

        accordion.ExpandedIndexes.ShouldBeEmpty();
    }


    [Fact]
    public void ExpandAllOpensEverythingInMultipleMode()
    {
        var (accordion, _, _) = Build(AccordionSelectionMode.Multiple);

        accordion.ExpandAll();

        accordion.ExpandedIndexes.ShouldBe([0, 1, 2]);
    }


    [Fact]
    public async Task ItemEventsCarryTheIndexAndTheModel()
    {
        AccordionItemChangedEventArgs? seen = null;
        var accordion = new Accordion
        {
            OnItemExpanded = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<AccordionItemChangedEventArgs>(
                new object(), a => seen = a
            )
        };
        var host = (IAccordionHost)accordion;

        var first = new Expander { AccordionHost = accordion, Item = "Alpha" };
        var second = new Expander { AccordionHost = accordion, Item = "Beta" };
        host.Register(first);
        host.Register(second);

        await second.ExpandAsync();

        seen.ShouldNotBeNull();
        seen.Index.ShouldBe(1);
        seen.Data.ShouldBe("Beta");
        seen.IsExpanded.ShouldBeTrue();
    }


    [Fact]
    public async Task ClosingTheOthersDoesNotEchoAsItemEvents()
    {
        var collapses = 0;
        var accordion = new Accordion
        {
            OnItemCollapsed = Microsoft.AspNetCore.Components.EventCallback.Factory.Create<AccordionItemChangedEventArgs>(
                new object(), _ => collapses++
            )
        };
        var host = (IAccordionHost)accordion;

        var first = new Expander { AccordionHost = accordion };
        var second = new Expander { AccordionHost = accordion };
        host.Register(first);
        host.Register(second);

        await first.ExpandAsync();
        await second.ExpandAsync();

        // First closing is a consequence of second opening, not an event of its own.
        collapses.ShouldBe(0);
    }


    [Fact]
    public async Task UnregisteringDropsTheItem()
    {
        var (accordion, host, items) = Build();

        await items[1].ExpandAsync();
        host.Unregister(items[1]);

        accordion.ItemViews.Count.ShouldBe(2);
        accordion.ItemViews.ShouldNotContain(items[1]);
    }


    // ---------------------------------------------------------------------------------------------
    // Rendered: parameters that change after the first render, and markup children
    // ---------------------------------------------------------------------------------------------

    sealed class Holder
    {
        public MarkupHost? Host;
    }

    sealed class MarkupHost : ComponentBase
    {
        [Parameter] public bool AllowCollapseAll { get; set; } = true;
        [Parameter] public bool LoadContentOnDemand { get; set; }
        [Parameter] public Holder Holder { get; set; } = default!;

        public Accordion? Accordion { get; private set; }

        public void Rerender() => this.StateHasChanged();

        protected override void OnInitialized() => this.Holder.Host = this;

        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenComponent<Accordion>(0);
            b.AddAttribute(1, nameof(Accordion.AllowCollapseAll), this.AllowCollapseAll);
            b.AddAttribute(2, nameof(Accordion.LoadContentOnDemand), this.LoadContentOnDemand);
            b.AddAttribute(3, nameof(Accordion.ChildContent), (RenderFragment)(c =>
            {
                // a @foreach of markup children, not Items
                for (var i = 0; i < 3; i++)
                {
                    var body = "BODY-" + i;
                    c.OpenComponent<Expander>(10);
                    c.SetKey(i);
                    c.AddAttribute(11, nameof(Expander.HeaderText), "Item " + i);
                    c.AddAttribute(12, nameof(Expander.ChildContent), (RenderFragment)(x => x.AddContent(0, body)));
                    c.CloseComponent();
                }
            }));
            b.AddComponentReferenceCapture(4, r => this.Accordion = (Accordion)r);
            b.CloseComponent();
        }
    }

    static async Task<(HtmlRenderer Renderer, HtmlRootComponent Root, MarkupHost Host)> RenderHostAsync(bool loadContentOnDemand = false)
    {
        var renderer = new HtmlRenderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance);
        var holder = new Holder();
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<MarkupHost>(
            ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(MarkupHost.Holder)] = holder,
                [nameof(MarkupHost.LoadContentOnDemand)] = loadContentOnDemand
            })));
        return (renderer, root, holder.Host!);
    }


    [Fact]
    public async Task TurningAllowCollapseAllOffOnAClosedListOpensTheFirstItem()
    {
        var (renderer, _, host) = await RenderHostAsync();
        await using var _ = renderer;

        host.Accordion!.ExpandedIndex.ShouldBe(-1);

        await renderer.Dispatcher.InvokeAsync(() =>
        {
            host.AllowCollapseAll = false;
            host.Rerender();
        });

        host.Accordion.ExpandedIndex.ShouldBe(0);
        host.Accordion.ItemViews[0].IsExpanded.ShouldBeTrue();
    }


    [Fact]
    public async Task LoadContentOnDemandReachesMarkupChildren()
    {
        var (renderer, root, _) = await RenderHostAsync(loadContentOnDemand: true);
        await using var _r = renderer;

        var html = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());
        html.ShouldContain("Item 1");
        html.ShouldNotContain("BODY-1");
    }


    [Fact]
    public async Task MarkupChildrenRenderTheirContentUpFrontByDefault()
    {
        var (renderer, root, _) = await RenderHostAsync();
        await using var _r = renderer;

        var html = await renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString());
        html.ShouldContain("BODY-1");
    }
}
