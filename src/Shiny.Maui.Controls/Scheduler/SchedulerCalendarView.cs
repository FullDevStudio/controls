using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Scheduler.Internal;

using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.Scheduler;

public class SchedulerCalendarView : ContentView
{
    /// <summary>
    /// Appended to the view's <see cref="Element.AutomationId"/>, then the date as <c>yyyyMMdd</c>, to name
    /// each day cell: <c>&lt;id&gt;_Day_20261019</c>. Keyed by date so it survives a month change.
    /// </summary>
    public const string DayAutomationIdSuffix = "_Day";

    /// <summary>
    /// Appended to a day's id, then <see cref="SchedulerEvent.Identifier"/>, to name each event shown in
    /// it: <c>&lt;id&gt;_Day_20261019_Event_&lt;identifier&gt;</c>. The agenda view uses the same suffix.
    /// </summary>
    public const string EventAutomationIdSuffix = "_Event";

    /// <summary>Appended to a day's id to name its "+N more" overflow.</summary>
    public const string MoreAutomationIdSuffix = "_More";

    /// <summary>Appended to the view's <see cref="Element.AutomationId"/> to name the previous-month button.</summary>
    public const string PreviousMonthAutomationIdSuffix = "_PreviousMonth";

    /// <summary>Appended to the view's <see cref="Element.AutomationId"/> to name the next-month button.</summary>
    public const string NextMonthAutomationIdSuffix = "_NextMonth";

    /// <summary>Appended to the view's <see cref="Element.AutomationId"/> to name the month title label.</summary>
    public const string MonthTitleAutomationIdSuffix = "_MonthTitle";

    readonly Grid rootGrid;
    readonly Grid headerGrid;
    readonly Label monthLabel;
    readonly Button prevButton;
    readonly Button nextButton;
    readonly Grid dayHeaderGrid;
    readonly Grid calendarGrid;
    readonly ContentView loaderOverlay;
    readonly CalendarDayCell[] cells = new CalendarDayCell[42];
    readonly SwipeGestureRecognizer swipeLeft;
    readonly SwipeGestureRecognizer swipeRight;
    PinchGestureRecognizer? pinchGesture;
    double currentScale = 1;
    double startScale = 1;

    CancellationTokenSource? loadCts;


