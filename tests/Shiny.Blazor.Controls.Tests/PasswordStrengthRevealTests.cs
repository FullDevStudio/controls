using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.JSInterop;
using Shouldly;
using Xunit;

namespace Shiny.Blazor.Controls.Tests;

/// <summary>
/// The Show/Hide toggle is relabelled when it is clicked, not only when the parent re-renders. A click
/// re-renders this component alone, so a label set only in OnParametersSet stayed on "Show".
/// </summary>
public class PasswordStrengthRevealTests
{
    sealed class NoJs : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => default;
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) => default;
    }

    sealed class Holder
    {
        public PasswordStrength? Control;
    }

    sealed class Host : ComponentBase
    {
        [Parameter] public Holder Holder { get; set; } = default!;

        protected override void BuildRenderTree(RenderTreeBuilder b)
        {
            b.OpenComponent<PasswordStrength>(0);
            b.AddComponentReferenceCapture(1, r => this.Holder.Control = (PasswordStrength)r);
            b.CloseComponent();
        }
    }


    [Fact]
    public async Task RevealingRelabelsTheToggleWithoutAParentRender()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<IJSRuntime>(new NoJs());

        await using var renderer = new HtmlRenderer(services.BuildServiceProvider(), NullLoggerFactory.Instance);
        var holder = new Holder();
        var root = await renderer.Dispatcher.InvokeAsync(() => renderer.RenderComponentAsync<Host>(
            ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(Host.Holder)] = holder })));

        string Html() => renderer.Dispatcher.InvokeAsync(() => root.ToHtmlString()).GetAwaiter().GetResult();

        Html().ShouldContain(">Show<");

        await renderer.Dispatcher.InvokeAsync(() => holder.Control!.SetPasswordRevealed(true));

        Html().ShouldContain(">Hide<");
        Html().ShouldNotContain(">Show<");
    }
}
