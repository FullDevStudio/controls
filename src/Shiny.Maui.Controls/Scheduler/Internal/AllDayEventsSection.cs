using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.Scheduler.Internal;

class AllDayEventsSection : ContentView
{
    readonly HorizontalStackLayout stack;
    readonly List<(View View, SchedulerEvent Event)> eventViews = [];

    public AllDayEventsSection()
    {
        stack = new HorizontalStackLayout { Spacing = 4, Padding = new Thickness(60, 4, 4, 4) };
        var scroll = new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = stack,
            HeightRequest = 32
        };
        Content = scroll;
        IsVisible = false;

        // Named by the agenda view; the chips follow this section's id.
        AutomationIds.Track(this, () => SchedulerAutomationIds.NameEvents(this, eventViews));
    }

    public void SetEvents(IReadOnlyList<SchedulerEvent> events, DataTemplate? template, Action<SchedulerEvent>? onTapped)
    {
        stack.Children.Clear();
        eventViews.Clear();
        IsVisible = events.Count > 0;

        foreach (var evt in events)
        {
            View view;
            if (template != null)
            {
                view = (View)template.CreateContent();
                view.BindingContext = evt;
            }
            else
            {
                var chipLabel = new Label { Text = evt.Title}.WithFontSize(ShinyThemeKeys.Type.LabelSmallSize);
                var chip = new Border
                {
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle().WithCornerRadius(ShinyThemeKeys.Shape.CornerExtraSmallRadius),
                    Stroke = Colors.Transparent,
                    Padding = new Thickness(8, 2),
                    Content = chipLabel
                };

                // The event's own colour wins; otherwise the chip follows the theme accent.
                if (evt.Color is { } chipColor)
                {
                    chip.BackgroundColor = chipColor;
                    chipLabel.TextColor = Colors.White;
                }
                else
                {
                    chip.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.Primary);
                    chipLabel.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnPrimary);
                }

                view = chip;
            }

            if (onTapped != null)
            {
                var tap = new TapGestureRecognizer();
                var captured = evt;
                tap.Tapped += (_, _) => onTapped(captured);
                view.GestureRecognizers.Add(tap);
            }
            stack.Children.Add(view);
            eventViews.Add((view, evt));
        }
        SchedulerAutomationIds.NameEvents(this, eventViews);
    }
}