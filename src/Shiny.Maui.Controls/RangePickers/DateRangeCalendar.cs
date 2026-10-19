using System.Globalization;
using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;
using Shiny.Controls.RangePickers;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls;

/// <summary>Raised when a range is completed, or cleared (<see cref="Range"/> null).</summary>
public class DateRangeSelectedEventArgs(DateRange? range) : EventArgs
{
    public DateRange? Range { get; } = range;
}


/// <summary>
/// An inline calendar for picking a date range: tap the first day, then the last. The rules -
/// min/max dates and spans, blocked days, single-day ranges - come from the shared range picker
/// engine, so the Blazor <c>DateRangeCalendar</c> picks exactly the same ranges.
/// </summary>
public partial class DateRangeCalendar : ContentView
{
    /// <summary>
    /// Appended to the calendar's <see cref="Element.AutomationId"/>, then the date as <c>yyyyMMdd</c>, to
    /// name each day: <c>&lt;id&gt;_Day_20261019</c>. Keyed by date, not grid position, so a test finds the
    /// same day whichever month is showing. A padding day that another visible month also shows is left
    /// unnamed there, so every id is unique.
    /// </summary>
    public const string DayAutomationIdSuffix = "_Day";

    /// <summary>Appended to the calendar's <see cref="Element.AutomationId"/> to name the previous-month button.</summary>
    public const string PreviousMonthAutomationIdSuffix = "_PreviousMonth";

    /// <summary>Appended to the calendar's <see cref="Element.AutomationId"/> to name the next-month button.</summary>
    public const string NextMonthAutomationIdSuffix = "_NextMonth";

    /// <summary>Appended to the calendar's <see cref="Element.AutomationId"/> to name each month's title, by index: <c>&lt;id&gt;_MonthTitle_0</c>.</summary>
    public const string MonthTitleAutomationIdSuffix = "_MonthTitle";

    /// <summary>Appended to the calendar's <see cref="Element.AutomationId"/> to name each preset chip, by index: <c>&lt;id&gt;_Preset_0</c>.</summary>
    public const string PresetAutomationIdSuffix = "_Preset";

    readonly DateRangeSelector selector = new();
    readonly HorizontalStackLayout presetRow;
    readonly ScrollView presetScroll;
    readonly Grid monthsGrid;
    readonly List<MonthView> months = [];
    bool syncing;

    public DateRangeCalendar()
    {
        this.presetRow = new HorizontalStackLayout { Spacing = 8, Padding = new Thickness(4, 0) };
        this.presetScroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            Content = this.presetRow,
            IsVisible = false,
            Margin = new Thickness(0, 0, 0, 8)
        };

        this.monthsGrid = new Grid { ColumnSpacing = 24 };

        var root = new VerticalStackLayout { Children = { this.presetScroll, this.monthsGrid } };

        // The preview follows the pointer only while it is over the calendar.
        var pointer = new PointerGestureRecognizer();
        pointer.PointerExited += (_, _) =>
        {
            if (this.selector.Hover is null)
                return;
            this.selector.Hover = null;
            this.Refresh();
        };
        root.GestureRecognizers.Add(pointer);

        this.Content = root;
        this.RebuildMonths();
        this.RebuildConstraints();
        this.Refresh();
        AutomationIds.Track(this, this.ApplyAutomationIds);

