using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;
namespace Shiny.Maui.Controls;

public class CountryPicker : ContentView
{
    /// <summary>Appended to the picker's <see cref="Element.AutomationId"/> to name the search entry.</summary>
    public const string EntryAutomationIdSuffix = AutoCompleteEntry.EntryAutomationIdSuffix;

    /// <summary>Names the floating list of matching countries.</summary>
    public const string DropDownAutomationIdSuffix = AutoCompleteEntry.DropDownAutomationIdSuffix;

    /// <summary>Names each country in the list by its ISO 3166 alpha-2 code: <c>&lt;id&gt;_Suggestion_CA</c>.</summary>
    public const string SuggestionAutomationIdSuffix = AutoCompleteEntry.SuggestionAutomationIdSuffix;

    /// <summary>Names the flag of the selected country.</summary>
    public const string FlagAutomationIdSuffix = "_Flag";

    static readonly bool showFlags = DeviceInfo.Platform != DevicePlatform.WinUI;

    readonly AutoCompleteEntry autoComplete;
    readonly Label selectedFlagLabel;
    readonly Grid rootGrid;

    public CountryPicker()
    {
        autoComplete = new AutoCompleteEntry
        {
            Placeholder = "Search countries...",
            ItemsSource = CountryData.All.ToList(),
            TextMemberPath = nameof(Country.Name),
            ShowAllOnFocus = true,
            HorizontalOptions = LayoutOptions.Fill,
            ItemTemplate = new DataTemplate(() =>
            {
                var grid = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Auto),
                        new ColumnDefinition(GridLength.Star)
                    },
                    ColumnSpacing = 8,
                    Padding = new Thickness(10, 8)
                };

                var flag = new Label
                {
                    VerticalTextAlignment = TextAlignment.Center,
                    IsVisible = showFlags
                }.WithFontSize(ShinyThemeKeys.Type.TitleLargeSize);
                flag.SetBinding(Label.TextProperty, static (Country c) => c.FlagEmoji);

                var name = new Label
                {
                    VerticalTextAlignment = TextAlignment.Center}.WithFontSize(ShinyThemeKeys.Type.BodyMediumSize);
                name.SetBinding(Label.TextProperty, static (Country c) => c.Name);

                grid.Add(flag, 0, 0);
                grid.Add(name, 1, 0);
                return grid;
            })
        };
        autoComplete.ItemSelected += OnItemSelected;
        // Keyed by code rather than index, so a test picks the same country whatever was typed.
        autoComplete.SuggestionKey = static item => (item as Country)?.Code2;

        selectedFlagLabel = new Label
        {
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center,
            WidthRequest = showFlags ? 32 : 0,
            IsVisible = false
        }.WithFontSize(ShinyThemeKeys.Type.HeadlineSmallSize);

        rootGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(showFlags ? new GridLength(36) : new GridLength(0)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = showFlags ? 4 : 0
        };
        rootGrid.Add(selectedFlagLabel, 0, 0);
        rootGrid.Add(autoComplete, 1, 0);

        Content = rootGrid;

        // The inner field's parts are named after this control, not after a second id of its own.
        AutomationIds.Track(this, () => autoComplete.NamePartsAfter(AutomationId));
        AutomationIds.Link(this, selectedFlagLabel, FlagAutomationIdSuffix);

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(CountryPicker));
    }

    public static readonly BindableProperty SelectedCountryProperty = BindableProperty.Create(
        nameof(SelectedCountry),
        typeof(Country),
        typeof(CountryPicker),
        null,
        BindingMode.TwoWay,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(CountryPicker), () =>
            {
                ((CountryPicker)b).OnSelectedCountryChanged(n as Country);
            }));
    public Country? SelectedCountry
    {
        get => (Country?)GetValue(SelectedCountryProperty);
        set => SetValue(SelectedCountryProperty, value);
    }

    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder),
        typeof(string),
        typeof(CountryPicker),
        "Search countries...",
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(CountryPicker), () =>
            {
                ((CountryPicker)b).autoComplete.Placeholder = (string)n;
            }));
    public string Placeholder
    {
        get => (string)GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(
        nameof(TextColor),
        typeof(Color),
        typeof(CountryPicker),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(CountryPicker), () =>
            {
                ((CountryPicker)b).autoComplete.TextColor = n as Color;
            }));
    public Color? TextColor
    {
        get => (Color?)GetValue(TextColorProperty);
        set => SetValue(TextColorProperty, value);
    }

    public static readonly BindableProperty PlaceholderColorProperty = BindableProperty.Create(
        nameof(PlaceholderColor),
        typeof(Color),
        typeof(CountryPicker),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(CountryPicker), () =>
            {
                ((CountryPicker)b).autoComplete.PlaceholderColor = n as Color;
            }));
    public Color? PlaceholderColor
    {
        get => (Color?)GetValue(PlaceholderColorProperty);
        set => SetValue(PlaceholderColorProperty, value);
    }

    public static readonly BindableProperty DropDownBackgroundColorProperty = BindableProperty.Create(
        nameof(DropDownBackgroundColor),
        typeof(Color),
        typeof(CountryPicker),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(CountryPicker), () =>
            { if (n is Color c) ((CountryPicker)b).autoComplete.DropDownBackgroundColor = c; }));
    public Color? DropDownBackgroundColor
    {
        get => (Color?)GetValue(DropDownBackgroundColorProperty);
        set => SetValue(DropDownBackgroundColorProperty, value);
    }

    public static readonly BindableProperty DropDownBorderColorProperty = BindableProperty.Create(
        nameof(DropDownBorderColor),
        typeof(Color),
        typeof(CountryPicker),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(CountryPicker), () =>
            { if (n is Color c) ((CountryPicker)b).autoComplete.DropDownBorderColor = c; }));
    public Color? DropDownBorderColor
    {
        get => (Color?)GetValue(DropDownBorderColorProperty);
        set => SetValue(DropDownBorderColorProperty, value);
    }

    public static readonly BindableProperty SpinnerColorProperty = BindableProperty.Create(
        nameof(SpinnerColor),
        typeof(Color),
        typeof(CountryPicker),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(CountryPicker), () =>
            { if (n is Color c) ((CountryPicker)b).autoComplete.SpinnerColor = c; }));
    public Color? SpinnerColor
    {
        get => (Color?)GetValue(SpinnerColorProperty);
        set => SetValue(SpinnerColorProperty, value);
    }

    public static readonly BindableProperty MaxDropDownHeightProperty = BindableProperty.Create(
        nameof(MaxDropDownHeight),
        typeof(double),
        typeof(CountryPicker),
        200d,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(CountryPicker), () =>
            {
                ((CountryPicker)b).autoComplete.MaxDropDownHeight = (double)n;
            }));
    public double MaxDropDownHeight
    {
        get => (double)GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    public static readonly BindableProperty FontSizeProperty = BindableProperty.Create(
        nameof(FontSize),
        typeof(double),
        typeof(CountryPicker),
        14d,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(CountryPicker), () =>
            {
                ((CountryPicker)b).autoComplete.FontSize = (double)n;
            }));
    public double FontSize
    {
        get => (double)GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(
        nameof(FontFamily),
        typeof(string),
        typeof(CountryPicker),
        null,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(CountryPicker), () =>
            {
                ((CountryPicker)b).autoComplete.FontFamily = n as string;
            }));
    public string? FontFamily
    {
        get => (string?)GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public static readonly BindableProperty CornerRadiusProperty = BindableProperty.Create(
        nameof(CornerRadius),
        typeof(double),
        typeof(CountryPicker),
        4d,
        propertyChanged: (b, _, n) => StyleGuard.WhenReady(b, typeof(CountryPicker), () =>
            {
                ((CountryPicker)b).autoComplete.CornerRadius = (double)n;
            }));
    public double CornerRadius
    {
        get => (double)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public event EventHandler<Country>? CountrySelected;

    void OnItemSelected(object? sender, object? e)
    {
        if (e is Country country)
        {
            SelectedCountry = country;
            CountrySelected?.Invoke(this, country);
        }
    }

    void OnSelectedCountryChanged(Country? country)
    {
        if (country != null)
        {
            autoComplete.Text = country.Name;
            autoComplete.SelectedItem = country;
            if (showFlags)
            {
                selectedFlagLabel.Text = country.FlagEmoji;
                selectedFlagLabel.IsVisible = true;
            }
        }
        else
        {
            autoComplete.Text = string.Empty;
            autoComplete.SelectedItem = null;
            selectedFlagLabel.IsVisible = false;
        }
    }
}
