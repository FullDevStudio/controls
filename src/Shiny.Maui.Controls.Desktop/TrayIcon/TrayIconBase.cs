namespace Shiny.Maui.Controls.Desktop.TrayIcon;

abstract class TrayIconBase : ITrayIcon
{
    protected TrayMenu? Menu { get; private set; }
    protected Func<Stream>? IconFactory { get; private set; }

    string? tooltip;
    public string? Tooltip
    {
        get => this.tooltip;
        set
        {
            this.tooltip = value;
            this.OnTooltipChanged(value);
        }
    }

    string? title;
    public string? Title
    {
        get => this.title;
        set
        {
            this.title = value;
            this.OnTitleChanged(value);
        }
    }

    bool isVisible = true;
    public bool IsVisible
    {
        get => this.isVisible;
        set
        {
            this.isVisible = value;
            this.OnVisibilityChanged(value);
        }
    }

    bool isTemplate;
    public bool IsTemplateImage
    {
        get => this.isTemplate;
        set
        {
            this.isTemplate = value;
            if (this.IconFactory != null)
                this.OnIconChanged(this.IconFactory);
        }
    }

    string? badge;
    public string? Badge
    {
        get => this.badge;
        set
        {
            if (this.badge == value) return;
            this.badge = value;
            this.OnBadgeChanged(value);
        }
    }

    System.Threading.Timer? animationTimer;
    IReadOnlyList<Func<Stream>>? animationFrames;
    int animationFrameIndex;

    public bool IsAnimating => this.animationTimer != null;

    public void SetIcon(Func<Stream> iconStreamFactory)
    {
        this.IconFactory = iconStreamFactory;
        this.OnIconChanged(iconStreamFactory);
    }

    public void SetMenu(TrayMenu menu)
    {
        if (this.Menu != null)
        {
            this.Menu.Changed -= this.OnMenuChanged;
            this.Menu.ItemUpdated -= this.OnMenuItemUpdatedCore;
        }
        this.Menu = menu;
        menu.Changed += this.OnMenuChanged;
        menu.ItemUpdated += this.OnMenuItemUpdatedCore;
        this.OnMenuChanged(menu, EventArgs.Empty);
    }

    public abstract void ShowMenu();

    public abstract void ShowNotification(string title, string message);

    public void StartAnimation(IReadOnlyList<Func<Stream>> frames, TimeSpan interval)
    {
        ArgumentNullException.ThrowIfNull(frames);
        if (frames.Count == 0)
            throw new ArgumentException("Animation requires at least one frame.", nameof(frames));
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Animation interval must be positive.");

        this.StopAnimation();
        this.animationFrames = frames;
        this.animationFrameIndex = 0;
        this.OnIconChanged(frames[0]);
        this.animationTimer = new System.Threading.Timer(this.OnAnimationTick, null, interval, interval);
    }

    public void StopAnimation()
    {
        var t = this.animationTimer;
        this.animationTimer = null;
        t?.Dispose();
        this.animationFrames = null;
        if (this.IconFactory != null)
            this.OnIconChanged(this.IconFactory);
    }

    void OnAnimationTick(object? state)
    {
        var frames = this.animationFrames;
        if (frames == null || this.animationTimer == null)
            return;
        this.animationFrameIndex = (this.animationFrameIndex + 1) % frames.Count;
        try
        {
            this.OnIconChanged(frames[this.animationFrameIndex]);
        }
        catch
        {
            // best-effort: never let a missing frame stream tear down the timer
        }
    }

    public event EventHandler<TrayClickEventArgs>? PrimaryClick;
    public event EventHandler<TrayClickEventArgs>? SecondaryClick;
    public event EventHandler<TrayClickEventArgs>? DoubleClick;

    protected void RaisePrimary(int x, int y) => this.PrimaryClick?.Invoke(this, new TrayClickEventArgs(x, y));
    protected void RaiseSecondary(int x, int y) => this.SecondaryClick?.Invoke(this, new TrayClickEventArgs(x, y));
    protected void RaiseDouble(int x, int y) => this.DoubleClick?.Invoke(this, new TrayClickEventArgs(x, y));

    protected abstract void OnIconChanged(Func<Stream> factory);
    protected abstract void OnTooltipChanged(string? value);
    protected abstract void OnTitleChanged(string? value);
    protected abstract void OnVisibilityChanged(bool visible);
    protected abstract void OnMenuChanged(object? sender, EventArgs e);

    void OnMenuItemUpdatedCore(object? sender, TrayMenuItemBase item) => this.OnMenuItemUpdated(item);

    /// <summary>
    /// One item changed without changing size. Platforms that can repaint the item in place
    /// override this; the default rebuilds the whole menu.
    /// </summary>
    protected virtual void OnMenuItemUpdated(TrayMenuItemBase item)
        => this.OnMenuChanged(this.Menu, EventArgs.Empty);

    /// <summary>Default implementation re-renders the current icon so platforms can composite the badge over it.</summary>
    protected virtual void OnBadgeChanged(string? value)
    {
        if (this.IconFactory != null)
            this.OnIconChanged(this.IconFactory);
    }

    public virtual void Dispose()
    {
        this.StopAnimation();
        if (this.Menu != null)
        {
            this.Menu.Changed -= this.OnMenuChanged;
            this.Menu.ItemUpdated -= this.OnMenuItemUpdatedCore;
        }
        GC.SuppressFinalize(this);
    }
}
