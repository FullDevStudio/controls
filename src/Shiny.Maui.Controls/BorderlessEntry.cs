using Microsoft.Maui.Controls;

namespace Shiny.Maui.Controls;

class BorderlessEntry : Entry
{
    /// <summary>
    /// Turns the platform's autofill/suggestion machinery off. MAUI's own
    /// <see cref="InputView.IsSpellCheckEnabled"/> and <see cref="Entry.IsTextPredictionEnabled"/>
    /// do not cover autofill (iOS <c>TextContentType</c>, Android autofill hints), which is what
    /// actually overwrites a half-typed serial with a saved address.
    /// </summary>
    public static readonly BindableProperty IsAutoCompleteEnabledProperty = BindableProperty.Create(
        nameof(IsAutoCompleteEnabled), typeof(bool), typeof(BorderlessEntry), true,
        propertyChanged: (b, _, _) => ((BorderlessEntry)b).Handler?.UpdateValue(AutoCompleteMapperKey));

    public bool IsAutoCompleteEnabled
    {
        get => (bool)GetValue(IsAutoCompleteEnabledProperty);
        set => SetValue(IsAutoCompleteEnabledProperty, value);
    }

    internal const string AutoCompleteMapperKey = "ShinyAutoComplete";

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        BorderlessEntryPolish.Apply(this.Handler?.PlatformView);
    }
}
