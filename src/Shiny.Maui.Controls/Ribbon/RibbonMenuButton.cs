using System.Diagnostics.CodeAnalysis;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;

namespace Shiny.Maui.Controls.Ribbons;

/// <summary>
/// One line inside a ribbon dropdown.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="RibbonItem"/>. A menu line has no size, no icon-over-label form and
/// cannot itself open a group; sharing the base would offer an author a dozen properties that do
/// nothing inside a menu. Entries can nest, and a nested one flies out as a submenu.
/// </remarks>
[ContentProperty(nameof(Children))]
public class RibbonMenuEntry : BindableObject
{
    readonly ObservableCollection<RibbonMenuEntry> children = new();

    public RibbonMenuEntry() => this.children.CollectionChanged += this.OnChildrenChanged;

    /// <summary>Raised when a property the menu draws from changes.</summary>
    internal event EventHandler? Changed;

    void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e) => this.RaiseChanged();

    void RaiseChanged() => this.Changed?.Invoke(this, EventArgs.Empty);

    static BindableProperty Redraw(string name, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type returnType, object? defaultValue = null)
        => BindableProperty.Create(
            name, returnType, typeof(RibbonMenuEntry), defaultValue,
            propertyChanged: (b, _, _) => ((RibbonMenuEntry)b).RaiseChanged()
        );


    public static readonly BindableProperty TextProperty = Redraw(nameof(Text), typeof(string));
    public static readonly BindableProperty IconProperty = Redraw(nameof(Icon), typeof(ImageSource));
    public static readonly BindableProperty IsEnabledProperty = Redraw(nameof(IsEnabled), typeof(bool), true);
    public static readonly BindableProperty IsVisibleProperty = Redraw(nameof(IsVisible), typeof(bool), true);
    public static readonly BindableProperty IsSeparatorProperty = Redraw(nameof(IsSeparator), typeof(bool), false);
    public static readonly BindableProperty IsCheckedProperty = Redraw(nameof(IsChecked), typeof(bool), false);
    public static readonly BindableProperty CommandProperty = Redraw(nameof(Command), typeof(ICommand));
    public static readonly BindableProperty CommandParameterProperty = Redraw(nameof(CommandParameter), typeof(object));


    /// <summary>Raised when the line is picked.</summary>
    public event EventHandler? Clicked;

    public string? Text
    {
        get => (string?)this.GetValue(TextProperty);
        set => this.SetValue(TextProperty, value);
    }

    public ImageSource? Icon
    {
        get => (ImageSource?)this.GetValue(IconProperty);
        set => this.SetValue(IconProperty, value);
    }

    public bool IsEnabled
    {
        get => (bool)this.GetValue(IsEnabledProperty);
        set => this.SetValue(IsEnabledProperty, value);
    }

    public bool IsVisible
    {
        get => (bool)this.GetValue(IsVisibleProperty);
        set => this.SetValue(IsVisibleProperty, value);
    }

    /// <summary>Draws a divider instead of a line. Text, icon and children are ignored.</summary>
    public bool IsSeparator
    {
        get => (bool)this.GetValue(IsSeparatorProperty);
        set => this.SetValue(IsSeparatorProperty, value);
    }

    /// <summary>Draws a tick beside the line, for a menu that is a set of choices rather than actions.</summary>
    public bool IsChecked
    {
        get => (bool)this.GetValue(IsCheckedProperty);
        set => this.SetValue(IsCheckedProperty, value);
    }

    public ICommand? Command
    {
        get => (ICommand?)this.GetValue(CommandProperty);
        set => this.SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => this.GetValue(CommandParameterProperty);
        set => this.SetValue(CommandParameterProperty, value);
    }

    /// <summary>Nested lines. When any are present the entry flies out a submenu instead of acting.</summary>
    public IList<RibbonMenuEntry> Children => this.children;

    /// <summary>True when picking this line opens a submenu rather than running something.</summary>
    public bool HasChildren => !this.IsSeparator && this.children.Any(x => x.IsVisible);


    /// <summary>Picks the line. The seam a test invokes through.</summary>
    public void Invoke()
    {
        if (!this.IsEnabled || this.IsSeparator)
            return;

        var parameter = this.CommandParameter;
        if (this.Command?.CanExecute(parameter) == true)
            this.Command.Execute(parameter);

        this.Clicked?.Invoke(this, EventArgs.Empty);
    }
}


