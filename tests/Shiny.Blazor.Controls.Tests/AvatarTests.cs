using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

public class AvatarTests
{
    [Theory]
    [InlineData("Allan Ritchie", "AR")]
    [InlineData("allan", "A")]
    [InlineData("  Grace   Brewster Hopper ", "GH")]
    [InlineData("Jean-Luc Picard", "JP")]
    [InlineData("ada.lovelace@example.com", "AL")]
    [InlineData("Émile Zola", "ÉZ")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void InitialsComeFromTheFirstAndLastWords(string? name, string expected)
        => AvatarText.GetInitials(name).ShouldBe(expected);


    /// <summary>
    /// Pinned, and pinned to the same values in the MAUI tests: the same person has to get the same
    /// colour in a web app and its MAUI twin.
    /// </summary>
    [Theory]
    [InlineData("Allan Ritchie", 53)]
    [InlineData("allan ritchie", 53)]
    [InlineData("Grace Hopper", 147)]
    public void TheHueIsStableAndIgnoresCase(string name, int hue)
        => AvatarText.GetHue(name).ShouldBe(hue);


    [Fact]
    public void ExplicitInitialsWin()
        => new Avatar { Name = "Allan Ritchie", Initials = "🦄" }.DisplayInitials.ShouldBe("🦄");


    [Fact]
    public void TheStyleIsWrittenInvariantly()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            var style = new Avatar { Size = 48.5, RingThickness = 1.5 }.RootStyle;

            style.ShouldContain("--shiny-avatar-size:48.5px;");
            style.ShouldContain("--shiny-avatar-ring-width:1.5px;");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }


    [Fact]
    public void UnsetParametersLeaveEverythingToTheStylesheet()
        => new Avatar { Name = "Allan Ritchie" }.RootStyle.ShouldBeEmpty();


    [Fact]
    public void ColorFromNameFillsWithTheNamesHue()
    {
        var style = new Avatar { Name = "Allan Ritchie", ColorFromName = true }.RootStyle;

        style.ShouldContain($"--shiny-avatar-fill:hsl({AvatarText.GetHue("Allan Ritchie")} 55% 42%);");
        style.ShouldContain("--shiny-avatar-ink:white;");
    }


    [Fact]
    public void AnExplicitFillBeatsColorFromName()
        => new Avatar { Name = "Allan Ritchie", ColorFromName = true, FillColor = "teal" }
            .RootStyle.ShouldNotContain("hsl(");


    [Fact]
    public void TheLabelCarriesTheStatus()
        => new Avatar { Name = "Allan Ritchie", Status = AvatarStatus.Busy }.AccessibleLabel.ShouldBe("Allan Ritchie, Busy");


    [Fact]
    public async Task ASingleAvatarRendersInitialsAndStatus()
    {
        var html = await RenderAsync(b =>
        {
            b.OpenComponent<Avatar>(0);
            b.AddAttribute(1, nameof(Avatar.Name), "Allan Ritchie");
            b.AddAttribute(2, nameof(Avatar.Status), AvatarStatus.Online);
            b.CloseComponent();
        });

        html.ShouldContain(">AR<");
        html.ShouldContain("shiny-avatar-status--online");
        html.ShouldContain("role=\"img\"");
        html.ShouldNotContain("<button");
    }


    [Fact]
    public async Task NoNameDrawsTheSilhouette()
    {
        var html = await RenderAsync(b =>
        {
            b.OpenComponent<Avatar>(0);
            b.CloseComponent();
        });

        html.ShouldContain("shiny-avatar-silhouette");
    }


    [Fact]
    public async Task TheGroupCollapsesEveryonePastMax()
    {
        var html = await RenderAsync(Group(count: 6, max: 3));

        html.ShouldContain(">+3<");
        html.ShouldContain(":nth-child(n+4)");
        html.ShouldContain("3 people and 3 more");
    }


    [Fact]
    public async Task TotalCountExtendsTheOverflow()
    {
        var html = await RenderAsync(Group(count: 4, max: 0, total: 120));

        html.ShouldContain(">+116<");
        html.ShouldNotContain("<style>");
    }


    [Fact]
    public async Task NoOverflowNoChip()
    {
        var html = await RenderAsync(Group(count: 3, max: 5));

        html.ShouldNotContain("shiny-avatar-overflow");
        html.ShouldContain("3 people");
    }


    [Fact]
    public async Task TheChipBecomesAButtonOnlyWhenSomeoneListens()
    {
        var html = await RenderAsync(Group(count: 5, max: 2, onOverflow: true));

        html.ShouldContain("<button");
        html.ShouldContain("aria-label=\"+3\"");
    }


    static Action<RenderTreeBuilder> Group(int count, int max, int total = 0, bool onOverflow = false) => b =>
    {
        b.OpenComponent<AvatarGroup>(0);
        b.AddAttribute(1, nameof(AvatarGroup.Max), max);
        b.AddAttribute(2, nameof(AvatarGroup.TotalCount), total);
        if (onOverflow)
            b.AddAttribute(3, nameof(AvatarGroup.OnOverflowClick), EventCallback.Factory.Create<int>(new object(), (int _) => { }));
        b.AddAttribute(4, nameof(AvatarGroup.ChildContent), (RenderFragment)(c =>
        {
            for (var i = 0; i < count; i++)
            {
                c.OpenComponent<Avatar>(10);
                c.SetKey(i);
                c.AddAttribute(11, nameof(Avatar.Name), "Person " + i);
                c.CloseComponent();
            }
        }));
        b.CloseComponent();
    };


    static async Task<string> RenderAsync(Action<RenderTreeBuilder> content)
    {
        await using var renderer = new HtmlRenderer(new ServiceCollection().BuildServiceProvider(), NullLoggerFactory.Instance);
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var root = await renderer.RenderComponentAsync<FragmentHost>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(FragmentHost.Content)] = (RenderFragment)(b => content(b)) }));
            await root.QuiescenceTask;
            // Decoded: the renderer encodes "+" as &#x2B;.
            return System.Net.WebUtility.HtmlDecode(root.ToHtmlString());
        });
    }


    sealed class FragmentHost : ComponentBase
    {
        [Parameter] public RenderFragment? Content { get; set; }

        protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Content);
    }
}
