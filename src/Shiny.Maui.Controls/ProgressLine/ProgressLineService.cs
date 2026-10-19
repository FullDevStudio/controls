using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls;

/// <inheritdoc cref="IProgressLineService"/>
public sealed class ProgressLineService : IProgressLineService, IDisposable
{
    readonly List<Run> runs = new();

    IDispatcherTimer? timer;
    ProgressLine? line;
    ProgressLineConfig? config;

    /// <summary>
    /// Bumped whenever a run starts. The completion sequence is a chain of awaited delays, and this
    /// is how a run that starts mid-fade cancels it instead of having the line vanish underneath it.
    /// </summary>
    int generation;

    bool disposed;

    public bool IsRunning => this.runs.Count > 0;


    public IProgressLineHandle Start(Action<ProgressLineConfig>? configure = null)
    {
        ObjectDisposedException.ThrowIf(this.disposed, this);

        var cfg = new ProgressLineConfig();
        configure?.Invoke(cfg);

        var run = new Run(this, cfg)
        {
            Progress = Math.Clamp(cfg.StartProgress, 0, 1)
        };

        this.runs.Add(run);
        this.generation++;

        OnUi(() => this.Show(cfg));
        return run;
    }


    public void CompleteAll()
    {
        foreach (var run in this.runs.ToArray())
            run.Complete();
    }


    void Show(ProgressLineConfig cfg)
    {
        if (this.disposed)
            return;

        // The last caller's look wins. Two overlapping runs share one line, so there is no honest way
        // to show both configurations at once, and the newer one is the one the user just triggered.
        this.config = cfg;

        this.line ??= new ProgressLine();
        Apply(this.line, cfg);

        this.EnsureAttached();

        this.line.Opacity = 1;
        this.line.IsVisible = true;
        this.line.IsActive = true;

        this.PushProgress();
        this.StartTimer(cfg);
    }


    static void Apply(ProgressLine line, ProgressLineConfig cfg)
    {
        // Docked by the service, not by the control's own re-parenting pass.
        line.Dock = false;

        line.Position = cfg.Position;
        line.BarColor = cfg.BarColor;
        line.TrackColor = cfg.TrackColor;
        line.LineHeight = cfg.LineHeight;
        line.CornerRadius = cfg.CornerRadius;
        line.UseGradient = cfg.UseGradient;
        line.GradientStartColor = cfg.GradientStartColor;
        line.GradientEndColor = cfg.GradientEndColor;
        line.PulseEnabled = cfg.PulseEnabled;
        line.AutoInset = cfg.AutoInset;
        line.Offset = cfg.Offset;
        line.FadeDuration = cfg.FadeDuration;
        line.ProgressAnimationDuration = cfg.ProgressAnimationDuration;
        line.IsIndeterminate = cfg.Indeterminate;
        AutomationIds.Set(line, cfg.AutomationId);

        cfg.Configure?.Invoke(line);
    }


    /// <summary>
    /// Puts the line on the page that is showing right now, moving it if navigation has happened
    /// since the run started.
    /// </summary>
    /// <remarks>
    /// Re-resolved on every tick rather than captured at <see cref="Start"/>. A service that caches
    /// the first page it saw keeps drawing onto a page the user has already navigated away from, and
    /// the symptom is a line that silently never appears again for the rest of the session.
    /// </remarks>
    void EnsureAttached()
    {
        if (this.line is null)
            return;

        var page = PageOverlay.CurrentPage();
        if (page is null)
            return;

        var target = PageOverlay.GetOrCreateLayer<PageOverlay.ProgressLineLayer>(page, PageOverlay.Layers.ProgressLine);
        if (ReferenceEquals(this.line.Parent, target))
            return;

        if (this.line.Parent is Layout previous)
            previous.Children.Remove(this.line);

        target.Children.Add(this.line);
        this.line.RefreshLayout();
    }


    void StartTimer(ProgressLineConfig cfg)
    {
        this.StopTimer();

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher is null)
            return;

