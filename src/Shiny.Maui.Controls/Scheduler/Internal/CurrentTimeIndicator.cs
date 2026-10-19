using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.Scheduler.Internal;

class CurrentTimeIndicator : ContentView
{
    readonly BoxView line;
    readonly Label timeLabel;
    readonly BoxView dot;

    public CurrentTimeIndicator()
    {
        dot = new BoxView
        {
            CornerRadius = 4,
            WidthRequest = 8,
            HeightRequest = 8,
            VerticalOptions = LayoutOptions.Center
        };

        timeLabel = new Label
        {
            FontSize = 9,
            FontAttributes = FontAttributes.Bold,
            VerticalOptions = LayoutOptions.Center,
            VerticalTextAlignment = TextAlignment.Center,
            Margin = new Thickness(2, 0, 0, 0)
        };

        line = new BoxView
        {
            HeightRequest = 2,
            VerticalOptions = LayoutOptions.Center
        };

        // "now" marker follows the theme's Error role unless the consumer overrides MarkerColor.
        this.ApplyThemeMarker();

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            VerticalOptions = LayoutOptions.Start
        };

        grid.Add(dot, 0);
        grid.Add(timeLabel, 1);
        grid.Add(line, 2);

        Content = grid;
    }

    public Color? MarkerColor
    {
        set
        {
            if (value is null)
            {
                this.ApplyThemeMarker();
                return;
            }

            line.RemoveDynamicResource(BoxView.ColorProperty);
            dot.RemoveDynamicResource(BoxView.ColorProperty);
            timeLabel.RemoveDynamicResource(Label.TextColorProperty);

            line.Color = value;
            dot.Color = value;
            timeLabel.TextColor = value;
        }
    }

    void ApplyThemeMarker()
    {
        line.SetDynamicResource(BoxView.ColorProperty, ShinyThemeKeys.Color.Error);
        dot.SetDynamicResource(BoxView.ColorProperty, ShinyThemeKeys.Color.Error);
        timeLabel.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.Error);
    }

    public void UpdateTime(bool use24HourTime)
    {
        var now = ShinyTime.For(this).LocalNow();
        timeLabel.Text = now.ToString(use24HourTime ? "HH:mm" : "h:mm tt");
    }
}