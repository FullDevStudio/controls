using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.Scheduler.Internal;

class CalendarDayCell : ContentView
{
    readonly Label dateLabel;
    readonly VerticalStackLayout eventsStack;
    readonly Grid root;
    readonly List<(View View, SchedulerEvent Event)> eventViews = [];
    View? overflowView;

    DateOnly date;
    IReadOnlyList<SchedulerEvent> events = [];
    bool isSelected;
    bool isCurrentMonth;
    bool isToday;
    int maxEvents = 3;
    bool showCountOnly;
    DataTemplate? eventTemplate;
    DataTemplate? overflowTemplate;
    // null => follow the active theme pack. A consumer-supplied colour wins.
    Color? cellColor;
    Color? selectedColor;
    Color? currentDayColor;

    public CalendarDayCell()
    {
        dateLabel = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            HeightRequest = 24,
            WidthRequest = 24
        }.WithFontSize(ShinyThemeKeys.Type.BodySmallSize);

        eventsStack = new VerticalStackLayout
        {
            Spacing = 1
        };

        root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(28)),
                new RowDefinition(GridLength.Star)
            },
            Padding = new Thickness(1),
            IsClippedToBounds = true
        };

        root.Add(dateLabel, 0, 0);
        root.Add(eventsStack, 0, 1);

        IsClippedToBounds = true;
        Content = root;

        // The owning view names this cell by date; its events follow the cell's id.
        AutomationIds.Track(this, NameParts);
    }

    void NameParts()
    {
        SchedulerAutomationIds.NameEvents(this, eventViews);
        if (overflowView is not null)
            AutomationIds.Set(overflowView, AutomationIds.For(this, SchedulerCalendarView.MoreAutomationIdSuffix));
    }

    public DateOnly Date
    {
        get => date;
        set { date = value; Refresh(); }
    }

    public IReadOnlyList<SchedulerEvent> Events
    {
        get => events;
        set { events = value; RefreshEvents(); }
    }

    public bool IsSelected
    {
        get => isSelected;
        set { isSelected = value; RefreshAppearance(); }
    }

    public bool IsCurrentMonth
    {
        get => isCurrentMonth;
        set { isCurrentMonth = value; RefreshAppearance(); }
    }

    public bool IsToday
    {
        get => isToday;
        set { isToday = value; RefreshAppearance(); }
    }

    public int MaxEvents
    {
        get => maxEvents;
        set { maxEvents = value; RefreshEvents(); }
    }

    public bool ShowCountOnly
    {
        get => showCountOnly;
        set { showCountOnly = value; RefreshEvents(); }
    }

    public DataTemplate? EventTemplate
    {
        get => eventTemplate;
        set { eventTemplate = value; RefreshEvents(); }
    }

    public DataTemplate? OverflowTemplate
    {
        get => overflowTemplate;
        set { overflowTemplate = value; RefreshEvents(); }
    }

    public Color? CellColor
    {
        get => cellColor;
        set { cellColor = value; RefreshAppearance(); }
    }

    public Color? SelectedColor
    {
        get => selectedColor;
        set { selectedColor = value; RefreshAppearance(); }
    }

    public Color? CurrentDayColor
    {
        get => currentDayColor;
        set { currentDayColor = value; RefreshAppearance(); }
    }

    public Action<SchedulerEvent>? EventTapped { get; set; }
    public Action<DateOnly>? DayTapped { get; set; }

    void Refresh()
    {
        dateLabel.Text = date.Day.ToString();
        RefreshAppearance();
    }

    void RefreshAppearance()
    {
        dateLabel.Opacity = isCurrentMonth ? 1.0 : 0.4;

        if (isToday)
        {
            Apply(dateLabel, Label.TextColorProperty, null, ShinyThemeKeys.Color.OnPrimary);
            Apply(dateLabel, VisualElement.BackgroundColorProperty, currentDayColor, ShinyThemeKeys.Color.Primary);
        }
        else
        {
            Apply(dateLabel, Label.TextColorProperty, null,
                isCurrentMonth ? ShinyThemeKeys.Color.OnSurface : ShinyThemeKeys.Color.OnSurfaceVariant);
            dateLabel.RemoveDynamicResource(VisualElement.BackgroundColorProperty);
            dateLabel.BackgroundColor = Colors.Transparent;
        }

        if (isSelected)
            Apply(this, VisualElement.BackgroundColorProperty, selectedColor, ShinyThemeKeys.Color.SecondaryContainer);
        else
            Apply(this, VisualElement.BackgroundColorProperty, cellColor, ShinyThemeKeys.Color.Surface);
    }

    /// <summary>Uses the explicit colour when one was supplied, otherwise binds to the theme token.</summary>
    static void Apply(Element target, BindableProperty property, Color? explicitColor, string themeKey)
    {
        if (explicitColor is null)
        {
            target.SetDynamicResource(property, themeKey);
        }
        else
        {
            target.RemoveDynamicResource(property);
            target.SetValue(property, explicitColor);
        }
    }

    void RefreshEvents()
    {
        eventsStack.Children.Clear();
        eventViews.Clear();
        overflowView = null;

        if (events.Count == 0)
            return;

        if (showCountOnly)
        {
            var countOnly = new Label
            {
                Text = events.Count.ToString(),
                FontSize = 10,
                HorizontalTextAlignment = TextAlignment.Center
            };
            countOnly.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurfaceVariant);
            eventsStack.Children.Add(countOnly);
            return;
        }

        var allDay = events.Where(e => e.IsAllDay).ToList();
        var timed = events.Where(e => !e.IsAllDay).OrderBy(e => e.Start).ToList();
        var sorted = allDay.Concat(timed).ToList();

        var toShow = sorted.Take(maxEvents).ToList();
        var overflow = sorted.Count - maxEvents;

        foreach (var evt in toShow)
        {
            View view;
            if (eventTemplate != null)
            {
                view = (View)eventTemplate.CreateContent();
                view.BindingContext = evt;
            }
            else
            {
                view = CreateDefaultEventView(evt);
            }

            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => EventTapped?.Invoke(evt);
            view.GestureRecognizers.Add(tap);
            eventsStack.Children.Add(view);
            eventViews.Add((view, evt));
        }

        if (overflow > 0)
        {
            var ctx = new CalendarOverflowContext { EventCount = sorted.Count - maxEvents, Date = date };
            View overflowView;
            if (overflowTemplate != null)
            {
                overflowView = (View)overflowTemplate.CreateContent();
                overflowView.BindingContext = ctx;
            }
            else
            {
                var overflowLabel = new Label
                {
                    Text = $"+{ctx.EventCount} more",
                    FontSize = 10,
                    Padding = new Thickness(2, 0)
                };
                overflowLabel.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurfaceVariant);
                overflowView = overflowLabel;
            }
            eventsStack.Children.Add(overflowView);
            this.overflowView = overflowView;
        }
        NameParts();
    }

    static View CreateDefaultEventView(SchedulerEvent evt)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(3)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 2,
            Padding = new Thickness(1)
        };

        var bar = new BoxView { CornerRadius = 1, WidthRequest = 3 };
        if (evt.Color is { } eventColor)
            bar.Color = eventColor;
        else
            bar.SetDynamicResource(BoxView.ColorProperty, ShinyThemeKeys.Color.Primary);
        grid.Add(bar, 0);

        grid.Add(new Label
        {
            Text = evt.Title,
            FontSize = 10,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1
        }, 1);

        return grid;
    }

    protected override void OnParentSet()
    {
        base.OnParentSet();
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => DayTapped?.Invoke(date);
        GestureRecognizers.Clear();
        GestureRecognizers.Add(tap);
    }
}