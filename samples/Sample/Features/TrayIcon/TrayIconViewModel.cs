using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using Shiny;
using Shiny.Maui.Controls.Desktop.TrayIcon;

namespace Sample.Features.TrayIcon;

[ShellMap<TrayIconPage>(registerRoute: false)]
public partial class TrayIconViewModel : ObservableObject
{
    readonly ITrayIconFactory? factory;
    Shiny.Maui.Controls.Desktop.TrayIcon.ITrayIcon? icon;

    // Live rows in the tray menu, driven from a background timer to show they move while the
    // menu is open — the same shape as a usage meter against a limit.
    readonly TrayProgressItem sessionUsage = new("Session usage", 0.18, "Resets in 4 hr 12 min");
    readonly TrayProgressItem syncProgress = new("Syncing library", 0) { Tone = TrayTone.Success };
    Timer? usageTimer;
    int usageTicks;

    [ObservableProperty] string status = "Not running";
    [ObservableProperty] string tooltip = "Shiny Controls Sample";
    [ObservableProperty] string title = string.Empty;
    [ObservableProperty] string badge = string.Empty;
    [ObservableProperty] bool isTemplate;
    [ObservableProperty] bool isVisible = true;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotRunning))]
    bool isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotSupported))]
    bool isSupported;

    public bool IsNotRunning => !this.IsRunning;
    public bool IsNotSupported => !this.IsSupported;
    [ObservableProperty] string unsupportedMessage = string.Empty;
    [ObservableProperty] bool notificationsEnabled = true;
    [ObservableProperty] int counter;
    [ObservableProperty] bool isAnimating;

    public ObservableCollection<string> EventLog { get; } = new();

    public TrayIconViewModel(IServiceProvider services)
    {
        this.factory = services.GetService(typeof(ITrayIconFactory)) as ITrayIconFactory;
        this.IsSupported = OperatingSystem.IsWindows()
            || OperatingSystem.IsMacCatalyst()
            || OperatingSystem.IsMacOS()
            || OperatingSystem.IsLinux();
        if (!this.IsSupported)
            this.UnsupportedMessage = "System tray icons are only available on Windows, macOS (AppKit / MacCatalyst), and Linux. This platform doesn't have a system tray.";
    }

    partial void OnTooltipChanged(string value) { if (this.icon != null) this.icon.Tooltip = value; }
    partial void OnTitleChanged(string value) { if (this.icon != null) this.icon.Title = value; }
    partial void OnBadgeChanged(string value) { if (this.icon != null) this.icon.Badge = string.IsNullOrWhiteSpace(value) ? null : value; }
    partial void OnIsTemplateChanged(bool value) { if (this.icon != null) this.icon.IsTemplateImage = value; }
    partial void OnIsVisibleChanged(bool value) { if (this.icon != null) this.icon.IsVisible = value; }

    [RelayCommand]
    void Start()
    {
        if (this.factory == null || this.icon != null) return;
        try
        {
            this.icon = this.factory.Create();
            this.icon.SetIcon(() => OpenLogoStream());
            this.icon.Tooltip = this.Tooltip;
            this.icon.Title = this.Title;
            this.icon.IsTemplateImage = this.IsTemplate;
            this.icon.IsVisible = this.IsVisible;
            if (!string.IsNullOrWhiteSpace(this.Badge))
                this.icon.Badge = this.Badge;
            this.icon.SetMenu(this.BuildMenu());
            this.usageTimer = new Timer(_ => this.TickUsage(), null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            this.icon.PrimaryClick += (_, e) => this.LogEvent($"Primary click @ {e.X},{e.Y}");
            this.icon.SecondaryClick += (_, e) => this.LogEvent($"Secondary click @ {e.X},{e.Y}");
            this.icon.DoubleClick += (_, e) => this.LogEvent($"Double click @ {e.X},{e.Y}");
            this.IsRunning = true;
            this.Status = "Running — check your system tray / menu bar";
            this.LogEvent("Tray icon created");
        }
        catch (Exception ex)
        {
            this.Status = "Failed: " + ex.Message;
            this.LogEvent("Error: " + ex.Message);
        }
    }

    [RelayCommand]
    void Stop()
    {
        if (this.icon == null) return;
        this.usageTimer?.Dispose();
        this.usageTimer = null;
        this.icon.Dispose();
        this.icon = null;
        this.IsAnimating = false;
        this.IsRunning = false;
        this.Status = "Stopped";
        this.LogEvent("Tray icon disposed");
    }

    [RelayCommand]
    void Increment()
    {
        this.Counter++;
        if (this.icon != null)
        {
            this.icon.Tooltip = $"Counter: {this.Counter}";
            this.icon.Badge = this.Counter > 0 ? this.Counter.ToString() : null;
            this.Badge = this.icon.Badge ?? string.Empty;
            this.LogEvent($"Counter → {this.Counter}");
        }
    }

    [RelayCommand]
    void ClearBadge()
    {
        this.Badge = string.Empty;
        if (this.icon != null) this.icon.Badge = null;
        this.LogEvent("Badge cleared");
    }

    [RelayCommand]
    void ToggleVisibility() => this.IsVisible = !this.IsVisible;

    [RelayCommand]
    void RebuildMenu()
    {
        if (this.icon == null) return;
        this.icon.SetMenu(this.BuildMenu());
        this.LogEvent("Menu rebuilt");
    }

    [RelayCommand]
    void SendNotification()
    {
        if (this.icon == null) return;
        this.icon.ShowNotification("Shiny Controls", $"Hello from your tray icon at {DateTime.Now:HH:mm:ss}.");
        this.LogEvent("Notification sent");
    }

    [RelayCommand]
    void ToggleAnimation()
    {
        if (this.icon == null) return;
        if (this.icon.IsAnimating)
        {
            this.icon.StopAnimation();
            this.IsAnimating = false;
            this.LogEvent("Animation stopped");
        }
        else
        {
            var frames = new Func<Stream>[]
            {
                OpenLogoStream,
                OpenLogoStream,
                OpenLogoStream,
                OpenLogoStream
            };
            this.icon.StartAnimation(frames, TimeSpan.FromMilliseconds(400));
            this.IsAnimating = true;
            this.LogEvent("Animation started (4 frames @ 400ms)");
        }
    }

    [RelayCommand]
    void ClearLog() => this.EventLog.Clear();

    void TickUsage()
    {
        // runs on a thread-pool thread; the tray repaints the rows itself
        this.usageTicks++;
        var usage = Math.Min(1, this.sessionUsage.Value + 0.03);
        if (usage >= 1) usage = 0.05;
        this.sessionUsage.Value = usage;
        this.sessionUsage.Tone = usage >= 0.9 ? TrayTone.Danger : usage >= 0.75 ? TrayTone.Warning : TrayTone.Accent;
        this.sessionUsage.Caption = $"Resets in 4 hr {Math.Max(0, 12 - this.usageTicks / 5)} min";

        var sync = this.syncProgress.Value + 0.1;
        this.syncProgress.Value = sync > 1 ? 0 : sync;
        this.syncProgress.Detail = $"{(int)Math.Round(this.syncProgress.Value * 40)} of 40";
    }

    TrayMenu BuildMenu() => TrayMenu.Build(b => b
        .Item(new TrayMenuItem("Increment counter", this.Increment) { Accelerator = "Ctrl+I", Icon = OpenLogoStream })
        .Item(new TrayMenuItem("Send notification", () => this.SendNotification()) { Accelerator = "Ctrl+N", Icon = OpenLogoStream })
        .Check("Notifications", this.NotificationsEnabled, v =>
        {
            this.NotificationsEnabled = v;
            this.LogEvent("Notifications " + (v ? "enabled" : "disabled"));
        })
        .Separator()
        .Progress(this.sessionUsage)
        .Progress(this.syncProgress)
        .Separator()
        .Item(new TrayMenuItem("Upgrade plan", () => this.LogEvent("Upgrade clicked")) { Pill = new TrayPill("Pro", TrayTone.Accent) })
        .Item(new TrayMenuItem("What's new", () => this.LogEvent("What's new clicked")) { Pill = new TrayPill("New", TrayTone.Success) })
        .Separator()
        .Submenu("Status", s => s
            .Item("Available", () => this.LogEvent("Status → Available"))
            .Item("Busy", () => this.LogEvent("Status → Busy"))
            .Item("Away", () => this.LogEvent("Status → Away")))
        .Separator()
        .Item(new TrayMenuItem("About", () => this.LogEvent("About clicked")) { Accelerator = "F1" })
        .Item(new TrayMenuItem("Quit sample tray", this.Stop) { Accelerator = "Ctrl+Q" }));

    void LogEvent(string message)
    {
        // Tray menu callbacks fire on the platform UI thread, and
        // MAUI MainThread is not wired up on the macOS AppKit host,
        // so update the collection directly.
        this.EventLog.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {message}");
        while (this.EventLog.Count > 30) this.EventLog.RemoveAt(this.EventLog.Count - 1);
    }

    static Stream OpenLogoStream()
        => FileSystem.OpenAppPackageFileAsync("trayicon.png").GetAwaiter().GetResult();
}
