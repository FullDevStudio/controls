using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Input;
using Shiny.Maui.Controls.Collections;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.MotionIcons;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls;

/// <summary>
/// A Cover Flow carousel: the current item faces you in the centre while the items either side turn
/// toward it in 3D and stack up behind one another, like album covers on a shelf.
/// </summary>
/// <example>
/// <code>
/// &lt;shiny:CoverFlowView ItemsSource="{Binding Albums}" CurrentPosition="{Binding Position}"
///                       ItemWidth="200" ItemHeight="200" ShowReflection="True" Loop="True"&gt;
///     &lt;shiny:CoverFlowView.ItemTemplate&gt;
///         &lt;DataTemplate&gt;&lt;Image Source="{Binding Art}" Aspect="AspectFill" /&gt;&lt;/DataTemplate&gt;
///     &lt;/shiny:CoverFlowView.ItemTemplate&gt;
///     &lt;shiny:CoverFlowView.CaptionTemplate&gt;
///         &lt;DataTemplate&gt;&lt;Label Text="{Binding Title}" HorizontalTextAlignment="Center" /&gt;&lt;/DataTemplate&gt;
///     &lt;/shiny:CoverFlowView.CaptionTemplate&gt;
/// &lt;/shiny:CoverFlowView&gt;
/// </code>
/// </example>
/// <remarks>
/// <para>
/// It is drawn entirely with MAUI transforms - <c>TranslationX</c>, <c>RotationY</c>, <c>Scale</c> and
/// <c>Opacity</c> - so the same code runs on every platform with no native handler. The position is
/// continuous: a drag moves it a fraction of an item at a time and every cover follows live; on release
/// it coasts to the nearest whole item with an ease-out on the window's shared frame ticker.
/// </para>
/// <para>
/// Only the covers within <see cref="VisibleItems"/> of the centre (plus one either side, which fades
/// in) are realized, and released covers are recycled. Covers never take touches themselves: the stage
/// hit-tests a tap against the covers' projected footprints, front to back, so tapping a side cover
/// brings it to the centre and tapping the centre cover selects it.
/// </para>
/// </remarks>
public class CoverFlowView : ContentView
{
    const int MaximumZ = 100_000;

    /// <summary>
    /// Appended to the control's own <see cref="Element.AutomationId"/>, with the item's index in
    /// <see cref="ItemsSource"/>, to name each realized cover: <c>&lt;id&gt;_Item_3</c>. Only covers near the
    /// centre exist. Covers take no touches themselves; tapping one's bounds still hits it.
    /// </summary>
    public const string ItemAutomationIdSuffix = "_Item";

    /// <summary>Names the caption area under the centre cover.</summary>
    public const string CaptionAutomationIdSuffix = "_Caption";

    readonly Grid root;
    readonly CoverFlowStage stage;
    readonly ContentView captionHost;
    readonly BoxView surfaceProbe;
    readonly Dictionary<int, CoverFlowSlot> realized = [];
    readonly List<CoverFlowSlot> pool = [];
    readonly List<(long Ticks, double Position)> samples = [];
    readonly Stopwatch clock = Stopwatch.StartNew();

    IDisposable? collectionSubscription;
    IList<object> items = [];
    double position;
    double dragStart;
    int reportedIndex = -1;
    int centreVirtual = int.MinValue;
    bool syncing;
    Point stageCentre;
    double blockHeight;

    MotionTicker? ticker;
    double animationFrom;
    double animationTo;
    double animationElapsed;
    double animationLength;

