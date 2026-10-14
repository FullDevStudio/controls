using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Animations;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Handlers;

namespace Shiny.Maui.Controls.Tests;

/// <summary>
/// Gives a headless view an <see cref="IAnimationManager"/> whose ticker never ticks, so a MAUI
/// animation started on it (FadeTo, ScaleTo, ...) stays in flight until the test steps it with
/// <see cref="Tick"/> or <see cref="Finish"/>. That is the only way to land a property change
/// <i>during</i> an animation, which is where "a request mid-animation is dropped" bugs live.
/// </summary>
sealed class FrozenAnimationHost
{
    readonly FrozenTicker ticker = new();

    public FrozenAnimationHost()
    {
        var services = new ServiceCollection()
            .AddSingleton<IAnimationManager>(_ => new AnimationManager(this.ticker))
            .BuildServiceProvider();
        this.Context = new MauiContext(services);
    }

    public IMauiContext Context { get; }

    /// <summary>Attaches a no-op handler carrying the frozen animation manager to <paramref name="view"/>.</summary>
    public T Attach<T>(T view) where T : VisualElement
    {
        var handler = new StubHandler();
        handler.SetMauiContext(this.Context);
        view.Handler = handler;
        return view;
    }

    /// <summary>Advances every running animation by one ticker fire.</summary>
    public void Tick() => this.ticker.Fire?.Invoke();

    /// <summary>
    /// Ticks until no animation is left running (or gives up after a generous cap). The animation
    /// manager measures real elapsed time between fires, so this waits between ticks.
    /// </summary>
    public async Task Finish(int maxTicks = 500)
    {
        var idle = 0;
        for (var i = 0; i < maxTicks && idle < 5; i++)
        {
            await Task.Delay(10);

            // A control that chains animations (finish one, then start the next from a continuation)
            // leaves the ticker briefly stopped in between, so only a run of idle checks means done.
            if (this.ticker.IsRunning)
            {
                idle = 0;
                this.Tick();
            }
            else
            {
                idle++;
            }
        }
    }


    sealed class FrozenTicker : Ticker
    {
        bool running;

        public override bool IsRunning => this.running;

        public override void Start() => this.running = true;

        public override void Stop() => this.running = false;
    }


    sealed class StubHandler() : ViewHandler<IView, object>(new PropertyMapper<IView, StubHandler>())
    {
        protected override object CreatePlatformView() => new();
    }
}