    public static readonly BindableProperty ProviderProperty = BindableProperty.Create(
        nameof(Provider), typeof(ISchedulerEventProvider), typeof(SchedulerCalendarView),
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(SchedulerCalendarView), () =>
            {
                ((SchedulerCalendarView)b).OnProviderChanged();
            }));

    public static readonly BindableProperty SelectedDateProperty = BindableProperty.Create(
        nameof(SelectedDate), typeof(DateOnly), typeof(SchedulerCalendarView),
        defaultValueCreator: b => DateOnly.FromDateTime(ShinyTime.For((Element)b).Today()),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(SchedulerCalendarView), () =>
            {
                ((SchedulerCalendarView)b).OnSelectedDateChanged();
            }));

    public static readonly BindableProperty DisplayMonthProperty = BindableProperty.Create(
        nameof(DisplayMonth), typeof(DateOnly), typeof(SchedulerCalendarView),
        defaultValueCreator: b => DateOnly.FromDateTime(ShinyTime.For((Element)b).Today()),
        defaultBindingMode: BindingMode.TwoWay,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(SchedulerCalendarView), () =>
            {
                ((SchedulerCalendarView)b).OnDisplayMonthChanged();
            }));

    public static readonly BindableProperty ShowCalendarCellEventCountOnlyProperty = BindableProperty.Create(
        nameof(ShowCalendarCellEventCountOnly), typeof(bool), typeof(SchedulerCalendarView), false);

    public static readonly BindableProperty EventItemTemplateProperty = BindableProperty.Create(
        nameof(EventItemTemplate), typeof(DataTemplate), typeof(SchedulerCalendarView));

    public static readonly BindableProperty OverflowItemTemplateProperty = BindableProperty.Create(
        nameof(OverflowItemTemplate), typeof(DataTemplate), typeof(SchedulerCalendarView));

    public static readonly BindableProperty LoaderTemplateProperty = BindableProperty.Create(
        nameof(LoaderTemplate), typeof(DataTemplate), typeof(SchedulerCalendarView));

    public static readonly BindableProperty MaxEventsPerCellProperty = BindableProperty.Create(
        nameof(MaxEventsPerCell), typeof(int), typeof(SchedulerCalendarView), 3);

    public static readonly BindableProperty CalendarCellColorProperty = BindableProperty.Create(
        nameof(CalendarCellColor), typeof(Color), typeof(SchedulerCalendarView), null);

    public static readonly BindableProperty CalendarCellSelectedColorProperty = BindableProperty.Create(
        nameof(CalendarCellSelectedColor), typeof(Color), typeof(SchedulerCalendarView), null);

    public static readonly BindableProperty CurrentDayColorProperty = BindableProperty.Create(
        nameof(CurrentDayColor), typeof(Color), typeof(SchedulerCalendarView), null);

    public static readonly BindableProperty FirstDayOfWeekProperty = BindableProperty.Create(
        nameof(FirstDayOfWeek), typeof(DayOfWeek), typeof(SchedulerCalendarView), DayOfWeek.Sunday,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(SchedulerCalendarView), () =>
            {
                ((SchedulerCalendarView)b).RebuildCalendar();
            }));

    public static readonly BindableProperty MinDateProperty = BindableProperty.Create(
        nameof(MinDate), typeof(DateOnly?), typeof(SchedulerCalendarView),
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(SchedulerCalendarView), () =>
            {
                ((SchedulerCalendarView)b).UpdateNavigationBounds();
            }));

    public static readonly BindableProperty MaxDateProperty = BindableProperty.Create(
        nameof(MaxDate), typeof(DateOnly?), typeof(SchedulerCalendarView),
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(SchedulerCalendarView), () =>
            {
                ((SchedulerCalendarView)b).UpdateNavigationBounds();
            }));

    public static readonly BindableProperty AllowPanProperty = BindableProperty.Create(
        nameof(AllowPan), typeof(bool), typeof(SchedulerCalendarView), true,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(SchedulerCalendarView), () =>
            {
                ((SchedulerCalendarView)b).UpdateGestures();
            }));

    public static readonly BindableProperty AllowZoomProperty = BindableProperty.Create(
        nameof(AllowZoom), typeof(bool), typeof(SchedulerCalendarView), false,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(SchedulerCalendarView), () =>
            {
                ((SchedulerCalendarView)b).UpdateGestures();
            }));

    public ISchedulerEventProvider? Provider
    {
        get => (ISchedulerEventProvider?)GetValue(ProviderProperty);
        set => SetValue(ProviderProperty, value);
    }

    public DateOnly SelectedDate
    {
        get => (DateOnly)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    public DateOnly DisplayMonth
    {
        get => (DateOnly)GetValue(DisplayMonthProperty);
        set => SetValue(DisplayMonthProperty, value);
    }

    public bool ShowCalendarCellEventCountOnly
    {
        get => (bool)GetValue(ShowCalendarCellEventCountOnlyProperty);
        set => SetValue(ShowCalendarCellEventCountOnlyProperty, value);
    }

    public DataTemplate? EventItemTemplate
    {
        get => (DataTemplate?)GetValue(EventItemTemplateProperty);
        set => SetValue(EventItemTemplateProperty, value);
    }

    public DataTemplate? OverflowItemTemplate
    {
        get => (DataTemplate?)GetValue(OverflowItemTemplateProperty);
        set => SetValue(OverflowItemTemplateProperty, value);
    }

    public DataTemplate? LoaderTemplate
    {
        get => (DataTemplate?)GetValue(LoaderTemplateProperty);
        set => SetValue(LoaderTemplateProperty, value);
    }

    public int MaxEventsPerCell
    {
        get => (int)GetValue(MaxEventsPerCellProperty);
        set => SetValue(MaxEventsPerCellProperty, value);
    }

    /// <summary>Leave unset to follow the active theme.</summary>
    public Color? CalendarCellColor
    {
        get => (Color?)GetValue(CalendarCellColorProperty);
        set => SetValue(CalendarCellColorProperty, value);
    }

    /// <summary>Leave unset to follow the active theme.</summary>
    public Color? CalendarCellSelectedColor
    {
        get => (Color?)GetValue(CalendarCellSelectedColorProperty);
        set => SetValue(CalendarCellSelectedColorProperty, value);
    }

    /// <summary>Leave unset to follow the active theme.</summary>
    public Color? CurrentDayColor
    {
        get => (Color?)GetValue(CurrentDayColorProperty);
        set => SetValue(CurrentDayColorProperty, value);
    }

    public DayOfWeek FirstDayOfWeek
    {
        get => (DayOfWeek)GetValue(FirstDayOfWeekProperty);
        set => SetValue(FirstDayOfWeekProperty, value);
    }

    public DateOnly? MinDate
    {
        get => (DateOnly?)GetValue(MinDateProperty);
        set => SetValue(MinDateProperty, value);
    }

    public DateOnly? MaxDate
    {
        get => (DateOnly?)GetValue(MaxDateProperty);
        set => SetValue(MaxDateProperty, value);
    }

    public bool AllowPan
    {
        get => (bool)GetValue(AllowPanProperty);
        set => SetValue(AllowPanProperty, value);
    }

    public bool AllowZoom
    {
        get => (bool)GetValue(AllowZoomProperty);
        set => SetValue(AllowZoomProperty, value);
    }

    public static readonly BindableProperty UseFeedbackProperty = BindableProperty.Create(
        nameof(UseFeedback), typeof(bool), typeof(SchedulerCalendarView), true);

    public bool UseFeedback
    {
        get => (bool)GetValue(UseFeedbackProperty);
        set => SetValue(UseFeedbackProperty, value);
    }


    public SchedulerCalendarView()
    {
        monthLabel = new Label
        {
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        prevButton = new Button
        {
            Text = "<",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Transparent,
            WidthRequest = 44,
            BorderWidth = 0,
            Padding = 0
        }.Neutralize();
        prevButton.SetDynamicResource(Button.TextColorProperty, ShinyThemeKeys.Color.Primary);
        prevButton.Clicked += (_, _) => NavigateMonth(-1);

        nextButton = new Button
        {
            Text = ">",
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = Colors.Transparent,
            WidthRequest = 44,
            BorderWidth = 0,
            Padding = 0
        }.Neutralize();
        nextButton.SetDynamicResource(Button.TextColorProperty, ShinyThemeKeys.Color.Primary);
        nextButton.Clicked += (_, _) => NavigateMonth(1);

        headerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(44)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(44))
            },
            HeightRequest = 44
        };
        headerGrid.Add(prevButton, 0);
        headerGrid.Add(monthLabel, 1);
        headerGrid.Add(nextButton, 2);

        dayHeaderGrid = new Grid { HeightRequest = 30 };
        for (var i = 0; i < 7; i++)
            dayHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));

        calendarGrid = new Grid();
        for (var i = 0; i < 7; i++)
            calendarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var i = 0; i < 6; i++)
            calendarGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

        for (var i = 0; i < 42; i++)
        {
            var cell = new CalendarDayCell();
            cell.DayTapped = OnDayTapped;
            cell.EventTapped = OnEventTapped;
            cells[i] = cell;
            calendarGrid.Add(cell, i % 7, i / 7);
        }

        loaderOverlay = new ContentView { IsVisible = false, Opacity = 0.8 };
        loaderOverlay.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.Surface);

        rootGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(new GridLength(44)),
                new RowDefinition(new GridLength(30)),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 0
        };
        rootGrid.Add(headerGrid, 0, 0);
        rootGrid.Add(dayHeaderGrid, 0, 1);
        rootGrid.Add(calendarGrid, 0, 2);
        rootGrid.Add(loaderOverlay, 0, 2);

        swipeLeft = new SwipeGestureRecognizer { Direction = SwipeDirection.Left };
        swipeLeft.Swiped += (_, _) => NavigateMonth(1);
        swipeRight = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
        swipeRight.Swiped += (_, _) => NavigateMonth(-1);

        HorizontalOptions = LayoutOptions.Fill;
        VerticalOptions = LayoutOptions.Fill;
        Content = rootGrid;
        UpdateGestures();
        RebuildCalendar();

        AutomationIds.Link(this, prevButton, PreviousMonthAutomationIdSuffix);
        AutomationIds.Link(this, nextButton, NextMonthAutomationIdSuffix);
        AutomationIds.Link(this, monthLabel, MonthTitleAutomationIdSuffix);
        AutomationIds.Track(this, NameDayCells);

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(SchedulerCalendarView));
    }

    void UpdateGestures()
    {
        calendarGrid.GestureRecognizers.Remove(swipeLeft);
        calendarGrid.GestureRecognizers.Remove(swipeRight);

        if (AllowPan)
        {
            calendarGrid.GestureRecognizers.Add(swipeLeft);
            calendarGrid.GestureRecognizers.Add(swipeRight);
        }

        if (pinchGesture != null)
            calendarGrid.GestureRecognizers.Remove(pinchGesture);

        if (AllowZoom)
        {
            pinchGesture = new PinchGestureRecognizer();
            pinchGesture.PinchUpdated += OnPinchUpdated;
            calendarGrid.GestureRecognizers.Add(pinchGesture);
        }
        else
        {
            calendarGrid.Scale = 1;
            currentScale = 1;
        }
    }

    void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                startScale = currentScale;
                break;
            case GestureStatus.Running:
                currentScale = Math.Clamp(startScale * e.Scale, 0.5, 3.0);
                calendarGrid.Scale = currentScale;
                break;
        }
    }

    void NavigateMonth(int direction)
    {
        var target = DisplayMonth.AddMonths(direction);
        if (MinDate.HasValue && new DateOnly(target.Year, target.Month, DateTime.DaysInMonth(target.Year, target.Month)) < MinDate.Value)
            return;
        if (MaxDate.HasValue && new DateOnly(target.Year, target.Month, 1) > MaxDate.Value)
            return;

        DisplayMonth = target;
    }

    void UpdateNavigationBounds()
    {
        var dm = DisplayMonth;
        prevButton.IsEnabled = !MinDate.HasValue ||
            new DateOnly(dm.Year, dm.Month, 1).AddMonths(-1).AddDays(DateTime.DaysInMonth(dm.AddMonths(-1).Year, dm.AddMonths(-1).Month) - 1) >= MinDate.Value;
        nextButton.IsEnabled = !MaxDate.HasValue ||
            new DateOnly(dm.Year, dm.Month, 1).AddMonths(1) <= MaxDate.Value;
        prevButton.Opacity = prevButton.IsEnabled ? 1.0 : 0.3;
        nextButton.Opacity = nextButton.IsEnabled ? 1.0 : 0.3;
    }

    void OnProviderChanged() => LoadEvents();
    void OnSelectedDateChanged() => UpdateCellSelection();
    void OnDisplayMonthChanged()
    {
        RebuildCalendar();
        UpdateNavigationBounds();
    }

    void RebuildCalendar()
    {
        var dm = DisplayMonth;
        monthLabel.Text = new DateTime(dm.Year, dm.Month, 1).ToString("MMMM yyyy");

        BuildDayHeaders();
        BuildDayCells();
        LoadEvents();
    }

    void BuildDayHeaders()
    {
        dayHeaderGrid.Children.Clear();
        var names = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.AbbreviatedDayNames;
        var first = (int)FirstDayOfWeek;

        for (var i = 0; i < 7; i++)
        {
            var idx = (first + i) % 7;
            var lbl = new Label
            {
                Text = names[idx],
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }.WithFontSize(ShinyThemeKeys.Type.LabelSmallSize);
            lbl.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurfaceVariant);
            dayHeaderGrid.Add(lbl, i);
        }
    }

    void BuildDayCells()
    {
        var dm = DisplayMonth;
        var firstOfMonth = new DateOnly(dm.Year, dm.Month, 1);
        var firstDayOffset = ((int)firstOfMonth.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        var startDate = firstOfMonth.AddDays(-firstDayOffset);
        var today = DateOnly.FromDateTime(ShinyTime.For(this).Today());

        for (var i = 0; i < 42; i++)
        {
            var date = startDate.AddDays(i);
            var cell = cells[i];
            cell.Date = date;
            cell.IsCurrentMonth = date.Month == dm.Month && date.Year == dm.Year;
            cell.IsToday = date == today;
            cell.IsSelected = date == SelectedDate;
            cell.MaxEvents = MaxEventsPerCell;
            cell.ShowCountOnly = ShowCalendarCellEventCountOnly;
            cell.EventTemplate = EventItemTemplate;
            cell.OverflowTemplate = OverflowItemTemplate;
            cell.CellColor = CalendarCellColor;
            cell.SelectedColor = CalendarCellSelectedColor;
            cell.CurrentDayColor = CurrentDayColor;
            cell.Events = [];
        }
        NameDayCells();
    }

    void NameDayCells()
    {
        foreach (var cell in cells)
            AutomationIds.Set(cell, SchedulerAutomationIds.For(this, DayAutomationIdSuffix, SchedulerAutomationIds.DateKey(cell.Date)));
    }

    void UpdateCellSelection()
    {
        for (var i = 0; i < 42; i++)
            cells[i].IsSelected = cells[i].Date == SelectedDate;
    }

    async void LoadEvents()
    {
        if (Provider == null) return;

        loadCts?.Cancel();
        loadCts = new CancellationTokenSource();
        var token = loadCts.Token;

        ShowLoader(true);

        try
        {
            var dm = DisplayMonth;
            var firstOfMonth = new DateOnly(dm.Year, dm.Month, 1);
            var firstDayOffset = ((int)firstOfMonth.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
            var startDate = firstOfMonth.AddDays(-firstDayOffset);
            var endDate = startDate.AddDays(42);

            var start = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue));
            var end = new DateTimeOffset(endDate.ToDateTime(TimeOnly.MinValue));

            var events = await Provider.GetEvents(start, end);
            if (token.IsCancellationRequested) return;

            var grouped = events
                .SelectMany(e =>
                {
                    var results = new List<(DateOnly Date, SchedulerEvent Event)>();
                    var eventStart = DateOnly.FromDateTime(e.Start.LocalDateTime);
                    var eventEnd = DateOnly.FromDateTime(e.End.LocalDateTime);
                    if (e.End.LocalDateTime.TimeOfDay == TimeSpan.Zero && eventEnd > eventStart)
                        eventEnd = eventEnd.AddDays(-1);

                    for (var d = eventStart; d <= eventEnd; d = d.AddDays(1))
                        results.Add((d, e));
                    return results;
                })
                .GroupBy(x => x.Date)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<SchedulerEvent>)g.Select(x => x.Event).ToList());

            for (var i = 0; i < 42; i++)
            {
                if (grouped.TryGetValue(cells[i].Date, out var dayEvents))
                    cells[i].Events = dayEvents;
                else
                    cells[i].Events = [];
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex)
        {
            // These loaders are `async void`, so anything escaping here is posted to the sync
            // context and terminates the process - including an exception from the consumer's
            // ISchedulerEventProvider. Surface it for debugging and leave the view usable rather
            // than taking the host app down.
            System.Diagnostics.Debug.WriteLine($"[Shiny.Scheduler] load failed: {ex}");
        }
        finally
        {
            if (!token.IsCancellationRequested)
                ShowLoader(false);
        }
    }

    void ShowLoader(bool show)
    {
        if (show && loaderOverlay.Content == null)
        {
            var template = LoaderTemplate ?? DefaultTemplates.CreateLoaderTemplate();
            loaderOverlay.Content = (View)template.CreateContent();
        }
        loaderOverlay.IsVisible = show;
    }

    void OnDayTapped(DateOnly date)
    {
        if (MinDate.HasValue && date < MinDate.Value) return;
        if (MaxDate.HasValue && date > MaxDate.Value) return;
        if (Provider != null && !Provider.CanCalendarSelect(date))
            return;

        if (UseFeedback)
            FeedbackHelper.Execute(this, "DaySelected");

        SelectedDate = date;

        if (date.Month != DisplayMonth.Month || date.Year != DisplayMonth.Year)
            DisplayMonth = new DateOnly(date.Year, date.Month, 1);

        Provider?.OnCalendarDateSelected(date);
    }

    void OnEventTapped(SchedulerEvent evt)
    {
        if (UseFeedback)
            FeedbackHelper.Execute(this, "EventSelected");
        Provider?.OnEventSelected(evt);
    }
}