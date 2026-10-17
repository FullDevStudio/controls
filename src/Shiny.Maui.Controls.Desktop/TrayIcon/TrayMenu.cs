using System.Collections;
using System.Collections.ObjectModel;

namespace Shiny.Maui.Controls.Desktop.TrayIcon;

/// <summary>
/// A tray context menu. Mutating Items or any item's properties after the menu has been assigned
/// triggers a rebuild on the platform handler.
/// </summary>
public sealed class TrayMenu
{
    public ObservableCollection<TrayMenuItemBase> Items { get; } = new();

    /// <summary>Fired internally whenever the menu shape changes — handlers subscribe to rebuild.</summary>
    internal event EventHandler? Changed;

    /// <summary>
    /// Fired internally when an item changed in a way that keeps its size — a progress value, its
    /// detail or caption text. Handlers repaint that one item in place, which is what lets a value
    /// move while the menu is open; a full rebuild would replace the menu the user is looking at.
    /// </summary>
    internal event EventHandler<TrayMenuItemBase>? ItemUpdated;

    public TrayMenu()
    {
        this.Items.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null)
                foreach (TrayMenuItemBase i in e.NewItems)
                    this.Adopt(i);
            this.RaiseChanged();
        };
    }

    void Adopt(TrayMenuItemBase item)
    {
        item.Owner = this;
        if (item is TraySubmenu sub)
            foreach (var child in sub.Items)
                this.Adopt(child);
    }

    internal void RaiseChanged() => this.Changed?.Invoke(this, EventArgs.Empty);

    internal void RaiseItemUpdated(TrayMenuItemBase item) => this.ItemUpdated?.Invoke(this, item);

    public static TrayMenu Build(Action<TrayMenuBuilder> configure)
    {
        var b = new TrayMenuBuilder();
        configure(b);
        return b.Build();
    }
}

public abstract class TrayMenuItemBase
{
    bool isEnabled = true;
    bool isVisible = true;

    public bool IsEnabled
    {
        get => this.isEnabled;
        set { if (this.isEnabled != value) { this.isEnabled = value; this.Owner?.RaiseChanged(); } }
    }

    public bool IsVisible
    {
        get => this.isVisible;
        set { if (this.isVisible != value) { this.isVisible = value; this.Owner?.RaiseChanged(); } }
    }

    internal TrayMenu? Owner { get; set; }
}

public sealed class TrayMenuItem : TrayMenuItemBase
{
    string label = string.Empty;
    string? accelerator;
    Func<Stream>? iconFactory;
    TrayPill? pill;

    public TrayMenuItem() { }
    public TrayMenuItem(string label, Action? clicked = null)
    {
        this.Label = label;
        if (clicked != null)
            this.Clicked += (_, _) => clicked();
    }

    public string Label
    {
        get => this.label;
        set { if (this.label != value) { this.label = value; this.Owner?.RaiseChanged(); } }
    }

    /// <summary>Cross-platform accelerator string, e.g. "Ctrl+S" / "Cmd+Q". Best-effort.</summary>
    public string? Accelerator
    {
        get => this.accelerator;
        set { if (this.accelerator != value) { this.accelerator = value; this.Owner?.RaiseChanged(); } }
    }

    /// <summary>Optional menu item icon. Stream is re-read per render.</summary>
    public Func<Stream>? Icon
    {
        get => this.iconFactory;
        set { this.iconFactory = value; this.Owner?.RaiseChanged(); }
    }

    /// <summary>
    /// Optional pill drawn after the label — a short status tag such as "New", "Pro" or "Beta".
    /// Native on macOS AppKit / MacCatalyst and Windows; Linux shows it as bracketed text because
    /// the desktop shell draws app-indicator menus itself and keeps nothing but the label.
    /// </summary>
    public TrayPill? Pill
    {
        get => this.pill;
        set { if (this.pill != value) { this.pill = value; this.Owner?.RaiseChanged(); } }
    }

    public event EventHandler? Clicked;

    internal void RaiseClicked() => this.Clicked?.Invoke(this, EventArgs.Empty);
}

public sealed class TrayCheckMenuItem : TrayMenuItemBase
{
    string label = string.Empty;
    bool isChecked;

    public TrayCheckMenuItem() { }
    public TrayCheckMenuItem(string label, bool isChecked = false)
    {
        this.Label = label;
        this.isChecked = isChecked;
    }

    public string Label
    {
        get => this.label;
        set { if (this.label != value) { this.label = value; this.Owner?.RaiseChanged(); } }
    }

    public bool IsChecked
    {
        get => this.isChecked;
        set { if (this.isChecked != value) { this.isChecked = value; this.Owner?.RaiseChanged(); } }
    }

    public event EventHandler<bool>? Toggled;

    internal void RaiseToggled(bool value)
    {
        this.isChecked = value;
        this.Toggled?.Invoke(this, value);
    }
}

public sealed class TraySeparator : TrayMenuItemBase { }

