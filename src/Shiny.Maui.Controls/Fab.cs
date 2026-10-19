using System.Windows.Input;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls;

public class Fab : ContentView, ITapTarget
{
    const double DefaultSize = 56;
    const double DefaultIconSize = 24;
    const double IconTextSpacing = 8;

    readonly Border border;
    readonly Grid innerGrid;
    readonly Image iconImage;
    readonly Label textLabel;
    readonly TapGestureRecognizer tap;

    public Fab()
    {
        iconImage = new Image
        {
            WidthRequest = DefaultIconSize,
            HeightRequest = DefaultIconSize,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Aspect = Aspect.AspectFit,
            // No Icon set yet — an empty but visible Image still reserves IconSize inside the button.
            IsVisible = false
        };

        textLabel = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.NoWrap,
            IsVisible = false
        };

        innerGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            },
            // Spacing is applied by the grid whether or not both cells have a visible child, so it
            // is set from UpdateContentLayout instead — a hidden icon must not pad the label.
            ColumnSpacing = 0,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        innerGrid.Add(iconImage, 0, 0);
        innerGrid.Add(textLabel, 1, 0);

        border = new Border
        {
            HeightRequest = DefaultSize,
            MinimumWidthRequest = DefaultSize,
            Padding = new Thickness(16, 0),
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(DefaultSize / 2)
            },
            Content = innerGrid,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End
        };

        // Theme defaults — overridden if the consumer sets FabBackgroundColor / TextColor / FontSize
        // explicitly. A literal left here would beat the theme permanently, not just by default.
        border.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.Primary);
        border.SetDynamicResource(VisualElement.ShadowProperty, ShinyThemeKeys.Elevation.Level3);
        textLabel.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnPrimary);
        textLabel.SetDynamicResource(Label.FontSizeProperty, ShinyThemeKeys.Type.LabelLargeSize);

        tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        border.GestureRecognizers.Add(tap);

        Content = border;
        HorizontalOptions = LayoutOptions.End;
        VerticalOptions = LayoutOptions.End;

        UpdateTextVisibility();
        UpdateContentLayout();

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(Fab));
    }


    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon),
        typeof(ImageSource),
        typeof(Fab),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(Fab), () =>
            {
            var fab = (Fab)b;
            fab.iconImage.Source = n as ImageSource;
            fab.iconImage.IsVisible = n is not null;
            fab.UpdateContentLayout();
        }));
    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(Fab),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(Fab), () =>
            {
            var fab = (Fab)b;
            fab.textLabel.Text = n as string ?? string.Empty;
            fab.UpdateTextVisibility();
            fab.UpdateContentLayout();
        }));
    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command),
        typeof(ICommand),
        typeof(Fab),
        null);
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter),
        typeof(object),
        typeof(Fab),
        null);
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly BindableProperty FabBackgroundColorProperty = BindableProperty.Create(
        nameof(FabBackgroundColor),
        typeof(Color),
        typeof(Fab),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(Fab), () =>
            {
            var fab = (Fab)b;
            if (n is Color c)
                fab.border.BackgroundColor = c;
            else
                fab.border.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.Primary);
        }));
    public Color? FabBackgroundColor
    {
        get => (Color?)GetValue(FabBackgroundColorProperty);
        set => SetValue(FabBackgroundColorProperty, value);
    }

    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(
        nameof(BorderColor),
        typeof(Color),
        typeof(Fab),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(Fab), () =>
            {
            var fab = (Fab)b;
            if (n is Color c)
                fab.border.Stroke = c;
        }));
    public Color? BorderColor
    {
        get => (Color?)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(
        nameof(BorderThickness),
        typeof(double),
        typeof(Fab),
        0.0,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(Fab), () =>
            {
                ((Fab)b).border.StrokeThickness = (double)n;
            }));
    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(Fab),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(Fab), () =>
            {
            var fab = (Fab)b;
            if (n is Color c)
                fab.textLabel.TextColor = c;
            else
                fab.textLabel.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnPrimary);
        }));
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(Fab),
        ThemeTokens.Unset,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(Fab), () =>
            {
                ((Fab)b).textLabel.SetTokenOrValue(Label.FontSizeProperty, (double)n, ShinyThemeKeys.Type.LabelLargeSize);
            }));
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(
        nameof(FontAttributes),
        typeof(FontAttributes),
        typeof(Fab),
        Microsoft.Maui.Controls.FontAttributes.None,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(Fab), () =>
            {
                ((Fab)b).textLabel.FontAttributes = (FontAttributes)n;
            }));
    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size),
        typeof(double),
        typeof(Fab),
        DefaultSize,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(Fab), () =>
            {
            var fab = (Fab)b;
            fab.border.HeightRequest = fab.Size;
            fab.border.MinimumWidthRequest = fab.Size;
            fab.UpdateContentLayout();
        }));
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public static readonly BindableProperty IconSizeProperty = BindableProperty.Create(
        nameof(IconSize),
        typeof(double),
        typeof(Fab),
        DefaultIconSize,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(Fab), () =>
            {
            var fab = (Fab)b;
            var s = (double)n;
            fab.iconImage.WidthRequest = s;
            fab.iconImage.HeightRequest = s;
        }));
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public static readonly BindableProperty HasShadowProperty = BindableProperty.Create(
        nameof(HasShadow),
        typeof(bool),
        typeof(Fab),
        true,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(Fab), () =>
            {
                var fab = (Fab)b;
                if ((bool)n)
                    fab.border.SetDynamicResource(VisualElement.ShadowProperty, ShinyThemeKeys.Elevation.Level3);
                else
                    fab.border.Shadow = null;
            }));
    public bool HasShadow
    {
        get => (bool)GetValue(HasShadowProperty);
        set => SetValue(HasShadowProperty, value);
    }


    public static readonly BindableProperty UseFeedbackProperty = BindableProperty.Create(
        nameof(UseFeedback),
        typeof(bool),
        typeof(Fab),
        true);
    public bool UseFeedback
    {
        get => (bool)GetValue(UseFeedbackProperty);
        set => SetValue(UseFeedbackProperty, value);
    }


    public event EventHandler? Clicked;


    void OnTapped(object? sender, TappedEventArgs e) => Invoke();

    void ITapTarget.Invoke() => Invoke();

    internal void Invoke()
    {
        if (!IsEnabled)
            return;

        if (UseFeedback)
            FeedbackHelper.Execute(this, nameof(Clicked));

        Clicked?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }

    void UpdateTextVisibility()
        => textLabel.IsVisible = !string.IsNullOrEmpty(textLabel.Text);

    void UpdateContentLayout()
    {
        var hasIcon = iconImage.IsVisible;
        var hasText = textLabel.IsVisible;

        // Only pad between the two when both are actually there.
        innerGrid.ColumnSpacing = hasIcon && hasText ? IconTextSpacing : 0;

        border.HeightRequest = Size;
        border.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
        {
            CornerRadius = new CornerRadius(Size / 2)
        };

        if (!hasText)
        {
            // Icon-only (or empty): a perfect circle sized to Size.
            border.WidthRequest = Size;
            border.Padding = 0;
        }
        else
        {
            // Extended: width grows with the label, but MinimumWidthRequest keeps a short
            // label (a "+", a count) circular rather than a slightly-too-wide pill.
            border.WidthRequest = -1;
            border.Padding = new Thickness(hasIcon ? 20 : 16, 0);
        }
    }
}
