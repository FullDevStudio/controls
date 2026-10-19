using Microsoft.Extensions.Logging;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls.QuickEntry;

sealed class QuickEntryService : IQuickEntryService, IDisposable
{
    readonly IReadOnlyList<IQuickEntryPresenter> presenters;
    readonly IReadOnlyList<IScreenGlowPresenter> glowPresenters;
    readonly ILogger<QuickEntryService>? logger;
    readonly TimeProvider time;

    View? content;
    IQuickEntryPresenter? active;
    IScreenGlowPresenter? activeGlow;
    IQuickEntryBusyState? observedBusy;
    bool prepared;
    double currentHeight;
    bool desktopFallbackWarned;

    // Focus bounces once as the OS moves the key window between processes, and that transient
    // arrives as a deactivation. Ignoring them for a beat after Show stops the popup closing the
    // instant it opens.
    static readonly TimeSpan suppressDeactivateFor = TimeSpan.FromMilliseconds(400);
    long? suppressDeactivateSince;

    public QuickEntryService(
        QuickEntryOptions options,
        IEnumerable<IQuickEntryPresenter> presenters,
        IEnumerable<IScreenGlowPresenter> glowPresenters,
        ILogger<QuickEntryService>? logger = null,
        TimeProvider? time = null
    )
    {
        this.Options = options;
        this.presenters = presenters.ToList();
        this.glowPresenters = glowPresenters.ToList();
        this.logger = logger;
        this.time = time ?? TimeProvider.System;
        this.currentHeight = options.CollapsedHeight;
    }

    public QuickEntryOptions Options { get; }

    public bool IsSupported => this.ResolvePresenter() != null;

    public QuickEntryPresentation ResolvedPresentation => this.ResolvePresenter()?.Kind ?? QuickEntryPresentation.InApp;

    public bool IsOpen { get; private set; }

    public View? Content => this.content;

    public event EventHandler? Opened;
    public event EventHandler? Closed;

    public async Task PreloadAsync()
    {
        var presenter = this.ResolvePresenter();
        if (presenter == null)
            return;

        await this.PrepareAsync(presenter).ConfigureAwait(true);
    }

    public void Show()
    {
        var presenter = this.ResolvePresenter();
        if (presenter == null)
        {
            this.logger?.LogWarning("Quick entry cannot be shown — no presenter is available yet. Call Show() once the app has a window.");
            return;
        }

        _ = this.ShowAsync(presenter);
    }

    async Task ShowAsync(IQuickEntryPresenter presenter)
    {
        try
        {
            await this.PrepareAsync(presenter).ConfigureAwait(true);

            this.suppressDeactivateSince = this.time.GetTimestamp();
            presenter.Show(this.Options, this.Options.Width, this.currentHeight);
            this.IsOpen = true;

            if (this.Options.ScreenGlow == ScreenGlowTrigger.WhileOpen)
                this.ShowGlow();

            (this.content as IQuickEntryPresentationAware)?.OnQuickEntryOpened();
            this.Opened?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Failed to show the quick entry popup");
        }
    }

    public void Hide()
    {
        if (!this.IsOpen || this.active == null)
            return;

        this.active.Hide();
        this.IsOpen = false;

        // Whichever trigger lit it, closing the popup always puts the glow out — a glow left burning
        // with nothing on screen would be inexplicable.
        if (this.Options.ScreenGlow != ScreenGlowTrigger.None)
            this.HideGlow();

        (this.content as IQuickEntryPresentationAware)?.OnQuickEntryClosed();
        this.Closed?.Invoke(this, EventArgs.Empty);

        if (this.Options.RecreateContentOnShow)
            this.RebuildContent();
    }

    public void Toggle()
    {
        if (this.IsOpen)
            this.Hide();
        else
            this.Show();
    }

    public void Resize(double? width = null, double? height = null)
    {
        var w = width ?? this.Options.Width;
        var h = Math.Clamp(
            height ?? this.currentHeight,
            this.Options.CollapsedHeight,
            Math.Max(this.Options.CollapsedHeight, this.Options.MaxHeight)
        );

        this.currentHeight = h;
        this.active?.Resize(this.Options, w, h);
    }

    // ---------------------------------------------------------------------------------------
    // Glow
    // ---------------------------------------------------------------------------------------

    public bool IsGlowSupported => this.ResolveGlow() != null;

    public bool IsGlowVisible { get; private set; }

    public void ShowGlow()
    {
        var glow = this.ResolveGlow();
        if (glow == null || this.IsGlowVisible)
            return;

        this.IsGlowVisible = true;
        this.activeGlow = glow;
        _ = this.RunGlow(glow.ShowAsync(this.Options.Glow));
    }

    public void HideGlow()
    {
        if (!this.IsGlowVisible || this.activeGlow == null)
            return;

        this.IsGlowVisible = false;
        _ = this.RunGlow(this.activeGlow.HideAsync(this.Options.Glow));
    }

