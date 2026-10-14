namespace Shiny.Blazor.Controls;

/// <summary>
/// One button in a <see cref="ModalView"/>'s footer, rendered as a <see cref="ShinyButton"/> so it
/// picks up the active theme.
/// </summary>
/// <remarks>
/// The footer is the part of a modal that is nearly always the same shape - a row of actions on the
/// right, cancel first - so it is a list rather than markup. Reach for
/// <see cref="ModalView.FooterTemplate"/> when the footer is doing something else entirely.
/// <para>
/// Mutating a button in place (its <see cref="State"/>, say) does not repaint on its own; the modal
/// re-renders when the click handler that changed it returns, and any other change wants a
/// <c>StateHasChanged</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// Buttons =
/// [
///     new ModalButton("Cancel") { Appearance = ButtonAppearance.Text, Type = ButtonType.Secondary },
///     new ModalButton("Save") { ClosesModal = false, OnClick = SaveAsync }
/// ]
/// </code>
/// </example>
public sealed class ModalButton
{
    public ModalButton()
    {
    }

    public ModalButton(string text) => this.Text = text;

    /// <summary>The label.</summary>
    public string? Text { get; set; }

    /// <summary>What the action means, mapped onto the theme's semantic colours.</summary>
    public ButtonType Type { get; set; } = ButtonType.Primary;

    /// <summary>How much of the button the surface paints.</summary>
    public ButtonAppearance Appearance { get; set; } = ButtonAppearance.Filled;

    /// <summary>Inline SVG or an icon glyph shown before the text.</summary>
    public string? Icon { get; set; }

    /// <summary>Greys the button out and stops it responding.</summary>
    public bool Disabled { get; set; }

    /// <summary>
    /// Drives the button's busy/success/error affordance. Set it to <see cref="ButtonState.Busy"/>
    /// from an <see cref="OnClick"/> that saves, and pair it with <see cref="ClosesModal"/> false so
    /// the modal stays up while the work runs.
    /// </summary>
    public ButtonState State { get; set; } = ButtonState.Normal;

    /// <summary>
    /// Closes the modal after <see cref="OnClick"/> completes, with
    /// <see cref="ModalCloseReason.Button"/>. On by default: a footer button that leaves the modal up
    /// is the exception, not the rule. <see cref="ModalView.Closing"/> can still veto it.
    /// </summary>
    public bool ClosesModal { get; set; } = true;

    /// <summary>Runs before the modal closes, and is awaited.</summary>
    /// <remarks>
    /// Invoked like an <c>EventCallback</c>: when the delegate belongs to a component (a method group
    /// such as <c>OnClick = SaveAsync</c>, or a lambda that captures only <c>this</c>), that component
    /// re-renders when it completes — so a handler that sets the page's bound <c>IsOpen</c> to false
    /// closes the modal. A lambda that captures locals has no component to re-render; call
    /// <c>StateHasChanged</c> yourself from one of those.
    /// </remarks>
    public Func<Task>? OnClick { get; set; }

    /// <summary>Extra classes for this button.</summary>
    public string? CssClass { get; set; }

    /// <summary>
    /// Anything you want to get back in a handler - the button's own meaning to the page, when one
    /// delegate is servicing several of them.
    /// </summary>
    public object? Tag { get; set; }
}
