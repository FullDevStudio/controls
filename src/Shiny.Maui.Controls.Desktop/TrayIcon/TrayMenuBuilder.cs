namespace Shiny.Maui.Controls.Desktop.TrayIcon;

public sealed class TrayMenuBuilder
{
    readonly TrayMenu menu = new();

    public TrayMenuBuilder Item(string label, Action clicked)
    {
        this.menu.Items.Add(new TrayMenuItem(label, clicked));
        return this;
    }

    public TrayMenuBuilder Item(TrayMenuItem item)
    {
        this.menu.Items.Add(item);
        return this;
    }

    public TrayMenuBuilder Check(string label, bool isChecked, Action<bool> toggled)
    {
        var item = new TrayCheckMenuItem(label, isChecked);
        item.Toggled += (_, v) => toggled(v);
        this.menu.Items.Add(item);
        return this;
    }

    public TrayMenuBuilder Progress(string label, double value, string? caption = null)
        => this.Progress(new TrayProgressItem(label, value, caption));

    /// <summary>Adds a progress row. Keep the reference to update its value later.</summary>
    public TrayMenuBuilder Progress(TrayProgressItem item)
    {
        this.menu.Items.Add(item);
        return this;
    }

    public TrayMenuBuilder Separator()
    {
        this.menu.Items.Add(new TraySeparator());
        return this;
    }

    public TrayMenuBuilder Submenu(string label, Action<TraySubmenuBuilder> configure)
    {
        var sub = new TraySubmenu(label);
        var b = new TraySubmenuBuilder(sub);
        configure(b);
        this.menu.Items.Add(sub);
        return this;
    }

    public TrayMenu Build() => this.menu;
}

public sealed class TraySubmenuBuilder
{
    readonly TraySubmenu submenu;

    internal TraySubmenuBuilder(TraySubmenu submenu) => this.submenu = submenu;

    public TraySubmenuBuilder Item(string label, Action clicked)
    {
        this.submenu.Items.Add(new TrayMenuItem(label, clicked));
        return this;
    }

    public TraySubmenuBuilder Item(TrayMenuItem item)
    {
        this.submenu.Items.Add(item);
        return this;
    }

    public TraySubmenuBuilder Check(string label, bool isChecked, Action<bool> toggled)
    {
        var item = new TrayCheckMenuItem(label, isChecked);
        item.Toggled += (_, v) => toggled(v);
        this.submenu.Items.Add(item);
        return this;
    }

    public TraySubmenuBuilder Progress(string label, double value, string? caption = null)
        => this.Progress(new TrayProgressItem(label, value, caption));

    /// <summary>Adds a progress row. Keep the reference to update its value later.</summary>
    public TraySubmenuBuilder Progress(TrayProgressItem item)
    {
        this.submenu.Items.Add(item);
        return this;
    }

    public TraySubmenuBuilder Separator()
    {
        this.submenu.Items.Add(new TraySeparator());
        return this;
    }

    public TraySubmenuBuilder Submenu(string label, Action<TraySubmenuBuilder> configure)
    {
        var sub = new TraySubmenu(label);
        var b = new TraySubmenuBuilder(sub);
        configure(b);
        this.submenu.Items.Add(sub);
        return this;
    }
}
