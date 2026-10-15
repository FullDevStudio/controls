using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls;

/// <summary>
/// A row of overlapping <see cref="Avatar"/>s - a "facepile" - that collapses everyone past
/// <see cref="Max"/> into a single "+N" chip.
/// </summary>
/// <remarks>
/// <para>Declare the avatars inline, or bind <see cref="ItemsSource"/> with an
/// <see cref="ItemTemplate"/> whose root is an <see cref="Avatar"/>. A plain list of strings needs no
/// template: each string becomes an avatar of that name.</para>
/// <para>The group owns the look of the row: <see cref="Size"/> and <see cref="Shape"/> (when set) and
/// the separator ring are pushed onto every avatar it holds, so a mixed-up list still lines up.</para>
/// </remarks>
[ContentProperty(nameof(Avatars))]
public partial class AvatarGroup : ContentView, IView
{
    readonly HorizontalStackLayout stack;
    readonly Avatar overflowChip;
    readonly List<Avatar> generated = new();
    INotifyCollectionChanged? observedSource;
    EventHandler? overflowClicked;

    public AvatarGroup()
    {
        this.stack = new HorizontalStackLayout { Spacing = 0 };
        this.overflowChip = new Avatar();
        this.overflowChip.MarkAsOverflowChip();

        // Subscribed here rather than from a defaultValueCreator: a lazily-created default never fires
        // propertyChanged, so a hook wired there would never run.
        this.Avatars = new ObservableCollection<Avatar>();
        ((ObservableCollection<Avatar>)this.Avatars).CollectionChanged += (_, _) => this.Rebuild();

        this.Content = this.stack;
        SemanticProperties.SetDescription(this.stack, null);

        StyleGuard.MarkReady(this, typeof(AvatarGroup));
        this.Rebuild();
    }


    /// <summary>Raised when the "+N" chip is tapped. Subscribing makes it tappable.</summary>
    public event EventHandler? OverflowClicked
    {
        add
        {
            this.overflowClicked += value;
            this.UpdateOverflowTappable();
        }
        remove
        {
            this.overflowClicked -= value;
            this.UpdateOverflowTappable();
        }
    }


    Microsoft.Maui.Primitives.LayoutAlignment IView.HorizontalLayoutAlignment
        => DefaultAlignment.Resolve(this, HorizontalOptionsProperty, Microsoft.Maui.Primitives.LayoutAlignment.Start);

    Microsoft.Maui.Primitives.LayoutAlignment IView.VerticalLayoutAlignment
        => DefaultAlignment.Resolve(this, VerticalOptionsProperty, Microsoft.Maui.Primitives.LayoutAlignment.Center);


    /// <summary>The avatars declared inline. Ignored while <see cref="ItemsSource"/> is set.</summary>
    public IList<Avatar> Avatars { get; }


    /// <summary>The avatars currently drawn, in order - not counting the "+N" chip.</summary>
    public IReadOnlyList<Avatar> VisibleAvatars
        => this.stack.Children.OfType<Avatar>().Where(x => !ReferenceEquals(x, this.overflowChip)).ToList();


    /// <summary>The overflow chip itself, for tests and for apps that want to restyle it.</summary>
    internal Avatar OverflowChip => this.overflowChip;


    void OnItemsSourceChanged(IEnumerable? oldValue, IEnumerable? newValue)
    {
        if (this.observedSource is not null)
            this.observedSource.CollectionChanged -= this.OnSourceCollectionChanged;

        this.observedSource = newValue as INotifyCollectionChanged;
        if (this.observedSource is not null)
            this.observedSource.CollectionChanged += this.OnSourceCollectionChanged;

        this.Rebuild();
    }


    void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => this.Rebuild();


    IReadOnlyList<Avatar> AllAvatars()
    {
        if (this.ItemsSource is null)
            return this.Avatars.ToList();

        this.generated.Clear();
        foreach (var item in this.ItemsSource)
            this.generated.Add(this.CreateAvatar(item));

        return this.generated;
    }


    Avatar CreateAvatar(object? item)
    {
        var template = this.ItemTemplate;
        if (template is DataTemplateSelector selector)
            template = selector.SelectTemplate(item, this);

        if (template is null)
        {
            return item switch
            {
                Avatar avatar => avatar,
                _ => new Avatar { Name = item?.ToString() }
            };
        }

        if (template.CreateContent() is not Avatar created)
            throw new InvalidOperationException($"{nameof(AvatarGroup)}.{nameof(ItemTemplate)} must produce an {nameof(Avatar)}.");

        created.BindingContext = item;
        return created;
    }


    internal void Rebuild()
    {
        if (!StyleGuard.IsReady(this, typeof(AvatarGroup)))
            return;

        var all = this.AllAvatars();
        var max = this.Max;
        var visibleCount = max > 0 ? Math.Min(max, all.Count) : all.Count;
        var total = Math.Max(this.TotalCount, all.Count);
        var overflow = total - visibleCount;

        this.stack.Children.Clear();
        for (var i = 0; i < visibleCount; i++)
        {
            this.ApplyGroupLook(all[i], i);
            this.stack.Children.Add(all[i]);
        }

        if (overflow > 0)
        {
            // The chip matches the row it closes, even when the group itself leaves size and shape alone.
            var reference = all.Count > 0 ? all[0] : null;
            this.overflowChip.Size = reference?.Size ?? 40;
            this.overflowChip.Shape = reference?.Shape ?? AvatarShape.Circle;
            this.overflowChip.Initials = AvatarText.FormatOverflow(this.OverflowFormat, overflow);
            this.ApplyGroupLook(this.overflowChip, visibleCount);
            this.stack.Children.Add(this.overflowChip);
        }

        this.SetValue(OverflowCountPropertyKey, Math.Max(0, overflow));
        SemanticProperties.SetDescription(this,
            overflow > 0 ? $"{visibleCount} people and {overflow} more" : $"{visibleCount} people");
    }


    void ApplyGroupLook(Avatar avatar, int index)
    {
        if (this.Size > 0)
            avatar.Size = this.Size;
        if (this.Shape is AvatarShape shape)
            avatar.Shape = shape;

        avatar.RingThickness = Math.Max(0, this.RingThickness);
        avatar.RingColor = this.RingColor;

        var overlap = this.Overlap >= 0 ? this.Overlap : avatar.Size * 0.25;
        avatar.Margin = index == 0 ? new Thickness(0) : new Thickness(-overlap, 0, 0, 0);
    }


    void OnOverflowTapped(object? sender, EventArgs e)
    {
        this.overflowClicked?.Invoke(this, EventArgs.Empty);

        var command = this.OverflowCommand;
        var parameter = this.OverflowCount;
        if (command?.CanExecute(parameter) == true)
            command.Execute(parameter);
    }


    bool overflowHooked;

    void UpdateOverflowTappable()
    {
        var wanted = this.overflowClicked is not null || this.OverflowCommand is not null;
        if (wanted == this.overflowHooked)
            return;

        if (wanted)
            this.overflowChip.Clicked += this.OnOverflowTapped;
        else
            this.overflowChip.Clicked -= this.OnOverflowTapped;

        this.overflowHooked = wanted;
    }
}