    public async Task PulseGlowAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        this.ShowGlow();
        try
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.HideGlow();
        }
    }

    async Task RunGlow(Task work)
    {
        try
        {
            await work.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "The screen glow failed");
        }
    }

    // ---------------------------------------------------------------------------------------
    // Presenter selection
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Picks the presenter for the configured mode, resolving <see cref="QuickEntryPresentation.Auto"/>
    /// and falling back to in-app where a desktop window was asked for but is not available.
    /// </summary>
    IQuickEntryPresenter? ResolvePresenter()
    {
        var wantsDesktop = this.Options.Presentation != QuickEntryPresentation.InApp;
        if (wantsDesktop)
        {
            var desktop = this.Find(QuickEntryPresentation.Desktop);
            if (desktop != null)
                return desktop;

            // Only warn for an explicit ask. Auto is meant to land on in-app without comment — that
            // is the whole point of it.
            if (this.Options.Presentation == QuickEntryPresentation.Desktop && !this.desktopFallbackWarned)
            {
                this.desktopFallbackWarned = true;
                this.logger?.LogWarning(
                    "Quick entry was configured for a desktop window, which is not available here. Falling back to the in-app overlay. " +
                    "A desktop window needs the Shiny.Maui.Controls.Desktop add-on registered with UseDesktopQuickEntry(), on Windows, macOS (AppKit) or Linux."
                );
            }
        }

        return this.Find(QuickEntryPresentation.InApp);
    }

    IQuickEntryPresenter? Find(QuickEntryPresentation kind)
    {
        foreach (var presenter in this.presenters)
        {
            if (presenter.Kind == kind && presenter.IsSupported)
                return presenter;
        }
        return null;
    }

    IScreenGlowPresenter? ResolveGlow()
    {
        var kind = this.ResolvedPresentation;
        foreach (var glow in this.glowPresenters)
        {
            if (glow.Kind == kind && glow.IsSupported)
                return glow;
        }

        // The popup and the glow are chosen independently: Wayland can host the popup but not a
        // click-through overlay above other windows, so a desktop popup there still gets an in-app
        // glow rather than none.
        foreach (var glow in this.glowPresenters)
        {
            if (glow.IsSupported)
                return glow;
        }
        return null;
    }

    // ---------------------------------------------------------------------------------------

    async Task PrepareAsync(IQuickEntryPresenter presenter)
    {
        if (this.prepared && ReferenceEquals(this.active, presenter))
            return;

        // Switching presentation mid-run: let the old surface go before building the new one, or the
        // content ends up parented twice.
        if (this.active != null && !ReferenceEquals(this.active, presenter))
        {
            this.active.Hide();
            this.active.Teardown();
            this.active.Deactivated = null;
            this.active.KeyPressed = null;
            this.active.ContentHeightChanged = null;
            this.prepared = false;
        }

        this.content ??= this.BuildContent();
        this.active = presenter;
        presenter.Deactivated = this.OnDeactivated;
        presenter.KeyPressed = this.OnKey;
        presenter.ContentHeightChanged = h => this.Resize(null, h);

        await presenter.PrepareAsync(this.Options, this.content).ConfigureAwait(true);
        this.prepared = true;
    }

    View BuildContent()
    {
        var view = this.Options.ContentFactory?.Invoke() ?? new PromptView();
        view.WidthRequest = this.Options.Width;

        // There is no owner element to name it after, so the id comes from the options. Through
        // SetValue, not the AutomationId setter, which throws when the content already had one.
        if (!String.IsNullOrEmpty(this.Options.AutomationId) && String.IsNullOrEmpty(view.AutomationId))
            AutomationIds.Set(view, this.Options.AutomationId);
        this.ObserveBusy(view);
        return view;
    }

    void RebuildContent()
    {
        this.content = this.BuildContent();
        this.currentHeight = this.Options.CollapsedHeight;
        this.active?.SetContent(this.content);
        this.Resize(null, this.currentHeight);
    }

    void OnDeactivated()
    {
        if (!this.IsOpen || !this.Options.DismissOnFocusLost)
            return;
        if (this.suppressDeactivateSince is { } since && this.time.GetElapsedTime(since) < suppressDeactivateFor)
            return;

        this.Hide();
    }

    /// <summary>
    /// Content gets first refusal on every navigation key; only what it declines reaches the host's
    /// own behaviour, which is just "Escape closes".
    /// </summary>
    bool OnKey(QuickEntryKey key)
    {
        if (this.content is IQuickEntryKeyHandler handler && handler.HandleKey(key))
            return true;

        if (key == QuickEntryKey.Escape && this.Options.DismissOnEscape)
        {
            this.Hide();
            return true;
        }
        return false;
    }

    void ObserveBusy(View view)
    {
        if (this.observedBusy != null)
        {
            this.observedBusy.BusyChanged -= this.OnContentBusyChanged;
            this.observedBusy = null;
        }

        if (this.Options.ScreenGlow != ScreenGlowTrigger.WhileBusy)
            return;

        if (view is IQuickEntryBusyState busy)
        {
            this.observedBusy = busy;
            busy.BusyChanged += this.OnContentBusyChanged;
        }
    }

    void OnContentBusyChanged(object? sender, EventArgs e)
    {
        if (this.observedBusy == null)
            return;

        if (this.observedBusy.IsBusy && this.IsOpen)
            this.ShowGlow();
        else
            this.HideGlow();
    }

    public void Dispose()
    {
        if (this.observedBusy != null)
        {
            this.observedBusy.BusyChanged -= this.OnContentBusyChanged;
            this.observedBusy = null;
        }

        this.active?.Teardown();
        this.activeGlow?.Teardown();
    }
}
