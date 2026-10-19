using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using TvTableView = Shiny.Maui.Controls.TableView;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.Cells;

public class CheckboxCell : CellBase
{
    /// <summary>Appended to the cell's <see cref="Element.AutomationId"/> to name its check box.</summary>
    public const string CheckBoxAutomationIdSuffix = "_CheckBox";

    /// <summary>
    /// Children are built by CellBase's constructor (BuildLayout -> the virtual
    /// CreateAccessoryView override above), so by the time this body runs they exist.
    /// Marking ready here replays any property an implicit Style applied before
    /// construction - see StyleGuard.
    /// </summary>
    public CheckboxCell()
    {
        AutomationIds.Link(this, checkBox, CheckBoxAutomationIdSuffix);
        StyleGuard.MarkReady(this, typeof(CheckboxCell));
    }

    CheckBox checkBox = default!;

    public static readonly BindableProperty CheckedProperty = BindableProperty.Create(
        nameof(Checked), typeof(bool), typeof(CheckboxCell), false,
        BindingMode.TwoWay,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(CheckboxCell), () =>
            {
                ((CheckboxCell)b).checkBox.IsChecked = (bool)n;
            }));

    public static readonly BindableProperty AccentColorProperty = BindableProperty.Create(
        nameof(AccentColor), typeof(Color), typeof(CheckboxCell), null,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(CheckboxCell), () =>
            {
                ((CheckboxCell)b).UpdateCheckBoxColor();
            }));

    public bool Checked
    {
        get => (bool)GetValue(CheckedProperty);
        set => SetValue(CheckedProperty, value);
    }

    public Color? AccentColor
    {
        get => (Color?)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    protected override View? CreateAccessoryView()
    {
        checkBox = new CheckBox
        {
            VerticalOptions = LayoutOptions.Center
        };
        checkBox.CheckedChanged += (s, e) => Checked = e.Value;
        return checkBox;
    }

    protected override void ApplyAccentStyles() => this.UpdateCheckBoxColor();

    void UpdateCheckBoxColor()
        => Tint(checkBox, CheckBox.ColorProperty, AccentColor ?? ParentTableView?.CellAccentColor, ShinyThemeKeys.Color.Primary);

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


    protected override void OnTapped()
    {
        checkBox.IsChecked = !checkBox.IsChecked;
    }
}