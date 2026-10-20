namespace Shiny.Maui.Controls.Infrastructure;

/// <summary>
/// Names the parts of a control after the control, so a UI test (Appium, UI Automation, DevFlow) or an
/// accessibility tool that knows the control's <see cref="Element.AutomationId"/> can reach the entry,
/// button or chip inside it.
/// </summary>
/// <remarks>
/// <para>
/// Every part is named <c>&lt;owner id&gt;&lt;suffix&gt;</c>, with the suffix a public constant on the
/// control (<c>Expander.HeaderAutomationIdSuffix</c>, <c>TextEntry.EntryAutomationIdSuffix</c>, …), and a
/// part repeated per item adds its index: <c>&lt;owner id&gt;&lt;suffix&gt;_&lt;index&gt;</c>. A control
/// without an id of its own gives its parts none, except where an earlier release shipped a fixed id
/// (the Wizard's <c>WizardNextButton</c>): that stays as the fallback so existing tests keep finding it.
/// </para>
/// <para>
/// Everything goes through <see cref="BindableObject.SetValue(BindableProperty, object)"/>, never the
/// <see cref="Element.AutomationId"/> setter. The setter throws the second time it is used, and a part's
/// id changes whenever its owner's does — a templated row re-bound by a list rebuild, say. That was the
/// Expander crash, and a source scan in the tests keeps the setter out of the controls.
/// </para>
/// </remarks>
static class AutomationIds
{
    /// <summary>The id <paramref name="owner"/>'s part with <paramref name="suffix"/> should carry, or null.</summary>
    public static string? For(Element owner, string suffix)
        => String.IsNullOrEmpty(owner.AutomationId) ? null : owner.AutomationId + suffix;

    /// <summary>The id of the <paramref name="index"/>th of a repeated part, or null.</summary>
    public static string? For(Element owner, string suffix, int index)
        => String.IsNullOrEmpty(owner.AutomationId) ? null : $"{owner.AutomationId}{suffix}_{index}";

    /// <summary>Gives <paramref name="target"/> <paramref name="id"/>, or takes its id away when null. Safe to repeat.</summary>
    public static void Set(Element target, string? id)
    {
        if (String.IsNullOrEmpty(id))
            target.ClearValue(Element.AutomationIdProperty);
        else if (target.AutomationId != id)
            target.SetValue(Element.AutomationIdProperty, id);
    }

    /// <summary>
    /// Names <paramref name="part"/> after <paramref name="owner"/> now and every time the owner's id
    /// changes. <paramref name="fallback"/> is used while the owner has none.
    /// </summary>
    public static void Link(Element owner, Element part, string suffix, string? fallback = null)
        => Track(owner, () => Set(part, For(owner, suffix) ?? fallback));

    /// <summary>
    /// Runs <paramref name="apply"/> now and every time <paramref name="owner"/>'s id changes. For parts
    /// that come and go (generated items, a swapped template), where <see cref="Link"/> cannot hold them.
    /// </summary>
    /// <returns>
    /// The subscription. A control tracking its own id can ignore it: the handler lives and dies with the
    /// control. Anything tracking <em>another</em> element's id must dispose it, or that element keeps it
    /// (and everything <paramref name="apply"/> captures) alive for as long as it lives itself.
    /// </returns>
    public static IDisposable Track(Element owner, Action apply)
    {
        apply();
        return new Tracking(owner, apply);
    }

    sealed class Tracking : IDisposable
    {
        Element? owner;
        readonly Action apply;

        public Tracking(Element owner, Action apply)
        {
            this.owner = owner;
            this.apply = apply;
            owner.PropertyChanged += this.OnPropertyChanged;
        }

        void OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == Element.AutomationIdProperty.PropertyName)
                this.apply();
        }

        public void Dispose()
        {
            if (this.owner is { } o)
                o.PropertyChanged -= this.OnPropertyChanged;
            this.owner = null;
        }
    }
}
