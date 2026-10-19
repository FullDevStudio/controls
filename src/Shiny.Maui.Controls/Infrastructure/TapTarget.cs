namespace Shiny.Maui.Controls.Infrastructure;

/// <summary>
/// A control drawn out of a <see cref="ContentView"/> and a tap gesture that is, to the user, a button.
/// </summary>
/// <remarks>
/// <para>
/// A tap gesture is all a sighted finger needs, but nothing else can see it: Windows UI Automation
/// offers no Invoke pattern, so Narrator, keyboard users and UI-automation tests cannot press it, and
/// no platform announces it as a button. <see cref="TapTargetHandler"/> gives these controls the
/// platform's button semantics, and routes every way of pressing them (a click from UI Automation,
/// TalkBack's double-tap, Enter or Space on Windows) to <see cref="Invoke"/>, the same path a tap takes.
/// </para>
/// </remarks>
interface ITapTarget
{
    /// <summary>Everything a tap does. Does nothing while the control is disabled.</summary>
    void Invoke();
}
