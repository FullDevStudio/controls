using CoreAlignment = Microsoft.Maui.Primitives.LayoutAlignment;

namespace Shiny.Maui.Controls.Infrastructure;

/// <summary>
/// A default for <see cref="View.HorizontalOptions"/> / <see cref="View.VerticalOptions"/> that a
/// <see cref="Style"/> can still override.
/// </summary>
/// <remarks>
/// <para>
/// MAUI has no per-type default for those two properties, and the obvious substitute - assigning them
/// in the constructor - writes a <em>local</em> value. A local value outranks every Style, implicit or
/// explicit, so a Style centring the control silently never applied.
/// </para>
/// <para>
/// Layouts never read <c>HorizontalOptions</c> itself: they read <see cref="IView.HorizontalLayoutAlignment"/>,
/// which <see cref="View"/> implements explicitly. A control re-implements <see cref="IView"/> and answers
/// with <see cref="Resolve"/>: whatever was set - locally, by a Style, by a binding - wins, and only a
/// property nobody set falls back to the control's own default.
/// </para>
/// </remarks>
static class DefaultAlignment
{
    public static CoreAlignment Resolve(View view, BindableProperty property, CoreAlignment fallback)
    {
        if (!view.IsSet(property))
            return fallback;

        return ((LayoutOptions)view.GetValue(property)).Alignment switch
        {
            LayoutAlignment.Start => CoreAlignment.Start,
            LayoutAlignment.Center => CoreAlignment.Center,
            LayoutAlignment.End => CoreAlignment.End,
            _ => CoreAlignment.Fill
        };
    }
}
