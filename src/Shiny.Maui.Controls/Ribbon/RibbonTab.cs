using System.Diagnostics.CodeAnalysis;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Shiny.Maui.Controls.Ribbons;

/// <summary>
/// One tab of a <see cref="Ribbon"/> and the groups it shows.
/// </summary>
/// <example>
/// <code language="xaml">
/// &lt;shiny:RibbonTab Title="Home"&gt;
///     &lt;shiny:RibbonGroup Title="Clipboard"&gt;
///         &lt;shiny:RibbonButton Text="Paste" Icon="paste.png" Command="{Binding Paste}" /&gt;
///     &lt;/shiny:RibbonGroup&gt;
/// &lt;/shiny:RibbonTab&gt;
/// </code>
/// </example>
[ContentProperty(nameof(Groups))]
public class RibbonTab : BindableObject
{
    readonly ObservableCollection<RibbonGroup> groups = new();

    public RibbonTab() => this.groups.CollectionChanged += this.OnGroupsChanged;

    /// <summary>Raised when anything the ribbon draws this tab from changes, its groups included.</summary>
    internal event EventHandler? Changed;

    void RaiseChanged() => this.Changed?.Invoke(this, EventArgs.Empty);

    static BindableProperty Redraw(string name, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] Type returnType, object? defaultValue = null)
        => BindableProperty.Create(
            name, returnType, typeof(RibbonTab), defaultValue,
            propertyChanged: (b, _, _) => ((RibbonTab)b).RaiseChanged()
        );


    public static readonly BindableProperty TitleProperty = Redraw(nameof(Title), typeof(string));
    public static readonly BindableProperty KeyProperty = Redraw(nameof(Key), typeof(string));
    public static readonly BindableProperty IsVisibleProperty = Redraw(nameof(IsVisible), typeof(bool), true);
    public static readonly BindableProperty IsEnabledProperty = Redraw(nameof(IsEnabled), typeof(bool), true);
    public static readonly BindableProperty ContextTitleProperty = Redraw(nameof(ContextTitle), typeof(string));
    public static readonly BindableProperty ContextColorProperty = Redraw(nameof(ContextColor), typeof(Color));
    public static readonly BindableProperty AutomationIdProperty = Redraw(nameof(AutomationId), typeof(string));


    /// <summary>The label on the tab strip.</summary>
    public string? Title
    {
        get => (string?)this.GetValue(TitleProperty);
        set => this.SetValue(TitleProperty, value);
    }

    /// <summary>
    /// A stable name for the tab, for selecting it with <see cref="Ribbon.SelectTab(string)"/> without
    /// depending on its position or its display title.
    /// </summary>
    public string? Key
    {
        get => (string?)this.GetValue(KeyProperty);
        set => this.SetValue(KeyProperty, value);
    }

    /// <summary>
    /// Whether the tab appears at all. This is how a contextual tab works: bind it to whatever the tab
    /// is about being selected — a picture, a table, a chart — and the ribbon shows it, moves to it,
    /// and moves off it again when the selection goes away.
    /// </summary>
    public bool IsVisible
    {
        get => (bool)this.GetValue(IsVisibleProperty);
        set => this.SetValue(IsVisibleProperty, value);
    }

    /// <summary>A disabled tab is drawn dimmed on the strip and cannot be selected.</summary>
    public bool IsEnabled
    {
        get => (bool)this.GetValue(IsEnabledProperty);
        set => this.SetValue(IsEnabledProperty, value);
    }

    /// <summary>
    /// Marks the tab contextual and captions the band drawn above it — "Table Tools", "Picture Tools".
    /// Setting it is what makes a tab contextual; leave it null for a permanent one.
    /// </summary>
    public string? ContextTitle
    {
        get => (string?)this.GetValue(ContextTitleProperty);
        set => this.SetValue(ContextTitleProperty, value);
    }

    /// <summary>
    /// The accent for a contextual tab's band and underline. Falls back to the theme's tertiary
    /// colour, which is deliberately not the primary one the permanent tabs use.
    /// </summary>
    public Color? ContextColor
    {
        get => (Color?)this.GetValue(ContextColorProperty);
        set => this.SetValue(ContextColorProperty, value);
    }

    public string? AutomationId
    {
        get => (string?)this.GetValue(AutomationIdProperty);
        set => this.SetValue(AutomationIdProperty, value);
    }

    /// <summary>The groups on this tab, left to right.</summary>
    public IList<RibbonGroup> Groups => this.groups;

    /// <summary>True when <see cref="ContextTitle"/> is set — the tab is one of a contextual set.</summary>
    public bool IsContextual => !string.IsNullOrWhiteSpace(this.ContextTitle);

    /// <summary>The groups that are actually drawn.</summary>
    internal IReadOnlyList<RibbonGroup> VisibleGroups => this.groups.Where(x => x.IsVisible).ToList();

    /// <summary>Whether the ribbon may move to this tab.</summary>
    internal bool IsSelectable => this.IsVisible && this.IsEnabled;


    void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (RibbonGroup group in e.OldItems)
                group.Changed -= this.OnGroupChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (RibbonGroup group in e.NewItems)
                group.Changed += this.OnGroupChanged;
        }

        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var group in this.groups)
            {
                group.Changed -= this.OnGroupChanged;
                group.Changed += this.OnGroupChanged;
            }
        }

        this.RaiseChanged();
    }

    void OnGroupChanged(object? sender, EventArgs e) => this.RaiseChanged();


    /// <summary>Pushes a binding context down to the groups. See <see cref="RibbonGroup.ApplyBindingContext"/>.</summary>
    internal void ApplyBindingContext(object? context)
    {
        foreach (var group in this.groups)
        {
            SetInheritedBindingContext(group, context);
            group.ApplyBindingContext(context);
        }
    }
}