        StyleGuard.MarkReady(this, typeof(DateRangeCalendar));
    }


    internal DateRangeSelector Selector => this.selector;

    internal IReadOnlyList<RangeDayCell> Cells => [.. this.months.SelectMany(m => m.Cells)];

    CultureInfo EffectiveCulture => this.Culture ?? CultureInfo.CurrentCulture;

    DayOfWeek EffectiveFirstDay => this.FirstDayOfWeek ?? CalendarMath.FirstDayOfWeek(this.EffectiveCulture);

    DateOnly Today => DateOnly.FromDateTime(ShinyTime.For(this).Today());


    /// <summary>The range, once both ends are picked.</summary>
    public DateRange? Range => this.selector.Range;

    /// <summary>Raised when a range is completed or cleared.</summary>
    public event EventHandler<DateRangeSelectedEventArgs>? RangeSelected;


    /// <summary>Moves the view one month back.</summary>
    public void PreviousMonth() => this.DisplayMonth = this.DisplayMonth.AddMonths(-1);

    /// <summary>Moves the view one month forward.</summary>
    public void NextMonth() => this.DisplayMonth = this.DisplayMonth.AddMonths(1);

    /// <summary>Clears the selection.</summary>
    public void Clear()
    {
        this.selector.Clear();
        this.PushToProperties();
        this.Refresh();
        this.RaiseSelected();
    }


    /// <summary>Applies a preset, if the range it produces today is allowed.</summary>
    public bool ApplyPreset(DateRangePreset preset)
    {
        var range = preset.Resolve(this.Today);
        if (this.selector.Constraints.Validate(range) != DateRangeError.None)
            return false;

        this.selector.SetRange(range);
        this.PushToProperties();
        this.ShowMonthOf(range.Start);
        this.Refresh();
        this.RaiseSelected();
        return true;
    }


    internal void OnDayTapped(DateOnly date)
    {
        var result = this.selector.Tap(date);
        if (result == DateRangeTapResult.Ignored)
            return;

        this.PushToProperties();
        this.Refresh();
        FeedbackHelper.Execute(this, result == DateRangeTapResult.Completed ? "RangeSelected" : "StartSelected");

        if (result == DateRangeTapResult.Completed)
            this.RaiseSelected();
    }


    void OnDayHovered(DateOnly date)
    {
        if (!this.selector.IsPickingEnd || this.selector.Hover == date)
            return;

        this.selector.Hover = date;
        this.Refresh();
    }


    void RaiseSelected()
    {
        var range = this.selector.Range;
        this.RangeSelected?.Invoke(this, new DateRangeSelectedEventArgs(range));
        if (range is not null && this.RangeSelectedCommand?.CanExecute(range) == true)
            this.RangeSelectedCommand.Execute(range);
    }


    /// <summary>Writes the selector's state to StartDate/EndDate without echoing back into it.</summary>
    void PushToProperties()
    {
        this.syncing = true;
        try
        {
            this.StartDate = this.selector.Start?.ToDateTime(TimeOnly.MinValue);
            this.EndDate = this.selector.End?.ToDateTime(TimeOnly.MinValue);
        }
        finally
        {
            this.syncing = false;
        }
    }


    /// <summary>A bound StartDate/EndDate arrived from outside.</summary>
    void PullFromProperties()
    {
        if (this.syncing)
            return;

        DateOnly? start = this.StartDate is { } s ? DateOnly.FromDateTime(s) : null;
        DateOnly? end = this.EndDate is { } e ? DateOnly.FromDateTime(e) : null;

        if (start is { } a && end is { } b)
            this.selector.SetRange(new DateRange(a, b));
        else
            this.selector.SetStart(start);

        if (start is { } first && !this.IsMonthVisible(first))
            this.ShowMonthOf(first);

        this.Refresh();
    }


    bool IsMonthVisible(DateOnly date)
    {
        var first = DateOnly.FromDateTime(this.DisplayMonth);
        var last = first.AddMonths(Math.Max(1, this.Months)).AddDays(-1);
        return date >= first && date <= last;
    }


    void ShowMonthOf(DateOnly date) => this.DisplayMonth = new DateTime(date.Year, date.Month, 1);


    void RebuildConstraints()
    {
        this.selector.Constraints = new DateRangeConstraints
        {
            MinDate = this.MinDate is { } min ? DateOnly.FromDateTime(min) : null,
            MaxDate = this.MaxDate is { } max ? DateOnly.FromDateTime(max) : null,
            MinDays = this.MinDays,
            MaxDays = this.MaxDays,
            AllowSingleDay = this.AllowSingleDay,
            DisabledDates = this.DisabledDates is { Count: > 0 } dates ? dates.Select(DateOnly.FromDateTime).ToHashSet() : null,
            DisabledDaysOfWeek = this.DisabledDaysOfWeek is { Count: > 0 } days ? days.ToHashSet() : null,
            IsDateDisabled = this.IsDateDisabled,
            AllowDisabledDatesInRange = this.AllowDisabledDatesInRange
        };
    }


    void RebuildMonths()
    {
        var count = Math.Clamp(this.Months, 1, 12);
        if (this.months.Count == count)
            return;

        this.monthsGrid.Children.Clear();
        this.monthsGrid.ColumnDefinitions.Clear();
        foreach (var month in this.months)
            month.Detach();
        this.months.Clear();

        for (var i = 0; i < count; i++)
        {
            this.monthsGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var view = new MonthView(this, showPrevious: i == 0, showNext: i == count - 1);
            view.ApplyAutomationIds(i);
            this.months.Add(view);
            this.monthsGrid.Add(view, i, 0);
        }
    }


    void RebuildPresets()
    {
        this.presetRow.Children.Clear();
        var presets = this.Presets;
        this.presetScroll.IsVisible = presets is { Count: > 0 };
        if (presets is null)
            return;

        foreach (var preset in presets)
        {
            var label = new Label { Text = preset.Label, VerticalTextAlignment = TextAlignment.Center }
                .WithFontSize(ShinyThemeKeys.Type.LabelLargeSize);
            var chip = new Border
            {
                Padding = new Thickness(12, 6),
                StrokeShape = new RoundRectangle().WithCornerRadius(ShinyThemeKeys.Shape.CornerFullRadius),
                StrokeThickness = 1,
                Content = label,
                BindingContext = preset
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => this.ApplyPreset(preset);
            chip.GestureRecognizers.Add(tap);
            SemanticProperties.SetDescription(chip, preset.Label);
            this.presetRow.Add(chip);
        }
        this.NamePresets();
        this.RefreshPresets();
    }


    void NamePresets()
    {
        for (var i = 0; i < this.presetRow.Children.Count; i++)
        {
            if (this.presetRow.Children[i] is Element chip)
                AutomationIds.Set(chip, AutomationIds.For(this, PresetAutomationIdSuffix, i));
        }
    }


    /// <summary>Renames every part after the calendar's id. Days are also renamed on each <see cref="Refresh"/>.</summary>
    void ApplyAutomationIds()
    {
        this.NamePresets();
        for (var i = 0; i < this.months.Count; i++)
            this.months[i].ApplyAutomationIds(i);
    }


    void RefreshPresets()
    {
        var today = this.Today;
        foreach (var chip in this.presetRow.Children.OfType<Border>())
        {
            if (chip.BindingContext is not DateRangePreset preset || chip.Content is not Label label)
                continue;

            var range = preset.Resolve(today);
            var active = this.selector.Range == range;
            chip.Opacity = this.selector.Constraints.Validate(range) == DateRangeError.None ? 1 : 0.4;

            if (active)
            {
                RangePickerPaint.Fill(chip, ShinyThemeKeys.Color.SecondaryContainer);
                chip.SetDynamicResource(Border.StrokeProperty, ShinyThemeKeys.Brush.SecondaryContainer);
                label.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSecondaryContainer);
            }
            else
            {
                RangePickerPaint.Fill(chip, null);
                chip.SetDynamicResource(Border.StrokeProperty, ShinyThemeKeys.Brush.OutlineVariant);
                label.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurfaceVariant);
            }
        }
    }


    internal void Refresh()
    {
        this.selector.Today = this.Today;
        var first = DateOnly.FromDateTime(this.DisplayMonth);
        for (var i = 0; i < this.months.Count; i++)
            this.months[i].Show(first.AddMonths(i));

        this.RefreshPresets();
    }


    /// <summary>One month: a header, the weekday initials and a six-week grid of days.</summary>
    sealed class MonthView : VerticalStackLayout
    {
        readonly DateRangeCalendar owner;
        readonly Label title;
        readonly Grid weekdays;
        readonly Grid days;
        readonly RangeDayCell[] cells = new RangeDayCell[42];
        readonly View? previous;
        readonly View? next;

        public MonthView(DateRangeCalendar owner, bool showPrevious, bool showNext)
        {
            this.owner = owner;
            this.Spacing = 4;

            this.title = new Label
            {
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                FontAttributes = FontAttributes.Bold
            }.WithFontSize(ShinyThemeKeys.Type.TitleSmallSize);
            this.title.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurface);

            var header = new Grid
            {
                ColumnDefinitions = [new(44), new(GridLength.Star), new(44)],
                HeightRequest = 44
            };
            header.Add(this.title, 1, 0);
            if (showPrevious)
            {
                this.previous = NavButton("‹", "Previous month", owner.PreviousMonth);
                header.Add(this.previous, 0, 0);
            }
            if (showNext)
            {
                this.next = NavButton("›", "Next month", owner.NextMonth);
                header.Add(this.next, 2, 0);
            }

            this.weekdays = SevenColumns();
            for (var i = 0; i < 7; i++)
            {
                var day = new Label
                {
                    HorizontalTextAlignment = TextAlignment.Center,
                    HeightRequest = 20
                }.WithFontSize(ShinyThemeKeys.Type.LabelMediumSize);
                day.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurfaceVariant);
                this.weekdays.Add(day, i, 0);
            }

            this.days = SevenColumns();
            for (var r = 0; r < 6; r++)
                this.days.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            for (var i = 0; i < 42; i++)
            {
                var cell = new RangeDayCell();
                cell.Tapped += this.OnTapped;
                cell.Hovered += this.OnHovered;
                this.cells[i] = cell;
                this.days.Add(cell, i % 7, i / 7);
            }

            this.Children.Add(header);
            this.Children.Add(this.weekdays);
            this.Children.Add(this.days);
        }

        public IEnumerable<RangeDayCell> Cells => this.cells;

        public void ApplyAutomationIds(int index)
        {
            AutomationIds.Set(this.title, AutomationIds.For(this.owner, MonthTitleAutomationIdSuffix, index));
            if (this.previous is not null)
                AutomationIds.Set(this.previous, AutomationIds.For(this.owner, PreviousMonthAutomationIdSuffix));
            if (this.next is not null)
                AutomationIds.Set(this.next, AutomationIds.For(this.owner, NextMonthAutomationIdSuffix));
            this.NameDays();
        }

        void NameDays()
        {
            var prefix = AutomationIds.For(this.owner, DayAutomationIdSuffix);
            foreach (var cell in this.cells)
            {
                // A padding day is unnamed when hidden, or when the month it belongs to is also on
                // screen - that month's cell carries the id.
                var outside = cell.State.HasFlag(CalendarDayState.OutsideMonth);
                var named = prefix is not null && (!outside || (this.owner.ShowOutsideDays && !this.owner.IsMonthVisible(cell.Date)));
                AutomationIds.Set(cell, named ? $"{prefix}_{cell.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}" : null);
            }
        }

        public void Detach()
        {
            foreach (var cell in this.cells)
            {
                cell.Tapped -= this.OnTapped;
                cell.Hovered -= this.OnHovered;
            }
        }

        void OnTapped(object? sender, DateOnly date) => this.owner.OnDayTapped(date);

        void OnHovered(object? sender, DateOnly date) => this.owner.OnDayHovered(date);

        public void Show(DateOnly month)
        {
            var culture = this.owner.EffectiveCulture;
            var firstDay = this.owner.EffectiveFirstDay;
            var selector = this.owner.selector;

            this.title.Text = CalendarMath.MonthTitle(month.Year, month.Month, culture);

            var order = CalendarMath.WeekdayOrder(firstDay);
            for (var i = 0; i < 7; i++)
                ((Label)this.weekdays.Children[i]).Text = CalendarMath.ShortDayName(order[i], culture);

            var dates = CalendarMath.MonthCells(month.Year, month.Month, firstDay);
            var hasEnd = selector.End is not null && selector.End != selector.Start;
            var previewActive = selector.IsPickingEnd && selector.Hover is { } hover && selector.CanEndAt(hover) && hover > selector.Start;

            for (var i = 0; i < 42; i++)
            {
                var date = dates[i];
                this.cells[i].Update(
                    date,
                    selector.StateOf(date, month.Year, month.Month),
                    hasEnd,
                    previewActive,
                    this.owner.ShowOutsideDays,
                    culture
                );
            }
            this.NameDays();
        }

        static Grid SevenColumns()
        {
            var grid = new Grid();
            for (var i = 0; i < 7; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            return grid;
        }

        static View NavButton(string glyph, string description, Action action)
        {
            var label = new Label
            {
                Text = glyph,
                FontSize = 26,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
            label.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurfaceVariant);

            var target = new Border
            {
                StrokeThickness = 0,
                BackgroundColor = Colors.Transparent,
                StrokeShape = new RoundRectangle().WithCornerRadius(ShinyThemeKeys.Shape.CornerFullRadius),
                Content = label
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => action();
            target.GestureRecognizers.Add(tap);
            SemanticProperties.SetDescription(target, description);
            AutomationProperties.SetIsInAccessibleTree(target, true);
            return target;
        }
    }
}
