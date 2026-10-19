using Shiny.Maui.Controls.Themes;
using Shiny.Maui.Controls.Infrastructure;

namespace Shiny.Maui.Controls;

public class LoadingOverlay : Overlay
{
    /// <summary>Appended to the overlay's <see cref="Element.AutomationId"/> to name the message label.</summary>
    public const string MessageAutomationIdSuffix = "_Message";

    /// <summary>Appended to the overlay's id to name the progress bar shown when not indeterminate.</summary>
    public const string ProgressAutomationIdSuffix = "_Progress";

    readonly ActivityIndicator spinner;
    readonly ProgressBar progressBar;
    readonly Label messageLabel;
    readonly StackLayout contentLayout;

    public LoadingOverlay()
    {
        spinner = new ActivityIndicator
        {
            IsRunning = true,
            HeightRequest = 48,
            WidthRequest = 48,
            HorizontalOptions = LayoutOptions.Center
        };
        // Content sits on the dark Scrim backdrop, so use the inverse-on-surface role.
        spinner.SetDynamicResource(ActivityIndicator.ColorProperty, ShinyThemeKeys.Color.InverseOnSurface);

        progressBar = new ProgressBar
        {
            IsVisible = false,
            WidthRequest = 200,
            HorizontalOptions = LayoutOptions.Center,
            // Translucent white track left as-is (a tinted inverse-on-surface veil).
            TrackColor = Color.FromArgb("#FFFFFF33")
        };
        progressBar.SetDynamicResource(ProgressBar.BarColorProperty, ShinyThemeKeys.Color.InverseOnSurface);

        messageLabel = new Label
        {
            IsVisible = false,
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center
        }.WithFontSize(ShinyThemeKeys.Type.BodyMediumSize);
        messageLabel.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.InverseOnSurface);

        contentLayout = new StackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { spinner, progressBar, messageLabel }
        };

        OverlayContentTemplate = new DataTemplate(() => contentLayout);

        AutomationIds.Link(this, messageLabel, MessageAutomationIdSuffix);
        AutomationIds.Link(this, progressBar, ProgressAutomationIdSuffix);

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(LoadingOverlay));
    }

    // IsIndeterminate
    public static readonly BindableProperty IsIndeterminateProperty = BindableProperty.Create(
        nameof(IsIndeterminate), typeof(bool), typeof(LoadingOverlay), true,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(LoadingOverlay), () =>
            {
                ((LoadingOverlay)b).OnModeChanged();
            }));
    public bool IsIndeterminate { get => (bool)GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }

    // Progress (0-100)
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(
        nameof(Progress), typeof(double), typeof(LoadingOverlay), 0.0,
        BindingMode.TwoWay,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(LoadingOverlay), () =>
            {
                ((LoadingOverlay)b).progressBar.Value = (double)n;
            }));
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }

    // Message
    public static readonly BindableProperty MessageProperty = BindableProperty.Create(
        nameof(Message), typeof(string), typeof(LoadingOverlay), null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(LoadingOverlay), () =>
            {
            var lo = (LoadingOverlay)b;
            var text = (string?)n;
            lo.messageLabel.Text = text;
            lo.messageLabel.IsVisible = !string.IsNullOrWhiteSpace(text);
        }));
    public string? Message { get => (string?)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }

    // SpinnerColor
    public static readonly BindableProperty SpinnerColorProperty = BindableProperty.Create(
        nameof(SpinnerColor), typeof(Color), typeof(LoadingOverlay), null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(LoadingOverlay), () =>
            {
            var lo = (LoadingOverlay)b;
            if (n is Color c)
                lo.spinner.Color = c;
            else
                lo.spinner.SetDynamicResource(ActivityIndicator.ColorProperty, ShinyThemeKeys.Color.InverseOnSurface);
        }));
    public Color? SpinnerColor { get => (Color?)GetValue(SpinnerColorProperty); set => SetValue(SpinnerColorProperty, value); }

    void OnModeChanged()
    {
        spinner.IsVisible = IsIndeterminate;
        spinner.IsRunning = IsIndeterminate;
        progressBar.IsVisible = !IsIndeterminate;
    }
}
