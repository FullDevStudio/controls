using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls;

/// <summary>
/// A single action inside a <see cref="FabMenu"/>.
/// <para>
/// Renders as one capsule ("pill"): the label lives inside the capsule with a tinted circular icon
/// chip on the edge nearest the main FAB, so the whole row is a single tap target. An item with no
/// <see cref="Text"/> collapses to a plain circle of <see cref="Size"/>.
/// </para>
/// </summary>
public class FabMenuItem : ContentView, ITapTarget
{
    const double DefaultSize = 44;
    const double DefaultIconSize = 20;
    const double DefaultFabSize = 56;

    // Gap between the pill edge and the icon chip. The chip is inset by this on every side, which
    // puts its centre exactly Size/2 from the pill edge - the same place the centre of a text-less
    // circular item sits. That is what lets ApplyAxis line every chip up on the main FAB's axis.
    const double ChipInset = 6;
    const double LabelPadding = 18;
    const double LabelSpacing = 10;

    readonly Border pill;
    readonly HorizontalStackLayout pillContent;
    readonly Label label;
    readonly Border iconChip;
    readonly Image iconImage;
    readonly TapGestureRecognizer tap;

    // Set by the owning FabMenu - the icon chip sits on the edge closest to the main FAB.
    bool isLeading;
    double axisSize = DefaultFabSize;


    public FabMenuItem()
    {
        label = new Label
        {
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.NoWrap,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false
        }.WithFontSize(ShinyThemeKeys.Type.BodySmallSize);

        iconImage = new Image
        {
            WidthRequest = DefaultIconSize,
            HeightRequest = DefaultIconSize,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Aspect = Aspect.AspectFit,
            IsVisible = false
        };

        iconChip = new Border
        {
            Padding = 0,
            StrokeThickness = 0,
            Stroke = Brush.Transparent,
            Content = iconImage,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        pillContent = new HorizontalStackLayout
        {
            Spacing = LabelSpacing,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        pillContent.Add(label);
        pillContent.Add(iconChip);

        pill = new Border
        {
            Padding = 0,
            StrokeShape = new RoundRectangle
            {
                CornerRadius = new CornerRadius(DefaultSize / 2)
            },
            Content = pillContent,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center
        }.WithStrokeThickness(ShinyThemeKeys.Border.Thin);

        // Theme defaults - overridden if the consumer sets the explicit color properties.
        pill.SetDynamicResource(Border.StrokeProperty, ShinyThemeKeys.Color.OutlineVariant);
        label.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurface);

        tap = new TapGestureRecognizer();
        tap.Tapped += OnTapped;
        pill.GestureRecognizers.Add(tap);

        Content = pill;
        HorizontalOptions = LayoutOptions.End;
        AnchorX = 1;
        AnchorY = 0.5;

        UpdateLayout();

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(FabMenuItem));
    }


