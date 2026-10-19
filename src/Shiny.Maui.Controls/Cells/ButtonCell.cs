using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Shiny.Maui.Controls.Infrastructure;
using Shiny.Maui.Controls.Themes;

namespace Shiny.Maui.Controls.Cells;

public class ButtonCell : CellBase
{
    Label buttonLabel = default!;

    public static readonly BindableProperty CommandProperty = BindableProperty.Create(
        nameof(Command), typeof(ICommand), typeof(ButtonCell), null);

    public static readonly BindableProperty CommandParameterProperty = BindableProperty.Create(
        nameof(CommandParameter), typeof(object), typeof(ButtonCell), null);

    public static readonly BindableProperty ButtonTextColorProperty = BindableProperty.Create(
        nameof(ButtonTextColor), typeof(Color), typeof(ButtonCell), null,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(ButtonCell), () =>
            {
                ((ButtonCell)b).UpdateButtonColor();
            }));

    public static readonly BindableProperty TitleAlignmentProperty = BindableProperty.Create(
        nameof(TitleAlignment), typeof(TextAlignment), typeof(ButtonCell), TextAlignment.Center,
        propertyChanged: (b, o, n) => StyleGuard.WhenReady(b, typeof(ButtonCell), () =>
            {
                ((ButtonCell)b).UpdateTitleAlignment();
            }));

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public Color? ButtonTextColor
    {
        get => (Color?)GetValue(ButtonTextColorProperty);
        set => SetValue(ButtonTextColorProperty, value);
    }

    public TextAlignment TitleAlignment
    {
        get => (TextAlignment)GetValue(TitleAlignmentProperty);
        set => SetValue(TitleAlignmentProperty, value);
    }

    public ButtonCell()
    {
        BuildButtonLayout();
        // The base title label is swapped out for this one, so it takes the title's id (and the
        // detached base label gives it up - this runs after CellBase's own link on every change).
        AutomationIds.Link(this, buttonLabel, TitleAutomationIdSuffix);
        AutomationIds.Track(this, () => AutomationIds.Set(TitleLabel, null));

        // Last line: replays any styled property that was applied before the
        // children existed. See StyleGuard.
        StyleGuard.MarkReady(this, typeof(ButtonCell));
    }

    void BuildButtonLayout()
    {
        buttonLabel = new Label
        {
            VerticalOptions = LayoutOptions.Center,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(16, 12),
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Fill
        };
        buttonLabel.SetBinding(Label.TextProperty, static (ButtonCell c) => c.Title, source: this);

        Content = buttonLabel;
    }

    protected override void ApplyAccentStyles() => this.UpdateButtonColor();

    void UpdateButtonColor()
    {
        Tint(buttonLabel, Label.TextColorProperty,
            ButtonTextColor ?? ParentTableView?.CellAccentColor, ShinyThemeKeys.Color.Primary);
    }

    void UpdateTitleAlignment()
    {
        buttonLabel.HorizontalTextAlignment = TitleAlignment;
    }

    protected override void OnTapped()
    {
        if (Command?.CanExecute(CommandParameter) == true)
            Command.Execute(CommandParameter);
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