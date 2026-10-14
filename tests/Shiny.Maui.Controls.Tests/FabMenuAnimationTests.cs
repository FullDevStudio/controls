using Microsoft.Maui.Controls;
using Shouldly;
using Xunit;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// An IsOpen change that lands while the menu is still animating is honoured once the running
/// animation finishes. AnimateToStateAsync used to return early while animating, so closing within the
/// open animation (~300ms) left IsOpen False with every item still on screen.
/// </summary>
[Collection(ApplicationResourcesCollection.Name)]
public class FabMenuAnimationTests
{
    public FabMenuAnimationTests()
    {
        TestDispatcherProvider.Install();
        _ = new Application();
    }

    static (FrozenAnimationHost Host, FabMenu Menu, FabMenuItem Item) Build()
    {
        var host = new FrozenAnimationHost();
        var menu = host.Attach(new FabMenu());
        var item = new FabMenuItem { Text = "Share" };
        menu.Items.Add(item);
        return (host, menu, item);
    }

    [Fact]
    public async Task ClosingDuringTheOpenAnimation_StillCloses()
    {
        var (host, menu, item) = Build();

        menu.IsOpen = true;
        item.IsVisible.ShouldBeTrue(); // primed, animation in flight

        menu.IsOpen = false;
        await host.Finish();

        menu.IsOpen.ShouldBeFalse();
        item.IsVisible.ShouldBeFalse();
    }

    [Fact]
    public async Task OpenCloseOpen_EndsOpen()
    {
        var (host, menu, item) = Build();

        menu.IsOpen = true;
        menu.IsOpen = false;
        menu.IsOpen = true;
        await host.Finish();

        menu.IsOpen.ShouldBeTrue();
        item.IsVisible.ShouldBeTrue();
        item.Opacity.ShouldBe(1, 0.001);
    }
}
