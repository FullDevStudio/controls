using System.Collections;
using System.Windows.Input;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls;

public class AutoCompleteEntry : ContentView
{
    const int DefaultDebounceMs = 500;
    const double DefaultMaxDropDownHeight = 200;

    readonly BorderlessEntry entry;
    readonly ActivityIndicator spinner;
    readonly VerticalStackLayout suggestionStack;
    readonly ScrollView dropDownScroll;
    readonly Border dropDownBorder;
    readonly Microsoft.Maui.Controls.Shapes.RoundRectangle dropDownShape;
    readonly Grid rootGrid;

    CancellationTokenSource? debounceCts;
    bool suppressTextChanged;
    IList? currentFilteredItems;

    // Where the dropdown is floating while open, and the scrollers it is tracking.
    PageOverlay.AutoCompleteLayer? dropDownLayer;
    PageOverlay.ShinyOverlayRoot? dropDownRoot;
    readonly List<ScrollView> trackedScrolls = [];

    /// <remarks>
    /// An implicit Style targeting this type is applied by MAUI *before this constructor body
    /// runs at all* - StyleableElement's own constructor creates a MergedStyle, whose ctor
    /// calls RegisterImplicitStyles(), which resolves the style out of
    /// Application.Current.Resources and applies it there and then:
    ///
    ///     at Microsoft.Maui.Controls.Setter.Apply(...)
    ///     at Microsoft.Maui.Controls.MergedStyle.set_ImplicitStyle(...)
    ///     at Microsoft.Maui.Controls.MergedStyle.RegisterImplicitStyles()
    ///     at Microsoft.Maui.Controls.MergedStyle..ctor(...)
    ///     at Microsoft.Maui.Controls.StyleableElement..ctor()
    ///
    /// So the setters run the propertyChanged callbacks below while every field here is still
    /// null, and the page dies on inflation with a NullReferenceException. Reordering this
    /// constructor cannot help - nothing in it has run yet. See
    /// <see cref="Infrastructure.StyleGuard"/>: the callbacks below queue through it, and the
    /// MarkReady call at the end of this constructor replays them.
    /// </remarks>
    public AutoCompleteEntry()
    {
        entry = new BorderlessEntry
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        }.WithFontSize(ShinyThemeKeys.Type.BodyMediumSize);
        entry.TextChanged += OnEntryTextChanged;
        entry.Focused += (_, _) => OnEntryFocused();
        entry.Unfocused += (_, _) =>
        {
            // delay to allow tap on suggestion to register
            Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(200), HideDropDown);
        };

        spinner = new ActivityIndicator
        {
            IsRunning = false,
            IsVisible = false,
            WidthRequest = 20,
            HeightRequest = 20,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };

        var entryGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            VerticalOptions = LayoutOptions.Center
        };
        entryGrid.Add(entry, 0, 0);
        entryGrid.Add(spinner, 1, 0);

        suggestionStack = new VerticalStackLayout
        {
            Spacing = 0
        };

        dropDownScroll = new ScrollView
        {
            Content = suggestionStack,
            VerticalScrollBarVisibility = ScrollBarVisibility.Default
        };

        dropDownShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle()
            .WithCornerRadius(ShinyThemeKeys.Shape.CornerExtraSmallRadius);

        dropDownBorder = new Border
        {
            IsVisible = false,
            Padding = 0,
            StrokeShape = dropDownShape,
            MaximumHeightRequest = DefaultMaxDropDownHeight,
            Content = dropDownScroll
        }
        .WithStrokeThickness(ShinyThemeKeys.Border.Thin)
        .WithElevation(ShinyThemeKeys.Elevation.Level2);

        // The suggestion list is NOT a row of this grid. Laid out inline it was a ScrollView inside
        // whatever ScrollView the form sits in (two scrollers on one axis) and it shoved the rest of
        // the form down every time it opened. It floats in the page's overlay layer instead, placed
        // under the field - see ShowDropDown. The second row is only the fallback for a control that
        // is not on a ContentPage at all, where there is no overlay to float in.
        rootGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        rootGrid.Add(entryGrid, 0, 0);

        Content = rootGrid;

        // Install the page's overlay root up front. Doing it the first time the dropdown opens would
        // re-parent the page content while the entry has focus - which rebuilds every native view,
        // drops the keyboard, and so closes the dropdown it was opening.
        Loaded += (_, _) => Dispatcher.Dispatch(() =>
        {
            if (IsLoaded)
                PageOverlay.GetOrCreateRoot(this);
        });
        Unloaded += (_, _) => HideDropDown();
        SizeChanged += (_, _) => PlaceDropDown();

        // Theme defaults for anything the consumer left unset.
        ApplySpinnerColor();
        ApplyDropDownBackground();
        ApplyDropDownStroke();

        // Last line: replays any callback that fired before the children existed.
        StyleGuard.MarkReady(this, typeof(AutoCompleteEntry));
    }




    // Each of these takes the explicit colour when one is set, and otherwise falls back to the
    // theme resource. Shared by the constructor and the propertyChanged callbacks so the
    // "unset means theme default" behaviour is defined in exactly one place.

    void ApplySpinnerColor()
    {
        if (this.SpinnerColor is Color c)
            spinner.Color = c;
        else
            spinner.SetDynamicResource(ActivityIndicator.ColorProperty, ShinyThemeKeys.Color.OnSurfaceVariant);
    }

    void ApplyDropDownBackground()
    {
        if (this.DropDownBackgroundColor is Color c)
            dropDownBorder.BackgroundColor = c;
        else
            dropDownBorder.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.Surface);
    }

    void ApplyDropDownStroke()
    {
        if (this.DropDownBorderColor is Color c)
        {
            dropDownBorder.Stroke = c;
        }
        else
        {
            ThemeBrush.Apply(dropDownBorder, Border.StrokeProperty, ShinyThemeKeys.Brush.Outline);
        }
    }




    // --- Bindable Properties ---

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(AutoCompleteEntry),
        string.Empty,
        BindingMode.TwoWay,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl =>
        {
            if (ctrl.entry.Text != (string)n)
            {
                ctrl.suppressTextChanged = true;
                ctrl.entry.Text = (string)n;
                ctrl.suppressTextChanged = false;
            }
        }));
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder),
        typeof(string),
        typeof(AutoCompleteEntry),
        string.Empty,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl => ctrl.entry.Placeholder = (string)n));
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create(
        nameof(PlaceholderColor),
        typeof(Color),
        typeof(AutoCompleteEntry),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl => { if (n is Color c) ctrl.entry.PlaceholderColor = c; }));
    public Color? PlaceholderColor
    {
        get => (Color?)GetValue(PlaceholderColorProperty);
        set => SetValue(PlaceholderColorProperty, value);
    }

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IList),
        typeof(AutoCompleteEntry),
        null);
    public IList? ItemsSource
    {
        get => (IList?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly BindableProperty SearchCommandProperty = BindableProperty.Create(
        nameof(SearchCommand),
        typeof(ICommand),
        typeof(AutoCompleteEntry),
        null);
    public ICommand? SearchCommand
    {
        get => (ICommand?)GetValue(SearchCommandProperty);
        set => SetValue(SearchCommandProperty, value);
    }

    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(
        nameof(SelectedItem),
        typeof(object),
        typeof(AutoCompleteEntry),
        null,
        BindingMode.TwoWay);
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public static readonly BindableProperty IsBusyProperty = BindableProperty.Create(
        nameof(IsBusy),
        typeof(bool),
        typeof(AutoCompleteEntry),
        false,
        BindingMode.TwoWay,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl =>
        {
            var busy = (bool)n;
            ctrl.spinner.IsRunning = busy;
            ctrl.spinner.IsVisible = busy;
        }));
    public bool IsBusy
    {
        get => (bool)GetValue(IsBusyProperty);
        set => SetValue(IsBusyProperty, value);
    }

    public static readonly BindableProperty ItemTemplateProperty = BindableProperty.Create(
        nameof(ItemTemplate),
        typeof(DataTemplate),
        typeof(AutoCompleteEntry),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl =>
        {
            // Re-render if items are already showing
            if (ctrl.currentFilteredItems != null)
                ctrl.RenderDropDownItems(ctrl.currentFilteredItems);
        }));
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public static readonly BindableProperty TextMemberPathProperty = BindableProperty.Create(
        nameof(TextMemberPath),
        typeof(string),
        typeof(AutoCompleteEntry),
        null);
    public string? TextMemberPath
    {
        get => (string?)GetValue(TextMemberPathProperty);
        set => SetValue(TextMemberPathProperty, value);
    }

    public static readonly BindableProperty DebounceIntervalProperty = BindableProperty.Create(
        nameof(DebounceInterval),
        typeof(int),
        typeof(AutoCompleteEntry),
        DefaultDebounceMs);
    public int DebounceInterval
    {
        get => (int)GetValue(DebounceIntervalProperty);
        set => SetValue(DebounceIntervalProperty, value);
    }

    public static readonly BindableProperty ThresholdProperty = BindableProperty.Create(
        nameof(Threshold),
        typeof(int),
        typeof(AutoCompleteEntry),
        1);
    public int Threshold
    {
        get => (int)GetValue(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    public static readonly BindableProperty MaxDropDownHeightProperty = BindableProperty.Create(
        nameof(MaxDropDownHeight),
        typeof(double),
        typeof(AutoCompleteEntry),
        DefaultMaxDropDownHeight,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl => ctrl.dropDownBorder.MaximumHeightRequest = (double)n));
    public double MaxDropDownHeight
    {
        get => (double)GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(AutoCompleteEntry),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl => { if (n is Color c) ctrl.entry.TextColor = c; }));
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public static readonly BindableProperty DropDownBackgroundColorProperty = BindableProperty.Create(
        nameof(DropDownBackgroundColor),
        typeof(Color),
        typeof(AutoCompleteEntry),
        null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl => ctrl.ApplyDropDownBackground()));
    public Color? DropDownBackgroundColor
    {
        get => (Color?)GetValue(DropDownBackgroundColorProperty);
        set => SetValue(DropDownBackgroundColorProperty, value);
    }

    public static readonly BindableProperty DropDownBorderColorProperty = BindableProperty.Create(
        nameof(DropDownBorderColor),
        typeof(Color),
        typeof(AutoCompleteEntry),
        null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl => ctrl.ApplyDropDownStroke()));
    public Color? DropDownBorderColor
    {
        get => (Color?)GetValue(DropDownBorderColorProperty);
        set => SetValue(DropDownBorderColorProperty, value);
    }

    public static readonly BindableProperty SpinnerColorProperty = BindableProperty.Create(
        nameof(SpinnerColor),
        typeof(Color),
        typeof(AutoCompleteEntry),
        null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl => ctrl.ApplySpinnerColor()));
    public Color? SpinnerColor
    {
        get => (Color?)GetValue(SpinnerColorProperty);
        set => SetValue(SpinnerColorProperty, value);
    }

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(AutoCompleteEntry),
        ThemeTokens.Unset,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<AutoCompleteEntry>(
            b,
            ctrl => ctrl.entry.SetTokenOrValue(Entry.FontSizeProperty, (double)n, ShinyThemeKeys.Type.BodyMediumSize)));
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily),
        typeof(string),
        typeof(AutoCompleteEntry),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl => ctrl.entry.FontFamily = n as string));
    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(
        nameof(FontAttributes),
        typeof(FontAttributes),
        typeof(AutoCompleteEntry),
        Microsoft.Maui.Controls.FontAttributes.None,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<AutoCompleteEntry>(b, ctrl => ctrl.entry.FontAttributes = (FontAttributes)n));
    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    public static readonly BindableProperty ShowAllOnFocusProperty = BindableProperty.Create(
        nameof(ShowAllOnFocus),
        typeof(bool),
        typeof(AutoCompleteEntry),
        false);
    public bool ShowAllOnFocus
    {
        get => (bool)GetValue(ShowAllOnFocusProperty);
        set => SetValue(ShowAllOnFocusProperty, value);
    }

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius),
        typeof(double),
        typeof(AutoCompleteEntry),
        ThemeTokens.Unset,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady<AutoCompleteEntry>(
            b,
            ctrl => ctrl.dropDownShape.SetCornerTokenOrValue((double)n, ShinyThemeKeys.Shape.CornerExtraSmallRadius)));
    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }


    // --- Events ---

    public event EventHandler<object?>? ItemSelected;


    // --- Private Methods ---

    void OnEntryFocused()
    {
        if (ShowAllOnFocus && SearchCommand == null)
        {
            var searchText = Text ?? string.Empty;
            if (searchText.Length < Threshold)
            {
                var source = ItemsSource;
                if (source != null && source.Count > 0)
                {
                    var all = new List<object>();
                    foreach (var item in source)
                        all.Add(item);
                    RenderDropDownItems(all);
                    ShowDropDown();
                }
                return;
            }
        }

        ShowDropDown();
    }

    void OnEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (suppressTextChanged)
            return;

        Text = e.NewTextValue;
        SelectedItem = null;

        debounceCts?.Cancel();

        var searchText = e.NewTextValue ?? string.Empty;
        if (searchText.Length < Threshold)
        {
            if (ShowAllOnFocus && entry.IsFocused && SearchCommand == null)
            {
                var source = ItemsSource;
                if (source != null && source.Count > 0)
                {
                    var all = new List<object>();
                    foreach (var item in source)
                        all.Add(item);
                    RenderDropDownItems(all);
                    ShowDropDown();
                    return;
                }
            }
            HideDropDown();
            return;
        }

        debounceCts = new CancellationTokenSource();
        var token = debounceCts.Token;
        var debounce = DebounceInterval;

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(debounce), () =>
        {
            if (token.IsCancellationRequested)
                return;

            PerformSearch(searchText);
        });
    }

    void PerformSearch(string searchText)
    {
        if (SearchCommand != null)
        {
            if (SearchCommand.CanExecute(searchText))
                SearchCommand.Execute(searchText);

            ShowDropDown();
        }
        else
        {
            FilterLocalItems(searchText);
        }
    }

    void FilterLocalItems(string searchText)
    {
        var source = ItemsSource;
        if (source == null || source.Count == 0)
        {
            HideDropDown();
            return;
        }

        var filtered = new List<object>();
        var comparison = StringComparison.OrdinalIgnoreCase;

        foreach (var item in source)
        {
            var text = GetDisplayText(item);
            if (text.Contains(searchText, comparison))
                filtered.Add(item);
        }

        if (filtered.Count > 0)
        {
            RenderDropDownItems(filtered);
            ShowDropDown();
        }
        else
        {
            HideDropDown();
        }
    }

    void RenderDropDownItems(IList items)
    {
        currentFilteredItems = items;
        suggestionStack.Children.Clear();

        foreach (var item in items)
        {
            View itemView;

            if (ItemTemplate != null)
            {
                var content = ItemTemplate.CreateContent();
                if (content is View v)
                {
                    v.BindingContext = item;
                    itemView = v;
                }
                else if (content is ViewCell cell)
                {
                    cell.BindingContext = item;
                    itemView = cell.View;
                }
                else
                {
                    itemView = CreateDefaultItemView(item);
                }
            }
            else
            {
                itemView = CreateDefaultItemView(item);
            }

            var tapGesture = new TapGestureRecognizer();
            var capturedItem = item;
            tapGesture.Tapped += (_, _) => OnItemTapped(capturedItem);
            itemView.GestureRecognizers.Add(tapGesture);

            suggestionStack.Children.Add(itemView);
        }
    }

    View CreateDefaultItemView(object item)
    {
        var label = new Label
        {
            Text = GetDisplayText(item),
            Padding = new Thickness(10, 8),
            VerticalTextAlignment = TextAlignment.Center
        }.WithFontSize(ShinyThemeKeys.Type.BodyMediumSize);
        return label;
    }

    string GetDisplayText(object item)
    {
        if (item is string s)
            return s;

        if (!string.IsNullOrEmpty(TextMemberPath))
        {
            if (MemberPath.TryGetValue(item, TextMemberPath, out var value))
                return value?.ToString() ?? string.Empty;
        }

        return item.ToString() ?? string.Empty;
    }

    void OnItemTapped(object item)
    {
        SelectedItem = item;

        suppressTextChanged = true;
        var displayText = GetDisplayText(item);
        entry.Text = displayText;
        Text = displayText;
        suppressTextChanged = false;

        HideDropDown();
        ItemSelected?.Invoke(this, item);
    }

    /// <summary>Whether the suggestion list is open. Test seam.</summary>
    internal bool IsDropDownOpen { get; private set; }

    /// <summary>The suggestion list's card. Test seam.</summary>
    internal View DropDown => dropDownBorder;

    /// <summary>Runs a search as if the debounce had just elapsed on <paramref name="text"/>. Test seam.</summary>
    internal void SearchNow(string text) => PerformSearch(text);

    void ShowDropDown()
    {
        if (currentFilteredItems == null || currentFilteredItems.Count == 0)
            return;

        IsDropDownOpen = true;

        var root = PageOverlay.GetOrCreateRoot(this);
        if (root is null)
        {
            // Not on a ContentPage: nothing to float over, so fall back to the inline row.
            Reparent(rootGrid, inline: true);
            dropDownBorder.IsVisible = true;
            return;
        }

        var layer = PageOverlay.GetOrCreateLayer<PageOverlay.AutoCompleteLayer>(root, PageOverlay.Layers.AutoComplete);
        Reparent(layer, inline: false);
        dropDownLayer = layer;

        if (!ReferenceEquals(dropDownRoot, root))
        {
            if (dropDownRoot is not null)
                dropDownRoot.SizeChanged -= OnOverlaySizeChanged;
            dropDownRoot = root;
            root.SizeChanged += OnOverlaySizeChanged;
        }

        // The field moves when any scroller above it does; follow it rather than hang in mid-air.
        UntrackScrolls();
        for (var parent = Parent; parent is not null && !ReferenceEquals(parent, root); parent = parent.Parent)
        {
            if (parent is ScrollView scroll)
            {
                scroll.Scrolled += OnAncestorScrolled;
                trackedScrolls.Add(scroll);
            }
        }

        // The automation id follows the field's, so a UI test can still find the suggestions now that
        // they are not underneath it. AutomationId is set-once in MAUI, hence the guard.
        if (dropDownBorder.AutomationId is null && AutomationId is { Length: > 0 } id)
            dropDownBorder.AutomationId = id + "_DropDown";

        PlaceDropDown();
    }

    void HideDropDown()
    {
        IsDropDownOpen = false;
        dropDownBorder.IsVisible = false;
        UntrackScrolls();

        if (dropDownRoot is not null)
        {
            dropDownRoot.SizeChanged -= OnOverlaySizeChanged;
            dropDownRoot = null;
        }

        // Out of the layer entirely, so a closed dropdown is not left in another page's tree.
        if (dropDownBorder.Parent is Layout layout && !ReferenceEquals(layout, rootGrid))
            layout.Remove(dropDownBorder);
        dropDownLayer = null;
    }

    void Reparent(Layout target, bool inline)
    {
        if (ReferenceEquals(dropDownBorder.Parent, target))
            return;

        if (dropDownBorder.Parent is Layout current)
            current.Remove(dropDownBorder);

        if (inline)
        {
            dropDownBorder.Margin = 0;
            dropDownBorder.WidthRequest = -1;
            dropDownBorder.MaximumHeightRequest = MaxDropDownHeight;
            dropDownBorder.HorizontalOptions = LayoutOptions.Fill;
            dropDownBorder.VerticalOptions = LayoutOptions.Start;
            rootGrid.Add(dropDownBorder, 0, 1);
        }
        else
        {
            dropDownBorder.HorizontalOptions = LayoutOptions.Start;
            dropDownBorder.VerticalOptions = LayoutOptions.Start;
            target.Add(dropDownBorder);
        }
    }

    void UntrackScrolls()
    {
        foreach (var scroll in trackedScrolls)
            scroll.Scrolled -= OnAncestorScrolled;
        trackedScrolls.Clear();
    }

    void OnAncestorScrolled(object? sender, ScrolledEventArgs e) => PlaceDropDown();

    void OnOverlaySizeChanged(object? sender, EventArgs e) => PlaceDropDown();

    /// <summary>
    /// Puts the floating list directly under the field at the field's width, or above it when there is
    /// more room there - the bottom of a form, or a field low on a short screen.
    /// </summary>
    void PlaceDropDown()
    {
        if (!IsDropDownOpen || dropDownRoot is not { } root || dropDownLayer is null)
            return;

        if (ViewGeometry.BoundsIn(this, root) is not { } field)
        {
            // Not laid out yet (the next SizeChanged comes back here) or scrolled out of the tree.
            dropDownBorder.IsVisible = false;
            return;
        }

        const double gap = 2;
        const double edge = 8;

        dropDownBorder.WidthRequest = field.Width;

        var desired = ((IView)dropDownBorder).Measure(field.Width, double.PositiveInfinity).Height
            - dropDownBorder.Margin.VerticalThickness;
        var height = Math.Min(MaxDropDownHeight, double.IsFinite(desired) && desired > 0 ? desired : MaxDropDownHeight);

        var below = root.Height > 0 ? root.Height - field.Bottom - gap - edge : double.PositiveInfinity;
        var above = field.Top - gap - edge;
        var flip = below < height && above > below;

        // Never taller than the side it opens on, so a flipped list cannot cover its own field.
        var room = Math.Max(0, flip ? above : below);
        var shown = Math.Min(height, room);
        dropDownBorder.MaximumHeightRequest = Math.Min(MaxDropDownHeight, room);

        var y = flip ? field.Top - gap - shown : field.Bottom + gap;
        dropDownBorder.Margin = new Thickness(field.X, y, 0, 0);
        dropDownBorder.IsVisible = true;
    }

    protected override void OnPropertyChanged(string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);

        // When ItemsSource changes externally (e.g. from SearchCommand callback), update dropdown
        if (propertyName == nameof(ItemsSource))
        {
            if (ItemsSource != null)
                RenderDropDownItems(ItemsSource);

            if (entry.IsFocused && !string.IsNullOrEmpty(Text) && Text.Length >= Threshold)
                ShowDropDown();
        }
    }
}