        this.timer = dispatcher.CreateTimer();
        this.timer.Interval = cfg.TrickleInterval > TimeSpan.Zero
            ? cfg.TrickleInterval
            : TimeSpan.FromMilliseconds(400);
        this.timer.IsRepeating = true;
        this.timer.Tick += this.OnTick;
        this.timer.Start();
    }


    void StopTimer()
    {
        if (this.timer is null)
            return;

        this.timer.Tick -= this.OnTick;
        this.timer.Stop();
        this.timer = null;
    }


    void OnTick(object? sender, EventArgs e)
    {
        if (this.disposed || this.runs.Count == 0)
            return;

        // Navigation can happen between ticks with no run boundary to hang the check off.
        this.EnsureAttached();

        foreach (var run in this.runs)
            run.Trickle();

        this.PushProgress();
    }


    /// <summary>
    /// Pushes the slowest active run to the line. The slowest, not the average: the line's job is to
    /// say "not finished yet", and averaging lets one quick run drag the bar most of the way across
    /// while the slow one it is waiting on has barely started.
    /// </summary>
    void PushProgress()
    {
        if (this.line is null || this.runs.Count == 0)
            return;

        var lowest = this.runs.Min(r => r.Progress);
        this.line.Value = Math.Clamp(lowest, 0, 1) * this.line.Maximum;
    }


    internal void OnRunFinished(Run run, bool sweep)
    {
        OnUi(() =>
        {
            this.runs.Remove(run);

            if (this.runs.Count > 0)
            {
                this.PushProgress();
                return;
            }

            this.StopTimer();
            _ = this.FinishAsync(sweep, ++this.generation);
        });
    }


    async Task FinishAsync(bool sweep, int token)
    {
        var current = this.line;
        if (current is null)
            return;

        if (sweep)
        {
            current.IsIndeterminate = false;
            current.Value = current.Maximum;

            // Let the fill reach the end before it starts fading, so the run visibly completes
            // rather than dissolving somewhere short of it.
            await Task.Delay(Math.Max(current.ProgressAnimationDuration, 0));
            if (token != this.generation || this.disposed)
                return;
        }

        current.IsActive = false;

        await Task.Delay(Math.Max(current.FadeDuration, 0) + 16);
        if (token != this.generation || this.disposed)
            return;

        this.Detach();
    }


    void Detach()
    {
        if (this.line is null)
            return;

        if (this.line.Parent is Layout parent)
            parent.Children.Remove(this.line);

        this.line.Value = 0;
        this.line.IsIndeterminate = false;
        this.line.Opacity = 0;
    }


    static void OnUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is null || !dispatcher.IsDispatchRequired)
            action();
        else
            dispatcher.Dispatch(action);
    }


    public void Dispose()
    {
        if (this.disposed)
            return;

        this.disposed = true;
        this.StopTimer();
        this.runs.Clear();

        OnUi(() =>
        {
            this.Detach();
            this.line?.Dispose();
            this.line = null;
        });
    }


    internal sealed class Run(ProgressLineService owner, ProgressLineConfig config) : IProgressLineHandle
    {
        public double Progress { get; set; }

        public bool IsComplete { get; private set; }

        public void SetProgress(double progress)
        {
            if (this.IsComplete)
                return;

            var clamped = Math.Clamp(progress, 0, 1);
            if (clamped <= this.Progress)
                return;

            this.Progress = clamped;
            OnUi(owner.PushProgress);
        }


        /// <summary>
        /// Advances a fraction of the distance still to run, so the line decelerates as it approaches
        /// the ceiling and never quite arrives — which is the honest shape for "still working".
        /// </summary>
        public void Trickle()
        {
            if (this.IsComplete || !config.Trickle || config.Indeterminate)
                return;

            var ceiling = Math.Clamp(config.TrickleCeiling, 0, 1);
            if (this.Progress >= ceiling)
                return;

            this.Progress = Math.Min(ceiling, this.Progress + (ceiling - this.Progress) * config.TrickleRate);
        }


        public void Complete() => this.Finish(sweep: true);

        public void Cancel() => this.Finish(sweep: false);

        public void Dispose() => this.Complete();

        void Finish(bool sweep)
        {
            if (this.IsComplete)
                return;

            this.IsComplete = true;
            this.Progress = 1;
            owner.OnRunFinished(this, sweep);
        }
    }
}
