namespace Shiny.Controls.Media;

/// <summary>Which way VideoText's text scrolls across the control, if at all.</summary>
public enum MarqueeDirection
{
    /// <summary>The text stays centred.</summary>
    None,

    /// <summary>The text enters from the right edge and leaves through the left.</summary>
    Left,

    /// <summary>The text enters from the left edge and leaves through the right.</summary>
    Right,

    /// <summary>The text enters from the bottom edge and leaves through the top.</summary>
    Up,

    /// <summary>The text enters from the top edge and leaves through the bottom.</summary>
    Down
}
