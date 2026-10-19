using Shiny.Maui.Controls.ColorPicker.Internal;
using MauiSlider = Microsoft.Maui.Controls.Slider;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.ColorPicker;

public partial class ColorPicker : ContentView
{
    /// <summary>Appended to the picker's <see cref="Element.AutomationId"/> to name the saturation/brightness spectrum.</summary>
    public const string SpectrumAutomationIdSuffix = "_Spectrum";

    /// <summary>Appended to the picker's <see cref="Element.AutomationId"/> to name the hue bar.</summary>
    public const string HueAutomationIdSuffix = "_Hue";

    /// <summary>Appended to the picker's <see cref="Element.AutomationId"/> to name the opacity slider.</summary>
    public const string OpacityAutomationIdSuffix = "_Opacity";

    /// <summary>Appended to the picker's <see cref="Element.AutomationId"/> to name the swatch previewing the colour.</summary>
    public const string PreviewAutomationIdSuffix = "_Preview";

    /// <summary>Appended to the picker's <see cref="Element.AutomationId"/> to name the hex entry.</summary>
    public const string HexAutomationIdSuffix = "_Hex";

    readonly ColorSpectrumView spectrumView;
    readonly HueBarView hueBar;
    readonly MauiSlider opacitySlider;
    readonly BoxView opacityTrack;
    readonly Border previewSwatch;
    readonly Entry hexEntry;
    readonly Grid rootGrid;

    bool isUpdating;

