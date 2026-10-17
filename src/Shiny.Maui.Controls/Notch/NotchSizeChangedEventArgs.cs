namespace Shiny.Maui.Controls;

/// <summary>Raised when a <see cref="NotchView"/> settles on a new size for its content.</summary>
public sealed class NotchSizeChangedEventArgs(Size oldSize, Size newSize) : EventArgs
{
    /// <summary>The size the notch was morphing from.</summary>
    public Size OldSize { get; } = oldSize;

    /// <summary>The size the notch is morphing to — the content plus padding, clamped to the min/max.</summary>
    public Size NewSize { get; } = newSize;
}
