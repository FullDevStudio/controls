namespace Shiny.Maui.Controls.Infrastructure;

/// <summary>
/// Tells a value the author wrote on the element itself apart from one a <see cref="Style"/> supplied.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="BindableObject.IsSet"/> cannot: it is true for a value from an implicit or explicit
/// style too, and MAUI keeps the specificity that would tell them apart internal. A control that
/// "leaves the author's own value alone" and checks <c>IsSet</c> therefore also leaves every
/// <em>styled</em> value alone — which is how a Style making every <see cref="ShinyButton"/> Filled
/// hid a <see cref="ButtonGroup"/>'s selection.
/// </para>
/// <para>
/// <see cref="BindableObject.ClearValue(BindableProperty)"/> removes only the local value, leaving a
/// style's (and a binding's) underneath, so clearing and comparing answers the question. A local value
/// equal to the one underneath it cannot be seen — and makes no difference to how anything looks.
/// </para>
/// </remarks>
static class LocalValue
{
    /// <summary>
    /// True when <paramref name="property"/> carries a local value that differs from what the element
    /// would show without it. The local value is put back, so the element is left as it was found.
    /// </summary>
    public static bool Has(BindableObject target, BindableProperty property)
    {
        if (!target.IsSet(property))
            return false;

        var local = target.GetValue(property);
        target.ClearValue(property);

        if (target.IsSet(property) && Equals(target.GetValue(property), local))
            return false;

        target.SetValue(property, local);
        return true;
    }
}