/// <summary>The colour family of a <see cref="TrayPill"/> or a <see cref="TrayProgressItem"/> bar.</summary>
public enum TrayTone
{
    /// <summary>Grey — a quiet tag.</summary>
    Neutral,
    /// <summary>The system accent colour (blue by default).</summary>
    Accent,
    Success,
    Warning,
    Danger
}

/// <summary>A short status tag drawn after a <see cref="TrayMenuItem"/>'s label.</summary>
public sealed record TrayPill(string Text, TrayTone Tone = TrayTone.Neutral);

/// <summary>
/// A non-interactive menu row showing a labelled progress bar — usage against a limit, a sync or
/// download in flight. Layout: <see cref="Label"/> on the left and <see cref="Detail"/> on the right,
/// the bar beneath, and an optional <see cref="Caption"/> under the bar.
/// </summary>
/// <remarks>
/// Changing <see cref="Value"/>, <see cref="Label"/>, <see cref="Detail"/>, <see cref="Tone"/> or the
/// caption's text repaints the row in place, so it can be driven from a timer and moves while the
/// menu is open. Adding or removing the caption changes the row's height and rebuilds the menu.
/// Drawn natively on macOS AppKit / MacCatalyst and Windows; on Linux the bar is rendered as text.
/// </remarks>
public sealed class TrayProgressItem : TrayMenuItemBase
{
    string label = string.Empty;
    double value;
    string? detail;
    string? caption;
    TrayTone tone = TrayTone.Accent;

    public TrayProgressItem() { }
    public TrayProgressItem(string label, double value = 0, string? caption = null)
    {
        this.label = label;
        this.value = Clamp(value);
        this.caption = caption;
    }

    public string Label
    {
        get => this.label;
        set { if (this.label != value) { this.label = value; this.Owner?.RaiseItemUpdated(this); } }
    }

    /// <summary>Fraction complete, 0 to 1. Out-of-range values are clamped; NaN reads as 0.</summary>
    public double Value
    {
        get => this.value;
        set
        {
            var v = Clamp(value);
            if (this.value != v) { this.value = v; this.Owner?.RaiseItemUpdated(this); }
        }
    }

    /// <summary>
    /// Text at the right of the label row. Leave null to show the percentage ("62%"); set it to an
    /// empty string to show nothing there.
    /// </summary>
    public string? Detail
    {
        get => this.detail;
        set { if (this.detail != value) { this.detail = value; this.Owner?.RaiseItemUpdated(this); } }
    }

    /// <summary>Optional secondary line under the bar, e.g. "Resets in 2 hr 14 min".</summary>
    public string? Caption
    {
        get => this.caption;
        set
        {
            if (this.caption == value) return;
            var reshaped = string.IsNullOrEmpty(this.caption) != string.IsNullOrEmpty(value);
            this.caption = value;
            if (reshaped) this.Owner?.RaiseChanged();
            else this.Owner?.RaiseItemUpdated(this);
        }
    }

    /// <summary>Bar colour. Defaults to <see cref="TrayTone.Accent"/>; switch to Warning/Danger near a limit.</summary>
    public TrayTone Tone
    {
        get => this.tone;
        set { if (this.tone != value) { this.tone = value; this.Owner?.RaiseItemUpdated(this); } }
    }

    internal bool HasCaption => !string.IsNullOrEmpty(this.caption);

    /// <summary>The text actually drawn at the right of the label row.</summary>
    internal string DisplayDetail
        => this.detail ?? Math.Round(this.value * 100, MidpointRounding.AwayFromZero).ToString("0", System.Globalization.CultureInfo.CurrentCulture) + "%";

    static double Clamp(double v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 1);
}

public sealed class TraySubmenu : TrayMenuItemBase, IEnumerable<TrayMenuItemBase>
{
    string label = string.Empty;
    public ObservableCollection<TrayMenuItemBase> Items { get; } = new();

    public TraySubmenu()
    {
        this.Items.CollectionChanged += (_, e) =>
        {
            if (e.NewItems != null && this.Owner != null)
                foreach (TrayMenuItemBase i in e.NewItems)
                    AdoptInto(this.Owner, i);
            this.Owner?.RaiseChanged();
        };
    }

    static void AdoptInto(TrayMenu owner, TrayMenuItemBase item)
    {
        item.Owner = owner;
        if (item is TraySubmenu sub)
            foreach (var child in sub.Items)
                AdoptInto(owner, child);
    }

    public TraySubmenu(string label) : this()
    {
        this.Label = label;
    }

    public string Label
    {
        get => this.label;
        set { if (this.label != value) { this.label = value; this.Owner?.RaiseChanged(); } }
    }

    public void Add(TrayMenuItemBase item) => this.Items.Add(item);

    IEnumerator<TrayMenuItemBase> IEnumerable<TrayMenuItemBase>.GetEnumerator() => this.Items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => this.Items.GetEnumerator();
}