/// <summary>
/// A button whose whole face opens a dropdown — no default action of its own.
/// </summary>
/// <example>
/// <code language="xaml">
/// &lt;shiny:RibbonMenuButton Text="Insert" Icon="insert.png"&gt;
///     &lt;shiny:RibbonMenuEntry Text="Table" Command="{Binding InsertTable}" /&gt;
///     &lt;shiny:RibbonMenuEntry Text="Picture" Command="{Binding InsertPicture}" /&gt;
///     &lt;shiny:RibbonMenuEntry IsSeparator="True" /&gt;
///     &lt;shiny:RibbonMenuEntry Text="Chart"&gt;
///         &lt;shiny:RibbonMenuEntry Text="Bar" Command="{Binding InsertChart}" CommandParameter="Bar" /&gt;
///         &lt;shiny:RibbonMenuEntry Text="Line" Command="{Binding InsertChart}" CommandParameter="Line" /&gt;
///     &lt;/shiny:RibbonMenuEntry&gt;
/// &lt;/shiny:RibbonMenuButton&gt;
/// </code>
/// </example>
[ContentProperty(nameof(Menu))]
public class RibbonMenuButton : RibbonItem
{
    readonly ObservableCollection<RibbonMenuEntry> menu = new();

    public RibbonMenuButton()
    {
        this.menu.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (RibbonMenuEntry entry in e.OldItems)
                    entry.Changed -= this.OnEntryChanged;
            }

            if (e.NewItems is not null)
            {
                foreach (RibbonMenuEntry entry in e.NewItems)
                    entry.Changed += this.OnEntryChanged;
            }

            this.RaiseChanged();
        };
    }

    void OnEntryChanged(object? sender, EventArgs e) => this.RaiseChanged();

    /// <summary>The dropdown's lines.</summary>
    public IList<RibbonMenuEntry> Menu => this.menu;

    /// <summary>The lines that are actually drawn.</summary>
    internal IReadOnlyList<RibbonMenuEntry> VisibleMenu => this.menu.Where(x => x.IsVisible).ToList();
}


/// <summary>
/// A button split in two: pressing the face runs the default action, pressing the chevron opens the
/// dropdown.
/// </summary>
/// <remarks>
/// The shape to reach for when one choice out of a set is overwhelmingly the common one — AutoSum
/// over the other aggregates, paste over paste-special. Making that choice a menu pick puts a click
/// in front of it every single time; making the others invisible loses them.
/// </remarks>
public class RibbonSplitButton : RibbonMenuButton
{
    public static readonly BindableProperty CommandProperty = Redraw(
        nameof(Command), typeof(ICommand), typeof(RibbonSplitButton)
    );

    public static readonly BindableProperty CommandParameterProperty = Redraw(
        nameof(CommandParameter), typeof(object), typeof(RibbonSplitButton)
    );


    /// <summary>Raised when the button's face — not its chevron — is pressed.</summary>
    public event EventHandler? Clicked;

    /// <summary>The default action, run when the face is pressed.</summary>
    public ICommand? Command
    {
        get => (ICommand?)this.GetValue(CommandProperty);
        set => this.SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => this.GetValue(CommandParameterProperty);
        set => this.SetValue(CommandParameterProperty, value);
    }


    /// <summary>Presses the face. Opening the dropdown is a separate gesture and does not come through here.</summary>
    public void Invoke()
    {
        if (!this.IsEnabled)
            return;

        var parameter = this.CommandParameter;
        if (this.Command?.CanExecute(parameter) == true)
            this.Command.Execute(parameter);

        this.Clicked?.Invoke(this, EventArgs.Empty);
    }
}