    public static readonly BindableProperty IconProperty = BindableProperty.Create(
        nameof(Icon),
        typeof(ImageSource),
        typeof(FabMenuItem),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            var item = (FabMenuItem)b;
            item.iconImage.Source = n as ImageSource;
            item.iconImage.IsVisible = n is not null;
            item.UpdateLayout();
        }));
    public ImageSource? Icon
    {
        get => (ImageSource?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public static readonly BindableProperty TextProperty = BindableProperty.Create(
        nameof(Text),
        typeof(string),
        typeof(FabMenuItem),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            var item = (FabMenuItem)b;
            item.label.Text = n as string ?? string.Empty;
            item.label.IsVisible = !string.IsNullOrEmpty(item.label.Text);
            item.UpdateLayout();
        }));
    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command),
        typeof(ICommand),
        typeof(FabMenuItem),
        null);
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter),
        typeof(object),
        typeof(FabMenuItem),
        null);
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    /// <summary>Fill of the circular icon chip - and of the whole pill when the item has no <see cref="Text"/>.</summary>
    public static readonly BindableProperty FabBackgroundColorProperty = BindableProperty.Create(
        nameof(FabBackgroundColor),
        typeof(Color),
        typeof(FabMenuItem),
        null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            var item = (FabMenuItem)b;
            item.ApplyChipBackground();
            item.ApplyPillBackground();
        }));
    public Color? FabBackgroundColor
    {
        get => (Color?)GetValue(FabBackgroundColorProperty);
        set => SetValue(FabBackgroundColorProperty, value);
    }

    /// <summary>Outline stroke of the pill. Defaults to the theme outline-variant hairline.</summary>
    public static readonly BindableProperty BorderColorProperty = BindableProperty.Create(
        nameof(BorderColor),
        typeof(Color),
        typeof(FabMenuItem),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            var item = (FabMenuItem)b;
            if (n is Color c)
                item.pill.Stroke = c;
            else
                item.pill.SetDynamicResource(Border.StrokeProperty, ShinyThemeKeys.Color.OutlineVariant);
        }));
    public Color? BorderColor
    {
        get => (Color?)GetValue(BorderColorProperty);
        set => SetValue(BorderColorProperty, value);
    }

    /// <summary>Outline thickness of the pill. Set to 0 for a borderless pill.</summary>
    public static readonly BindableProperty BorderThicknessProperty = BindableProperty.Create(
        nameof(BorderThickness),
        typeof(double),
        typeof(FabMenuItem),
        1.0,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            ((FabMenuItem)b).pill.StrokeThickness = (double)n;
        }));
    public double BorderThickness
    {
        get => (double)GetValue(BorderThicknessProperty);
        set => SetValue(BorderThicknessProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(FabMenuItem),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            var item = (FabMenuItem)b;
            if (n is Color c)
                item.label.TextColor = c;
            else
                item.label.SetDynamicResource(Label.TextColorProperty, ShinyThemeKeys.Color.OnSurface);
        }));
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    /// <summary>Fill of the pill body behind the label. Defaults to the theme surface-container-high.</summary>
    public static readonly BindableProperty LabelBackgroundColorProperty = BindableProperty.Create(
        nameof(LabelBackgroundColor),
        typeof(Color),
        typeof(FabMenuItem),
        null,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            ((FabMenuItem)b).ApplyPillBackground();
        }));
    public Color? LabelBackgroundColor
    {
        get => (Color?)GetValue(LabelBackgroundColorProperty);
        set => SetValue(LabelBackgroundColorProperty, value);
    }

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(FabMenuItem),
        ThemeTokens.Unset,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            ((FabMenuItem)b).label.SetTokenOrValue(Label.FontSizeProperty, (double)n, ShinyThemeKeys.Type.BodySmallSize);
        }));
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly BindableProperty FontAttributesProperty = BindableProperty.Create(
        nameof(FontAttributes),
        typeof(FontAttributes),
        typeof(FabMenuItem),
        Microsoft.Maui.Controls.FontAttributes.None,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            ((FabMenuItem)b).label.FontAttributes = (FontAttributes)n;
        }));
    public FontAttributes FontAttributes
    {
        get => (FontAttributes)GetValue(FontAttributesProperty);
        set => SetValue(FontAttributesProperty, value);
    }

    /// <summary>Pill height - and the diameter when the item has no <see cref="Text"/>.</summary>
    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size),
        typeof(double),
        typeof(FabMenuItem),
        DefaultSize,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            ((FabMenuItem)b).UpdateLayout();
        }));
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public static readonly BindableProperty IconSizeProperty = BindableProperty.Create(
        nameof(IconSize),
        typeof(double),
        typeof(FabMenuItem),
        DefaultIconSize,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            var item = (FabMenuItem)b;
            var s = (double)n;
            item.iconImage.WidthRequest = s;
            item.iconImage.HeightRequest = s;
        }));
    public double IconSize
    {
        get => (double)GetValue(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    public static readonly BindableProperty HasShadowProperty = BindableProperty.Create(
        nameof(HasShadow),
        typeof(bool),
        typeof(FabMenuItem),
        true,
        propertyChanged: (b, _, _) => StyleGuard.WhenReady(b, typeof(FabMenuItem), () =>
        {
            ((FabMenuItem)b).ApplyShadow();
        }));
    public bool HasShadow
    {
        get => (bool)GetValue(HasShadowProperty);
        set => SetValue(HasShadowProperty, value);
    }


    public static readonly BindableProperty UseFeedbackProperty = BindableProperty.Create(
        nameof(UseFeedback),
        typeof(bool),
        typeof(FabMenuItem),
        true);
    public bool UseFeedback
    {
        get => (bool)GetValue(UseFeedbackProperty);
        set => SetValue(UseFeedbackProperty, value);
    }


    public event EventHandler? Clicked;


    void ITapTarget.Invoke() => Invoke();

    internal void Invoke()
    {
        if (UseFeedback)
            FeedbackHelper.Execute(this, nameof(Clicked));

        Clicked?.Invoke(this, EventArgs.Empty);
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
    }

    /// <summary>
    /// Called by the owning <see cref="FabMenu"/>. Puts the icon chip on the side nearest the main
    /// FAB and insets the pill so every chip centre lands on the main FAB's vertical axis.
    /// </summary>
    internal void ApplyAxis(double fabSize, bool leading)
    {
        var orderChanged = this.isLeading != leading;
        this.isLeading = leading;
        this.axisSize = fabSize;

        var options = leading ? LayoutOptions.Start : LayoutOptions.End;
        HorizontalOptions = options;
        pill.HorizontalOptions = options;
        AnchorX = leading ? 0 : 1;

        if (orderChanged)
        {
            pillContent.Clear();
            if (leading)
            {
                pillContent.Add(iconChip);
                pillContent.Add(label);
            }
            else
            {
                pillContent.Add(label);
                pillContent.Add(iconChip);
            }
        }
        UpdateLayout();
    }

    void OnTapped(object? sender, TappedEventArgs e) => Invoke();

    void UpdateLayout()
    {
        var hasText = label.IsVisible;
        var hasIcon = iconImage.IsVisible;
        var size = Size;
        var chip = hasText ? Math.Max(0, size - ChipInset * 2) : size;

        // An empty chip is just a coloured dot the caller never asked for.
        iconChip.IsVisible = hasIcon;
        iconChip.WidthRequest = chip;
        iconChip.HeightRequest = chip;
        iconChip.StrokeShape = new RoundRectangle
        {
            CornerRadius = new CornerRadius(chip / 2)
        };

        pill.HeightRequest = size;
        pill.MinimumWidthRequest = size;
        pill.StrokeShape = new RoundRectangle
        {
            CornerRadius = new CornerRadius(size / 2)
        };

        if (hasText)
        {
            pill.WidthRequest = -1;
            // The chip side gets the tight inset; with no chip the label is centred instead.
            pill.Padding = hasIcon
                ? (isLeading
                    ? new Thickness(ChipInset, 0, LabelPadding, 0)
                    : new Thickness(LabelPadding, 0, ChipInset, 0))
                : new Thickness(LabelPadding, 0);
            pillContent.Spacing = hasIcon ? LabelSpacing : 0;
        }
        else
        {
            pill.WidthRequest = size;
            pill.Padding = 0;
            pillContent.Spacing = 0;
        }

        // Line the chip centre up with the main FAB's axis.
        var inset = Math.Max(0, (axisSize - size) / 2);
        pill.Margin = isLeading
            ? new Thickness(inset, 0, 0, 0)
            : new Thickness(0, 0, inset, 0);

        ApplyPillBackground();
        ApplyChipBackground();
        ApplyShadow();
    }

    /// <summary>The pill carries the label fill; with no label it *is* the button, so it takes the chip fill.</summary>
    void ApplyPillBackground()
    {
        if (label.IsVisible)
        {
            if (LabelBackgroundColor is Color c)
                pill.BackgroundColor = c;
            else
                pill.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.SurfaceContainerHigh);
        }
        else
        {
            if (FabBackgroundColor is Color c)
                pill.BackgroundColor = c;
            else
                pill.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.Primary);
        }
    }

    void ApplyChipBackground()
    {
        if (!label.IsVisible)
        {
            // The pill is already the coloured circle - a second fill would just double-draw it.
            iconChip.BackgroundColor = Colors.Transparent;
        }
        else if (FabBackgroundColor is Color c)
        {
            iconChip.BackgroundColor = c;
        }
        else
        {
            iconChip.SetDynamicResource(VisualElement.BackgroundColorProperty, ShinyThemeKeys.Color.Primary);
        }
    }

    void ApplyShadow()
    {
        if (HasShadow)
            pill.SetDynamicResource(VisualElement.ShadowProperty, ShinyThemeKeys.Elevation.Level3);
        else
            pill.Shadow = null;
    }
}
