using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.Cells;

public class SimpleCheckCell : CellBase
{
    /// <summary>Appended to the cell's <see cref="Element.AutomationId"/> to name the checkmark, visible only while checked.</summary>
    public const string CheckAutomationIdSuffix = "_Check";

    /// <summary>
    /// Children are built by CellBase's constructor (BuildLayout -> the virtual
    /// CreateAccessoryView override above), so by the time this body runs they exist.
    /// Marking ready here replays any property an implicit Style applied before
    /// construction - see StyleGuard.
    /// </summary>
    public SimpleCheckCell()
    {
        AutomationIds.Link(this, checkLabel, CheckAutomationIdSuffix);
        StyleGuard.MarkReady(this, typeof(SimpleCheckCell));
    }

    Label checkLabel = default!;

    public static readonly BindableProperty CheckedProperty = BindableProperty.Create(
        nameof(Checked), typeof(bool), typeof(SimpleCheckCell), false,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(SimpleCheckCell), () =>
            {
                ((SimpleCheckCell)b).UpdateCheckVisibility();
            }));

    public static readonly BindableProperty ValueProperty = BindableProperty.Create(
        nameof(Value), typeof(object), typeof(SimpleCheckCell), null);

    public static readonly BindableProperty AccentColorProperty = BindableProperty.Create(
        nameof(AccentColor), typeof(Color), typeof(SimpleCheckCell), null,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(SimpleCheckCell), () =>
            {
                ((SimpleCheckCell)b).UpdateCheckColor();
            }));

    public bool Checked
    {
        get => (bool)GetValue(CheckedProperty);
        set => SetValue(CheckedProperty, value);
    }

    public object? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public Color? AccentColor
    {
        get => (Color?)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    protected override View? CreateAccessoryView()
    {
        checkLabel = new Label
        {
            Text = "\u2713",
            FontSize = 20,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Center,
            IsVisible = false
        };
        return checkLabel;
    }

    void UpdateCheckVisibility()
    {
        checkLabel.IsVisible = Checked;
    }

    protected override void ApplyAccentStyles() => this.UpdateCheckColor();

    void UpdateCheckColor()
    {
        Tint(checkLabel, Label.TextColorProperty,
            AccentColor ?? ParentTableView?.CellAccentColor, ShinyThemeKeys.Color.Primary);
    }

    protected override void OnTapped()
    {
        Checked = !Checked;
    }

    /// <summary>Uses the explicit colour when one was supplied, otherwise binds to the theme token.</summary>
    static void Tint(Element target, BindableProperty property, Color? explicitColor, string themeKey)
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
}