    public CoverFlowView()
    {
        this.surfaceProbe = new BoxView { IsVisible = false, WidthRequest = 0, HeightRequest = 0, InputTransparent = true };
        this.surfaceProbe.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == BoxView.ColorProperty.PropertyName)
            {
                var surface = this.Surface;
                foreach (var slot in this.realized.Values.Concat(this.pool))
                    slot.SetSurface(surface);
            }
        };

        this.stage = new CoverFlowStage(this) { Children = { this.surfaceProbe } };
        this.captionHost = new ContentView { IsVisible = false };

        this.root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        this.root.Add(this.stage, 0, 0);
        this.root.Add(this.captionHost, 0, 1);
        this.Content = this.root;

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, e) => this.OnPan(e);
        this.GestureRecognizers.Add(pan);

        var tap = new TapGestureRecognizer();
        // A tap with no position (an accessibility activation, UI automation) means the control as a
        // whole was activated - treat it as a tap on the centre cover, which selects it.
        tap.Tapped += (_, e) =>
        {
            if (e.GetPosition(this.stage) is { } point)
                this.HandleTap(point);
            else
                this.SelectCentre();
        };
        this.GestureRecognizers.Add(tap);

        this.Unloaded += (_, _) => this.StopAnimation(snap: true);

        AutomationIds.Link(this, this.captionHost, CaptionAutomationIdSuffix);
        AutomationIds.Track(this, () =>
        {
            foreach (var slot in this.realized.Values)
                this.NameSlot(slot);
        });

        ThemeProbe.Tint(this.surfaceProbe, BoxView.ColorProperty, null, ShinyThemeKeys.Color.Surface);
        StyleGuard.MarkReady(this, typeof(CoverFlowView));
        this.Rebuild();
    }


    // ---------------------------------------------------------------------------------------------
    // Properties
    // ---------------------------------------------------------------------------------------------

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable), typeof(CoverFlowView), null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.OnItemsSourceChanged((IEnumerable?)n)));

    /// <summary>The items to show. Changes to an <see cref="INotifyCollectionChanged"/> source are followed.</summary>
    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)this.GetValue(ItemsSourceProperty);
        set => this.SetValue(ItemsSourceProperty, value);
    }

    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
        nameof(ItemTemplate), typeof(DataTemplate), typeof(CoverFlowView), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.Rebuild()));

    /// <summary>How each cover is drawn. A <see cref="DataTemplateSelector"/> is honoured.</summary>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)this.GetValue(ItemTemplateProperty);
        set => this.SetValue(ItemTemplateProperty, value);
    }

    public static readonly BindableProperty CaptionTemplateProperty = BindableProperty.Create(
        nameof(CaptionTemplate), typeof(DataTemplate), typeof(CoverFlowView), null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.RebuildCaption()));

    /// <summary>Drawn once beneath the covers, bound to the item currently in the centre.</summary>
    public DataTemplate? CaptionTemplate
    {
        get => (DataTemplate?)this.GetValue(CaptionTemplateProperty);
        set => this.SetValue(CaptionTemplateProperty, value);
    }

    public static readonly BindableProperty ItemWidthProperty = BindableProperty.Create(
        nameof(ItemWidth), typeof(double), typeof(CoverFlowView), 200d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.OnSizeChanged()));

    /// <summary>The width of each cover.</summary>
    public double ItemWidth
    {
        get => (double)this.GetValue(ItemWidthProperty);
        set => this.SetValue(ItemWidthProperty, value);
    }

    public static readonly BindableProperty ItemHeightProperty = BindableProperty.Create(
        nameof(ItemHeight), typeof(double), typeof(CoverFlowView), 200d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.OnSizeChanged()));

    /// <summary>The height of each cover.</summary>
    public double ItemHeight
    {
        get => (double)this.GetValue(ItemHeightProperty);
        set => this.SetValue(ItemHeightProperty, value);
    }

    public static readonly BindableProperty CurrentPositionProperty = BindableProperty.Create(
        nameof(CurrentPosition), typeof(int), typeof(CoverFlowView), 0, BindingMode.TwoWay,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.OnCurrentPositionChanged((int)n)));

    /// <summary>The index of the centred item. Out-of-range values are clamped, or wrapped when <see cref="Loop"/> is on.</summary>
    public int CurrentPosition
    {
        get => (int)this.GetValue(CurrentPositionProperty);
        set => this.SetValue(CurrentPositionProperty, value);
    }

    public static readonly BindableProperty SideAngleProperty = BindableProperty.Create(
        nameof(SideAngle), typeof(double), typeof(CoverFlowView), 60d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.ApplyTransforms()));

    /// <summary>Degrees the side covers turn toward the centre. Their outer edges come toward the viewer.</summary>
    public double SideAngle
    {
        get => (double)this.GetValue(SideAngleProperty);
        set => this.SetValue(SideAngleProperty, value);
    }

    public static readonly BindableProperty SideScaleProperty = BindableProperty.Create(
        nameof(SideScale), typeof(double), typeof(CoverFlowView), 0.85d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.ApplyTransforms()));

    /// <summary>The scale of every cover from one step out of the centre.</summary>
    public double SideScale
    {
        get => (double)this.GetValue(SideScaleProperty);
        set => this.SetValue(SideScaleProperty, value);
    }

    public static readonly BindableProperty SideOpacityProperty = BindableProperty.Create(
        nameof(SideOpacity), typeof(double), typeof(CoverFlowView), 1d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.ApplyTransforms()));

    /// <summary>The opacity of the outermost visible covers; covers between fade linearly toward it.</summary>
    public double SideOpacity
    {
        get => (double)this.GetValue(SideOpacityProperty);
        set => this.SetValue(SideOpacityProperty, value);
    }

    public static readonly BindableProperty CenterSpacingProperty = BindableProperty.Create(
        nameof(CenterSpacing), typeof(double), typeof(CoverFlowView), 130d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.OnSizeChanged()));

    /// <summary>
    /// The distance from the centre cover's centre to the first side cover's centre. It is also how far
    /// a drag has to travel to move one item.
    /// </summary>
    public double CenterSpacing
    {
        get => (double)this.GetValue(CenterSpacingProperty);
        set => this.SetValue(CenterSpacingProperty, value);
    }

    public static readonly BindableProperty ItemSpacingProperty = BindableProperty.Create(
        nameof(ItemSpacing), typeof(double), typeof(CoverFlowView), 50d,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.OnSizeChanged()));

    /// <summary>The distance between neighbouring covers in the side stacks.</summary>
    public double ItemSpacing
    {
        get => (double)this.GetValue(ItemSpacingProperty);
        set => this.SetValue(ItemSpacingProperty, value);
    }

    public static readonly BindableProperty VisibleItemsProperty = BindableProperty.Create(
        nameof(VisibleItems), typeof(int), typeof(CoverFlowView), 4,
        validateValue: (_, v) => (int)v >= 0,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.OnSizeChanged()));

    /// <summary>How many covers show on each side of the centre. Only these are realized.</summary>
    public int VisibleItems
    {
        get => (int)this.GetValue(VisibleItemsProperty);
        set => this.SetValue(VisibleItemsProperty, value);
    }

    public static readonly BindableProperty LoopProperty = BindableProperty.Create(
        nameof(Loop), typeof(bool), typeof(CoverFlowView), false,
        propertyChanged: (b, o, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.OnLoopChanged((bool)o)));

    /// <summary>Wraps from the last item back to the first, in both directions.</summary>
    public bool Loop
    {
        get => (bool)this.GetValue(LoopProperty);
        set => this.SetValue(LoopProperty, value);
    }

    public static readonly BindableProperty ShowReflectionProperty = BindableProperty.Create(
        nameof(ShowReflection), typeof(bool), typeof(CoverFlowView), false,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.Rebuild()));

    /// <summary>Draws a mirrored copy of each cover beneath it, fading into the surface.</summary>
    public bool ShowReflection
    {
        get => (bool)this.GetValue(ShowReflectionProperty);
        set => this.SetValue(ShowReflectionProperty, value);
    }

    public static readonly BindableProperty ReflectionOpacityProperty = BindableProperty.Create(
        nameof(ReflectionOpacity), typeof(double), typeof(CoverFlowView), 0.3d,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<CoverFlowView>(b, v => v.OnReflectionOpacityChanged((double)n)));

    /// <summary>How strong the reflection is at its top edge.</summary>
    public double ReflectionOpacity
    {
        get => (double)this.GetValue(ReflectionOpacityProperty);
        set => this.SetValue(ReflectionOpacityProperty, value);
    }

    public static readonly BindableProperty AnimationDurationProperty = BindableProperty.Create(
        nameof(AnimationDuration), typeof(int), typeof(CoverFlowView), 300);

    /// <summary>Milliseconds a move to another cover takes. 0 jumps.</summary>
    public int AnimationDuration
    {
        get => (int)this.GetValue(AnimationDurationProperty);
        set => this.SetValue(AnimationDurationProperty, value);
    }

    public static readonly BindableProperty IsSwipeEnabledProperty = BindableProperty.Create(
        nameof(IsSwipeEnabled), typeof(bool), typeof(CoverFlowView), true);

    /// <summary>Whether dragging moves through the covers. Taps and the navigation methods still work when off.</summary>
    public bool IsSwipeEnabled
    {
        get => (bool)this.GetValue(IsSwipeEnabledProperty);
        set => this.SetValue(IsSwipeEnabledProperty, value);
    }

    public static readonly BindableProperty PositionChangedCommandProperty = BindableProperty.Create(
        nameof(PositionChangedCommand), typeof(ICommand), typeof(CoverFlowView));

    /// <summary>Executed with the new index whenever a different item settles in the centre.</summary>
    public ICommand? PositionChangedCommand
    {
        get => (ICommand?)this.GetValue(PositionChangedCommandProperty);
        set => this.SetValue(PositionChangedCommandProperty, value);
    }

    public static readonly BindableProperty ItemSelectedCommandProperty = BindableProperty.Create(
        nameof(ItemSelectedCommand), typeof(ICommand), typeof(CoverFlowView));

    /// <summary>Executed with the item when the centre cover is tapped.</summary>
    public ICommand? ItemSelectedCommand
    {
        get => (ICommand?)this.GetValue(ItemSelectedCommandProperty);
        set => this.SetValue(ItemSelectedCommandProperty, value);
    }

    /// <summary>Raised with the new index whenever a different item settles in the centre.</summary>
    public event EventHandler<int>? PositionChanged;

    /// <summary>Raised when the centre cover is tapped.</summary>
    public event EventHandler<CollectionItemEventArgs>? ItemSelected;


    // ---------------------------------------------------------------------------------------------
    // Navigation
    // ---------------------------------------------------------------------------------------------

    /// <summary>Moves one item forward. Stops at the end unless <see cref="Loop"/> is on.</summary>
    public void Next() => this.Step(1);

    /// <summary>Moves one item back. Stops at the start unless <see cref="Loop"/> is on.</summary>
    public void Previous() => this.Step(-1);

    /// <summary>Brings <paramref name="index"/> to the centre - the short way round when looping.</summary>
    public void GoTo(int index, bool animate = true)
    {
        if (this.items.Count == 0)
            return;

        var target = this.Loop
            ? CoverFlowGeometry.NearestVirtual(this.TargetPosition, index, this.items.Count, true)
            : Math.Clamp(index, 0, this.items.Count - 1);

        this.MoveTo(target, animate);
    }

    void Step(int delta)
    {
        if (this.items.Count == 0)
            return;

        var target = Math.Round(this.TargetPosition, MidpointRounding.AwayFromZero) + delta;
        if (!this.Loop)
            target = Math.Clamp(target, 0, this.items.Count - 1);

        this.MoveTo(target, true);
    }

    /// <summary>Where the position is heading: the end of a running animation, or where it is now.</summary>
    double TargetPosition => this.ticker is not null ? this.animationTo : this.position;


    // ---------------------------------------------------------------------------------------------
    // Test seams
    // ---------------------------------------------------------------------------------------------

    /// <summary>The continuous (and, when looping, unwrapped) position.</summary>
    internal double Position => this.position;

    /// <summary>Lets a headless test start real animations, which otherwise need a handler.</summary>
    internal bool AnimateWithoutHandler { get; set; }

    bool CanAnimate => this.Handler is not null || this.AnimateWithoutHandler;

    /// <summary>Where the centre cover sits when it is at rest, in stage coordinates.</summary>
    internal Point StageCentre => this.stageCentre;

    /// <summary>Whether a move is still animating.</summary>
    internal bool IsAnimating => this.ticker is not null;

    /// <summary>Advances a running animation by <paramref name="milliseconds"/>, as frames would.</summary>
    internal void AdvanceAnimation(double milliseconds)
    {
        if (this.ticker is not null)
            this.OnTick(TimeSpan.FromMilliseconds(milliseconds));
    }

    /// <summary>The covers currently realized, keyed by virtual index.</summary>
    internal IReadOnlyDictionary<int, CoverFlowSlot> RealizedSlots => this.realized;

    internal CoverFlowLayout CoverLayout => new(
        this.CenterSpacing, this.ItemSpacing, this.SideAngle, this.SideScale, this.SideOpacity, this.VisibleItems);

    internal View? CaptionView => this.captionHost.Content;

    /// <summary>Simulates a drag: <paramref name="totalX"/> is the distance from where it started.</summary>
    internal void Drag(GestureStatus status, double totalX) => this.OnPan(status, totalX);


    // ---------------------------------------------------------------------------------------------
    // Items
    // ---------------------------------------------------------------------------------------------

    void OnItemsSourceChanged(IEnumerable? source)
    {
        // Weak: an ItemsSource is usually a view model's collection, which outlives the page.
        this.collectionSubscription?.Dispose();
        this.collectionSubscription = WeakEventSubscription.CollectionChanged(source, this.OnCollectionChanged);
        this.Rebuild();
    }

    void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (this.Dispatcher is { IsDispatchRequired: true } dispatcher)
        {
            dispatcher.Dispatch(() => this.OnCollectionChanged(sender, e));
            return;
        }

        this.Rebuild();
    }

    /// <summary>
    /// Re-reads the items and starts the covers again from <see cref="CurrentPosition"/>. Every
    /// structural change goes through here: they are rare next to moving, and a full rebuild cannot
    /// leave a cover showing the wrong item.
    /// </summary>
    void Rebuild()
    {
        this.StopAnimation(snap: false);
        this.ReleaseAll(discard: true);

        this.items = this.ReadItems();
        this.centreVirtual = int.MinValue;
        this.position = this.items.Count == 0
            ? 0
            : CoverFlowGeometry.IndexAt(this.CurrentPosition, this.items.Count, this.Loop);

        this.UpdateWindow();
        this.Settle(raise: false);
        this.stage.InvalidateMeasure();
    }

    IList<object> ReadItems()
    {
        if (this.ItemsSource is null)
            return [];

        var list = new List<object>();
        foreach (var item in this.ItemsSource)
            list.Add(item);
        return list;
    }

    void OnLoopChanged(bool wasLooping)
    {
        // An unwrapped looping position means nothing to a bounded list, so start again from the index.
        // The index it is heading to, not the one it is passing: switching mid-animation used to leave
        // it parked half way, between covers, while CurrentPosition still named the target.
        var index = CoverFlowGeometry.IndexAt(this.TargetPosition, this.items.Count, wasLooping);
        this.StopAnimation(snap: false);
        this.ReleaseAll(discard: false);
        this.position = index;
        this.centreVirtual = int.MinValue;
        this.UpdateWindow();
        this.Settle(raise: false);
    }

    void OnCurrentPositionChanged(int value)
    {
        if (this.syncing || this.items.Count == 0)
            return;

        if (CoverFlowGeometry.IndexAt(this.TargetPosition, this.items.Count, this.Loop) == CoverFlowGeometry.IndexAt(value, this.items.Count, this.Loop))
            return;

        this.GoTo(CoverFlowGeometry.IndexAt(value, this.items.Count, this.Loop), animate: this.CanAnimate);
    }

    void OnSizeChanged()
    {
        this.UpdateWindow();
        this.stage.InvalidateMeasure();
    }

    void OnReflectionOpacityChanged(double value)
    {
        foreach (var slot in this.realized.Values)
            slot.ReflectionOpacity = value;
    }


    // ---------------------------------------------------------------------------------------------
    // Realization
    // ---------------------------------------------------------------------------------------------

    Color Surface => this.surfaceProbe.Color ?? Colors.White;

    double ReflectionHeight => this.ShowReflection ? Math.Round(this.ItemHeight * 0.4) : 0;

    DataTemplate ResolveTemplate(object item)
    {
        if (this.ItemTemplate is DataTemplateSelector selector)
            return selector.SelectTemplate(item, this);

        return this.ItemTemplate ?? DefaultTemplate;
    }

    static readonly DataTemplate DefaultTemplate = new(() =>
    {
        var label = new Label { HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
        label.SetBinding(Label.TextProperty, static (object o) => o);
        return label;
    });

    static View Realize(DataTemplate template)
    {
        var content = template.CreateContent();
        return content as View
            ?? throw new InvalidOperationException("A CoverFlowView template must produce a View.");
    }

    /// <summary>Realizes the covers in reach of the centre and releases the rest.</summary>
    void UpdateWindow()
    {
        var count = this.items.Count;
        if (count == 0)
        {
            this.ReleaseAll(discard: false);
            this.UpdateCaption();
            return;
        }

        // One past the visible reach so the cover entering the stack fades in instead of popping.
        var reach = CoverFlowGeometry.Reach(this.VisibleItems, count, this.Loop) + 1;
        var centre = (int)Math.Round(this.position, MidpointRounding.AwayFromZero);
        var first = centre - reach;
        var last = centre + reach;
        if (this.Loop)
        {
            // Never realize the same item twice: with a short list the window is capped at the count.
            var span = Math.Min(last - first + 1, count);
            first = centre - ((span - 1) / 2);
            last = first + span - 1;
        }
        else
        {
            first = Math.Max(first, 0);
            last = Math.Min(last, count - 1);
        }

        foreach (var key in this.realized.Keys.Where(k => k < first || k > last).ToList())
        {
            this.Release(this.realized[key]);
            this.realized.Remove(key);
        }

        for (var k = first; k <= last; k++)
        {
            if (!this.realized.ContainsKey(k))
                this.realized[k] = this.Acquire(k);
        }

        this.ApplyTransforms();
    }

    CoverFlowSlot Acquire(int virtualIndex)
    {
        var item = this.items[CoverFlowGeometry.Mod(virtualIndex, this.items.Count)];
        var template = this.ResolveTemplate(item);

        var slot = this.pool.FirstOrDefault(s => s.Template == template);
        if (slot is not null)
        {
            this.pool.Remove(slot);
        }
        else
        {
            var reflection = this.ShowReflection ? Realize(template) : null;
            slot = new CoverFlowSlot(Realize(template), reflection, this.Surface) { Template = template };
        }

        slot.VirtualIndex = virtualIndex;
        slot.ItemWidth = this.ItemWidth;
        slot.ItemHeight = this.ItemHeight;
        slot.ReflectionHeight = this.ReflectionHeight;
        slot.ReflectionOpacity = this.ReflectionOpacity;
        slot.ZIndex = -MaximumZ;
        slot.Opacity = 0;
        slot.SetBindingContextFor(item);
        this.NameSlot(slot);
        this.stage.Children.Add(slot);
        return slot;
    }

    void Release(CoverFlowSlot slot)
    {
        this.stage.Children.Remove(slot);
        slot.SetBindingContextFor(null);
        AutomationIds.Set(slot, null);
        this.pool.Add(slot);
    }

    // By the item's index rather than the virtual one, so an infinite loop's covers keep the names a
    // test knows from the data.
    void NameSlot(CoverFlowSlot slot)
        => AutomationIds.Set(slot, this.items.Count == 0
            ? null
            : AutomationIds.For(this, ItemAutomationIdSuffix, CoverFlowGeometry.Mod(slot.VirtualIndex, this.items.Count)));

    void ReleaseAll(bool discard)
    {
        foreach (var slot in this.realized.Values)
            this.Release(slot);

        this.realized.Clear();
        if (discard)
            this.pool.Clear();
    }


    // ---------------------------------------------------------------------------------------------
    // Layout and transforms
    // ---------------------------------------------------------------------------------------------

    internal Size MeasureStage(double widthConstraint, double heightConstraint)
    {
        this.blockHeight = this.ItemHeight + this.ReflectionHeight;

        foreach (var slot in this.realized.Values)
        {
            slot.ItemWidth = this.ItemWidth;
            slot.ItemHeight = this.ItemHeight;
            slot.ReflectionHeight = this.ReflectionHeight;
            slot.Measure(this.ItemWidth, this.blockHeight);
        }

        var reach = Math.Max(1, this.VisibleItems);
        var naturalWidth = this.ItemWidth + (2 * (this.CenterSpacing + ((reach - 1) * this.ItemSpacing)));
        var width = double.IsFinite(widthConstraint) ? widthConstraint : naturalWidth;

        // Headroom for perspective: a side cover's outer edge comes toward the viewer and grows.
        var height = this.blockHeight * 1.15;
        if (double.IsFinite(heightConstraint))
            height = Math.Min(height, heightConstraint);

        return new Size(width, height);
    }

    internal Size ArrangeStage(Rect bounds)
    {
        this.blockHeight = this.ItemHeight + this.ReflectionHeight;
        var top = Math.Max(0, (bounds.Height - this.blockHeight) / 2);
        this.stageCentre = new Point(bounds.Width / 2, top + (this.ItemHeight / 2));

        var box = new Rect((bounds.Width - this.ItemWidth) / 2, top, this.ItemWidth, this.blockHeight);
        var anchorY = this.blockHeight > 0 ? (this.ItemHeight / 2) / this.blockHeight : 0.5;

        foreach (var slot in this.realized.Values)
        {
            // Scale and turn about the cover's own centre, not the centre of cover-plus-reflection.
            slot.AnchorY = anchorY;
            slot.Arrange(box);
        }

        return bounds.Size;
    }

    void ApplyTransforms()
    {
        if (this.items.Count == 0)
            return;

        var layout = this.CoverLayout;
        var centre = (int)Math.Round(this.position, MidpointRounding.AwayFromZero);
        var centreMoved = centre != this.centreVirtual;
        this.centreVirtual = centre;

        foreach (var slot in this.realized.Values)
        {
            var t = CoverFlowGeometry.For(slot.VirtualIndex - this.position, layout);
            slot.TranslationX = t.TranslationX;
            slot.RotationY = t.RotationY;
            slot.Scale = t.Scale;
            slot.Opacity = t.Opacity;

            // Left of centre the nearer cover has the larger index, right of it the smaller, so a
            // cover's stacking order only changes as it passes the centre. Re-assigning ZIndex removes
            // and re-adds the native view on some platforms, so it is written only when it changes.
            var z = slot.VirtualIndex == centre
                ? MaximumZ
                : slot.VirtualIndex < centre ? slot.VirtualIndex - centre : centre - slot.VirtualIndex;
            if (slot.ZIndex != z)
                slot.ZIndex = z;
        }

        if (centreMoved)
            this.UpdateCaption();
    }

    void SetPosition(double value)
    {
        this.position = value;
        var centre = (int)Math.Round(value, MidpointRounding.AwayFromZero);
        if (centre != this.centreVirtual)
            this.UpdateWindow();
        else
            this.ApplyTransforms();
    }


    // ---------------------------------------------------------------------------------------------
    // Caption
    // ---------------------------------------------------------------------------------------------

    object? CentreItem => this.items.Count == 0
        ? null
        : this.items[CoverFlowGeometry.IndexAt(this.position, this.items.Count, this.Loop)];

    void RebuildCaption()
    {
        this.captionHost.Content = null;
        this.UpdateCaption();
    }

    void UpdateCaption()
    {
        var item = this.CentreItem;
        var template = this.CaptionTemplate;
        if (template is null || item is null)
        {
            this.captionHost.IsVisible = false;
            return;
        }

        if (template is DataTemplateSelector selector)
            template = selector.SelectTemplate(item, this);

        if (this.captionHost.Content is null || this.captionHost.Content.GetValue(CaptionTemplateKey) != template)
        {
            var view = Realize(template);
            view.SetValue(CaptionTemplateKey, template);
            this.captionHost.Content = view;
        }

        this.captionHost.Content.BindingContext = item;
        this.captionHost.IsVisible = true;
    }

    /// <summary>Remembers which template the caption view came from, so a selector can swap it.</summary>
    static readonly BindableProperty CaptionTemplateKey = BindableProperty.CreateAttached(
        "CoverFlowCaptionTemplate", typeof(DataTemplate), typeof(CoverFlowView), null);


    // ---------------------------------------------------------------------------------------------
    // Gestures
    // ---------------------------------------------------------------------------------------------

    void OnPan(PanUpdatedEventArgs e) => this.OnPan(e.StatusType, e.TotalX);

    void OnPan(GestureStatus status, double totalX)
    {
        if (!this.IsSwipeEnabled || this.items.Count == 0 || this.CenterSpacing <= 0)
            return;

        switch (status)
        {
            case GestureStatus.Started:
                this.StopAnimation(snap: false);
                this.dragStart = this.position;
                this.samples.Clear();
                this.Sample(this.position);
                break;

            case GestureStatus.Running:
                var raw = this.dragStart - (totalX / this.CenterSpacing);
                this.Sample(raw);
                this.SetPosition(CoverFlowGeometry.Resist(raw, this.items.Count, this.Loop));
                break;

            case GestureStatus.Completed:
            case GestureStatus.Canceled:
                var target = CoverFlowGeometry.SettleTarget(this.position, this.Velocity(), this.items.Count, this.Loop);
                this.samples.Clear();
                this.MoveTo(target, animate: true);
                break;
        }
    }

    void Sample(double value)
    {
        this.samples.Add((this.clock.ElapsedTicks, value));
        if (this.samples.Count > 8)
            this.samples.RemoveAt(0);
    }

    /// <summary>Items per second over the last tenth of a second of the drag.</summary>
    double Velocity()
    {
        if (this.samples.Count < 2)
            return 0;

        var (lastTicks, lastValue) = this.samples[^1];
        var window = Stopwatch.Frequency / 10;
        var first = this.samples.FirstOrDefault(s => lastTicks - s.Ticks <= window);
        var seconds = (lastTicks - first.Ticks) / (double)Stopwatch.Frequency;

        return seconds <= 0 ? 0 : (lastValue - first.Position) / seconds;
    }

    /// <summary>
    /// Brings the cover under <paramref name="point"/> (stage coordinates) to the centre, or selects it
    /// when it is already there. Covers are tested nearest-first, so the one drawn on top wins.
    /// </summary>
    internal void HandleTap(Point point)
    {
        if (this.items.Count == 0)
            return;

        var layout = this.CoverLayout;

        // While a move is settling, the centre slot belongs to the cover arriving in it: a tap there
        // finishes the move and selects that cover, rather than re-targeting to whichever cover happens
        // to be passing the middle (which is what a tap straight after Next or a swipe used to do).
        if (this.ticker is not null
            && CoverFlowGeometry.Footprint(CoverFlowGeometry.For(0, layout), this.stageCentre, this.ItemWidth, this.ItemHeight).Contains(point))
        {
            this.SelectCentre();
            return;
        }

        var hit = this.realized.Values
            .Select(slot => (Slot: slot, Transform: CoverFlowGeometry.For(slot.VirtualIndex - this.position, layout)))
            .Where(x => x.Transform.Opacity > 0.05)
            .OrderBy(x => x.Transform.Depth)
            .FirstOrDefault(x => CoverFlowGeometry.Footprint(x.Transform, this.stageCentre, this.ItemWidth, this.ItemHeight).Contains(point));

        if (hit.Slot is null)
            return;

        if (hit.Slot.VirtualIndex == (int)Math.Round(this.TargetPosition, MidpointRounding.AwayFromZero))
            this.SelectCentre();
        else
            this.MoveTo(hit.Slot.VirtualIndex, animate: true);
    }

    /// <summary>
    /// Selects the centre cover - the one a running animation is heading to, which it finishes first.
    /// Also what a tap with no position does (an accessibility activation, UI automation).
    /// </summary>
    internal void SelectCentre()
    {
        if (this.items.Count == 0)
            return;

        this.StopAnimation(snap: true);
        var index = CoverFlowGeometry.IndexAt(this.position, this.items.Count, this.Loop);
        var item = this.items[index];
        this.ItemSelected?.Invoke(this, new CollectionItemEventArgs(item, index));
        if (this.ItemSelectedCommand?.CanExecute(item) == true)
            this.ItemSelectedCommand.Execute(item);
    }

    /// <summary>The point a test should tap to hit the cover at <paramref name="virtualIndex"/>.</summary>
    internal Point CentreOf(int virtualIndex)
    {
        var t = CoverFlowGeometry.For(virtualIndex - this.position, this.CoverLayout);
        return new Point(this.stageCentre.X + t.TranslationX, this.stageCentre.Y);
    }


    // ---------------------------------------------------------------------------------------------
    // Animation
    // ---------------------------------------------------------------------------------------------

    void MoveTo(double target, bool animate)
    {
        var ticker = animate && this.AnimationDuration > 0 && this.CanAnimate
            ? MotionTicker.For(this)
            : null;

        if (ticker is null || Math.Abs(target - this.position) < 1e-6)
        {
            this.StopAnimation(snap: false);
            this.SetPosition(target);
            this.Settle(raise: true);
            return;
        }

        this.animationFrom = this.position;
        this.animationTo = target;
        this.animationElapsed = 0;

        // A longer trip takes a little longer, but never so long that skipping ten covers drags.
        var distance = Math.Abs(target - this.position);
        this.animationLength = this.AnimationDuration * Math.Min(2, Math.Max(1, Math.Sqrt(distance)));

        if (this.ticker is null)
        {
            this.ticker = ticker;
            this.ticker.Tick += this.OnTick;
        }
    }

    void OnTick(TimeSpan delta)
    {
        this.animationElapsed += delta.TotalMilliseconds;
        var t = this.animationLength <= 0 ? 1 : this.animationElapsed / this.animationLength;

        if (t >= 1)
        {
            var target = this.animationTo;
            this.StopAnimation(snap: false);
            this.SetPosition(target);
            this.Settle(raise: true);
            return;
        }

        this.SetPosition(this.animationFrom + ((this.animationTo - this.animationFrom) * CoverFlowGeometry.EaseOut(t)));
    }

    void StopAnimation(bool snap)
    {
        if (this.ticker is null)
            return;

        this.ticker.Tick -= this.OnTick;
        this.ticker = null;

        if (snap)
        {
            this.SetPosition(this.animationTo);
            this.Settle(raise: true);
        }
    }

    /// <summary>Publishes the item that has come to rest in the centre, once per change.</summary>
    void Settle(bool raise)
    {
        if (this.items.Count == 0)
        {
            this.reportedIndex = -1;
            return;
        }

        var index = CoverFlowGeometry.IndexAt(this.position, this.items.Count, this.Loop);

        // CurrentPosition is brought back into line even when the index did not change: set it to 3,
        // tap back to 0 before the move arrives, and the control rests on 0 while CurrentPosition still
        // said 3 - the event is unchanged (nothing moved), but the binding must not lie.
        if (this.CurrentPosition != index)
        {
            this.syncing = true;
            try
            {
                this.CurrentPosition = index;
            }
            finally
            {
                this.syncing = false;
            }
        }

        if (index == this.reportedIndex)
            return;

        var first = this.reportedIndex < 0;
        this.reportedIndex = index;

        if (!raise || first)
            return;

        this.PositionChanged?.Invoke(this, index);
        if (this.PositionChangedCommand?.CanExecute(index) == true)
            this.PositionChangedCommand.Execute(index);
    }
}
