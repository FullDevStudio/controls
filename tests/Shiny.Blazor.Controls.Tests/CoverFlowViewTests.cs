using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

/// <summary>
/// The engine in cover-flow.js owns the motion; what the component decides is which slides exist,
/// which real item a virtual (looping) index is, and how the engine's reports move CurrentPosition.
/// </summary>
public class CoverFlowViewTests
{
    static readonly string[] Twenty = Enumerable.Range(0, 20).Select(i => $"Item {i}").ToArray();

    sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => default;
    }

    static async Task<string> RenderAsync(params (string Name, object? Value)[] parameters)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime>(new NoJs());
        var provider = services.BuildServiceProvider();

        await using var renderer = new HtmlRenderer(provider, NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var values = new Dictionary<string, object?>();
            foreach (var (name, value) in parameters)
                values[name] = value;

            var output = await renderer.RenderComponentAsync<CoverFlowView<string>>(ParameterView.FromDictionary(values));
            return System.Net.WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    static int[] RenderedIndices(string html)
        => Regex.Matches(html, @"data-index=""(-?\d+)""").Select(m => int.Parse(m.Groups[1].Value)).ToArray();

    /// <summary>A component wired up the way the renderer would, without a renderer.</summary>
    static CoverFlowView<string> Create(int position = 0, bool loop = false, int count = 20, int visible = 4)
    {
        var view = new CoverFlowView<string>();
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(CoverFlowView<string>.Items)] = Twenty.Take(count).ToArray(),
            [nameof(CoverFlowView<string>.CurrentPosition)] = position,
            [nameof(CoverFlowView<string>.Loop)] = loop,
            [nameof(CoverFlowView<string>.VisibleItems)] = visible
        });
        parameters.SetParameterProperties(view);
        typeof(CoverFlowView<string>)
            .GetMethod("OnParametersSet", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(view, null);
        return view;
    }


    [Fact]
    public async Task OnlyAWindowAroundTheCentreIsRendered()
    {
        var html = await RenderAsync(("Items", Twenty), ("CurrentPosition", 10), ("VisibleItems", 3));

        // VisibleItems + a two-item buffer on each side.
        RenderedIndices(html).ShouldBe(Enumerable.Range(5, 11).ToArray());
    }


    [Fact]
    public async Task TheWindowIsClippedAtTheEndsWhenNotLooping()
    {
        var html = await RenderAsync(("Items", Twenty), ("CurrentPosition", 0));
        RenderedIndices(html).ShouldBe(Enumerable.Range(0, 7).ToArray());
    }


    [Fact]
    public async Task LoopingRendersVirtualIndicesPastTheStart()
    {
        var html = await RenderAsync(("Items", Twenty), ("CurrentPosition", 0), ("Loop", true), ("VisibleItems", 2));

        RenderedIndices(html).ShouldBe([-4, -3, -2, -1, 0, 1, 2, 3, 4]);
        html.ShouldContain("Item 19");
    }


    [Fact]
    public async Task ASmallLoopNeverRepeatsAnItem()
    {
        var html = await RenderAsync(("Items", Twenty.Take(3).ToArray()), ("Loop", true));
        RenderedIndices(html).Length.ShouldBe(3);
    }


    [Fact]
    public async Task EmptyShowsTheEmptyTemplate()
    {
        var html = await RenderAsync(
            ("Items", Array.Empty<string>()),
            ("EmptyTemplate", (RenderFragment)(b => b.AddContent(0, "Nothing here"))));

        html.ShouldContain("Nothing here");
        html.ShouldNotContain("shiny-coverflow-stage");
    }


    [Fact]
    public async Task TheCaptionFollowsTheCurrentItem()
    {
        var html = await RenderAsync(
            ("Items", Twenty),
            ("CurrentPosition", 7),
            ("CaptionTemplate", (RenderFragment<string>)(s => b => b.AddContent(0, $"Caption:{s}"))));

        html.ShouldContain("Caption:Item 7");
    }


    [Fact]
    public async Task ReflectionsRepeatTheTemplateHiddenFromAssistiveTech()
    {
        var html = await RenderAsync(("Items", Twenty.Take(1).ToArray()), ("ShowReflection", true));

        Regex.Matches(html, "Item 0").Count.ShouldBe(2);
        html.ShouldContain(@"class=""shiny-coverflow-reflection"" aria-hidden=""true""");
    }


    [Fact]
    public async Task APositionPastTheEndIsClamped()
    {
        var html = await RenderAsync(("Items", Twenty.Take(5).ToArray()), ("CurrentPosition", 99));
        html.ShouldContain(@"aria-label=""5 of 5"" aria-current=""true""");
    }


    [Fact]
    public void TheStyleIsWrittenInvariantly()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var style = new CoverFlowView<string> { ItemWidth = 180.5, ReflectionOpacity = 0.25 }.RootStyle;
            style.ShouldContain("--coverflow-item-w:180.5px;");
            style.ShouldContain("--coverflow-reflection-opacity:0.25;");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }


    [Theory]
    [InlineData(-1, 19)]
    [InlineData(-20, 0)]
    [InlineData(21, 1)]
    public void VirtualIndicesMapOntoRealItems(int virtualIndex, int real)
        => Create(loop: true).RealIndex(virtualIndex).ShouldBe(real);


    [Fact]
    public void GoingToAnItemWhileLoopingTakesTheShortWayRound()
    {
        var view = Create(position: 1, loop: true);
        view.NearestVirtual(19).ShouldBe(-1);
        view.NearestVirtual(5).ShouldBe(5);
    }


    [Fact]
    public async Task SettlingRaisesCurrentPositionChanged()
    {
        var raised = new List<int>();
        var view = Create(position: 2);
        view.CurrentPositionChanged = EventCallback.Factory.Create<int>(new object(), raised.Add);

        // Not attached to a renderer, so suppress the re-render the callback ends with.
        view.ApplyCenter(5).ShouldBeTrue();
        view.VirtualCenter.ShouldBe(5);

        await InvokeWithoutRender(view, nameof(CoverFlowView<string>.OnSettled), 5);

        view.CurrentPosition.ShouldBe(5);
        raised.ShouldBe([5]);
    }


    [Fact]
    public async Task ALoopingSettleReportsTheRealIndex()
    {
        var raised = new List<int>();
        var view = Create(position: 0, loop: true);
        view.CurrentPositionChanged = EventCallback.Factory.Create<int>(new object(), raised.Add);
        view.ApplyCenter(-2);

        await InvokeWithoutRender(view, nameof(CoverFlowView<string>.OnSettled), -2);

        view.CurrentPosition.ShouldBe(18);
        raised.ShouldBe([18]);
    }


    [Fact]
    public void ACentreOutsideTheListIsClampedWhenNotLooping()
    {
        var view = Create(position: 3);
        view.ApplyCenter(-4).ShouldBeTrue();
        view.VirtualCenter.ShouldBe(0);
        view.ApplyCenter(0).ShouldBeFalse();
    }


    [Fact]
    public async Task EnterSelectsTheCentreItem()
    {
        string? selected = null;
        var view = Create(position: 6);
        view.ItemSelected = EventCallback.Factory.Create<string>(new object(), s => selected = s);

        await (Task)typeof(CoverFlowView<string>)
            .GetMethod("OnKeyDown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(view, [new KeyboardEventArgs { Key = "Enter" }])!;

        selected.ShouldBe("Item 6");
    }


    [Fact]
    public async Task TappingTheCentreRaisesItemSelected()
    {
        string? selected = null;
        var view = Create(position: 0, loop: true);
        view.ItemSelected = EventCallback.Factory.Create<string>(new object(), s => selected = s);

        await view.OnItemClicked(-1);
        selected.ShouldBe("Item 19");
    }


    /// <summary>
    /// Runs a JS callback on a component with no renderer: the callback's state changes are what is
    /// under test, and its closing StateHasChanged would throw on a missing render handle.
    /// </summary>
    static async Task InvokeWithoutRender(CoverFlowView<string> view, string method, int arg)
    {
        try
        {
            await (Task)typeof(CoverFlowView<string>).GetMethod(method)!.Invoke(view, [arg])!;
        }
        catch (InvalidOperationException e) when (e.Message.Contains("render handle"))
        {
        }
    }
}