    public ColorPicker()
    {
        spectrumView = new ColorSpectrumView
        {
            HeightRequest = 200,
            HorizontalOptions = LayoutOptions.Fill
        };
        spectrumView.ColorPicked += OnSpectrumColorPicked;

        hueBar = new HueBarView
        {
            HeightRequest = 30,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(0, 8, 0, 0)
        };
        hueBar.HueChanged += OnHueChanged;

        // Opacity slider
        opacityTrack = new BoxView
        {
            HeightRequest = 30,
            CornerRadius = 6,
            HorizontalOptions = LayoutOptions.Fill,
            Background = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0.5),
                EndPoint = new Point(1, 0.5),
                GradientStops =
                {
                    new GradientStop(Colors.Transparent, 0),
                    new GradientStop(Colors.Black, 1)
                }
            }
        };

        opacitySlider = new MauiSlider
        {
            Minimum = 0,
            Maximum = 1,
            Value = 1,
            MinimumTrackColor = Colors.Transparent,
            MaximumTrackColor = Colors.Transparent,
            ThumbColor = Colors.White,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(0, 8, 0, 0)
        };
        opacitySlider.ValueChanged += OnOpacityChanged;

        var opacityRow = new Grid
        {
            HeightRequest = 30,
            Margin = new Thickness(0, 8, 0, 0),
            Children = { opacityTrack, opacitySlider }
        };

        previewSwatch = new Border
        {
            WidthRequest = 44,
            HeightRequest = 44,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle().WithCornerRadius(ShinyThemeKeys.Shape.CornerSmallRadius),
            BackgroundColor = Colors.Black,
            VerticalOptions = LayoutOptions.Center
        }.WithStrokeThickness(ShinyThemeKeys.Border.Medium);

        // Only the swatch's frame is themed - its fill is the colour being picked.
        previewSwatch.SetDynamicResource(Border.StrokeProperty, ShinyThemeKeys.Color.OutlineVariant);

        hexEntry = new Entry
        {
            FontFamily = "monospace",
            MaxLength = 9, // #AARRGGBB
            Placeholder = "#000000",
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Fill
        }.WithFontSize(ShinyThemeKeys.Type.BodyMediumSize);
        hexEntry.Completed += OnHexEntryCompleted;
        hexEntry.Unfocused += OnHexEntryUnfocused;

        var bottomRow = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 12,
            Margin = new Thickness(0, 8, 0, 0)
        };
        bottomRow.Add(previewSwatch, 0, 0);
        bottomRow.Add(hexEntry, 1, 0);

        rootGrid = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            Padding = new Thickness(12),

            // The spectrum, the hue bar and the opacity track are all Fill with a HeightRequest and no
            // width of their own - a GraphicsView has no content to measure, so each one asks for zero.
            // In a container that stretches (a page, a Fill cell) that is exactly right. In one that
            // shrink-wraps its content - ColorPickerButton's popup Border is HorizontalOptions.Center -
            // the only child that measures is the bottom row, and the whole picker came out the width of
            // a hex Entry with the spectrum squeezed to nothing. A minimum, not a WidthRequest, so the
            // picker still fills a wider host. 320 is the same panel width the Blazor picker uses.
            MinimumWidthRequest = 320
        };
        rootGrid.Add(spectrumView, 0, 0);
        rootGrid.Add(hueBar, 0, 1);
        rootGrid.Add(opacityRow, 0, 2);
        rootGrid.Add(bottomRow, 0, 3);

        Content = rootGrid;

        AutomationIds.Link(this, spectrumView, SpectrumAutomationIdSuffix);
        AutomationIds.Link(this, hueBar, HueAutomationIdSuffix);
        AutomationIds.Link(this, opacitySlider, OpacityAutomationIdSuffix);
        AutomationIds.Link(this, previewSwatch, PreviewAutomationIdSuffix);
        AutomationIds.Link(this, hexEntry, HexAutomationIdSuffix);

        UpdateFromColor(SelectedColor);

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(ColorPicker));
    }

    void OnSpectrumColorPicked(float saturation, float brightness)
    {
        if (isUpdating) return;
        isUpdating = true;

        var color = FromHsv(hueBar.Hue, saturation, brightness, (float)opacitySlider.Value);
        SetAndNotify(color);

        isUpdating = false;
    }

    void OnHueChanged(float hue)
    {
        if (isUpdating) return;
        isUpdating = true;

        spectrumView.Hue = hue;
        var (_, s, b) = spectrumView.GetCurrentSaturationBrightness();
        var color = FromHsv(hue, s, b, (float)opacitySlider.Value);
        SetAndNotify(color);

        isUpdating = false;
    }

    void OnOpacityChanged(object? sender, ValueChangedEventArgs e)
    {
        if (isUpdating) return;
        isUpdating = true;

        var (_, s, b) = spectrumView.GetCurrentSaturationBrightness();
        var color = FromHsv(hueBar.Hue, s, b, (float)e.NewValue);
        SetAndNotify(color);

        isUpdating = false;
    }

    void OnHexEntryCompleted(object? sender, EventArgs e) => ApplyHexInput();
    void OnHexEntryUnfocused(object? sender, FocusEventArgs e) => ApplyHexInput();

    void ApplyHexInput()
    {
        if (isUpdating) return;

        var text = hexEntry.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (!text.StartsWith('#'))
            text = "#" + text;

        try
        {
            var color = Color.FromArgb(text);
            SelectedColor = color;
        }
        catch
        {
            // Invalid input — revert display
            UpdateHexDisplay(SelectedColor);
        }
    }

    void SetAndNotify(Color color)
    {
        SetValue(SelectedColorProperty, color);
        UpdatePreview(color);
        UpdateHexDisplay(color);
        UpdateOpacityTrack(color);

        // Fire events directly — OnSelectedColorChanged won't fire them
        // because isUpdating is true when this is called from internal handlers
        ColorChanged?.Invoke(this, color);
        if (ColorChangedCommand?.CanExecute(color) == true)
            ColorChangedCommand.Execute(color);
    }

    void UpdateFromColor(Color color)
    {
        isUpdating = true;

        color.ToRgba(out var r, out var g, out var b, out var a);
        ToHsv(r, g, b, out var h, out var s, out var v);

        hueBar.Hue = h;
        spectrumView.Hue = h;
        spectrumView.SetSaturationBrightness(s, v);
        opacitySlider.Value = a;

        UpdatePreview(color);
        UpdateHexDisplay(color);
        UpdateOpacityTrack(color);

        isUpdating = false;
    }

    void UpdatePreview(Color color)
    {
        previewSwatch.BackgroundColor = color;
    }

    void UpdateHexDisplay(Color color)
    {
        var r = (int)(color.Red * 255);
        var g = (int)(color.Green * 255);
        var b = (int)(color.Blue * 255);
        var a = (int)(color.Alpha * 255);

        hexEntry.Text = a < 255
            ? $"#{a:X2}{r:X2}{g:X2}{b:X2}"
            : $"#{r:X2}{g:X2}{b:X2}";
    }

    void UpdateOpacityTrack(Color color)
    {
        var opaque = new Color(color.Red, color.Green, color.Blue, 1f);
        opacityTrack.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0.5),
            EndPoint = new Point(1, 0.5),
            GradientStops =
            {
                new GradientStop(Colors.Transparent, 0),
                new GradientStop(opaque, 1)
            }
        };
    }

    static Color FromHsv(float h, float s, float v, float a)
    {
        var c = v * s;
        var x = c * (1 - MathF.Abs((h / 60f) % 2 - 1));
        var m = v - c;

        float r, g, b;
        switch ((int)(h / 60) % 6)
        {
            case 0: r = c; g = x; b = 0; break;
            case 1: r = x; g = c; b = 0; break;
            case 2: r = 0; g = c; b = x; break;
            case 3: r = 0; g = x; b = c; break;
            case 4: r = x; g = 0; b = c; break;
            default: r = c; g = 0; b = x; break;
        }

        return new Color(r + m, g + m, b + m, a);
    }

    static void ToHsv(float r, float g, float b, out float h, out float s, out float v)
    {
        var max = MathF.Max(r, MathF.Max(g, b));
        var min = MathF.Min(r, MathF.Min(g, b));
        var delta = max - min;

        v = max;
        s = max == 0 ? 0 : delta / max;

        if (delta == 0)
        {
            h = 0;
        }
        else if (max == r)
        {
            h = 60 * (((g - b) / delta) % 6);
        }
        else if (max == g)
        {
            h = 60 * (((b - r) / delta) + 2);
        }
        else
        {
            h = 60 * (((r - g) / delta) + 4);
        }

        if (h < 0) h += 360;
    }
}
