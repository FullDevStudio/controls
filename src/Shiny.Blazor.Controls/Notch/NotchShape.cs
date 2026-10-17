namespace Shiny.Blazor.Controls;

/// <summary>The outline a <see cref="NotchView"/> draws.</summary>
public enum NotchShape
{
    /// <summary>A free-floating pill with every corner rounded — the Dynamic Island look.</summary>
    Island,

    /// <summary>Square top corners and rounded bottom ones, for a notch flush with the top edge.</summary>
    Notch
}

/// <summary>The size a <see cref="NotchView"/> is morphing to: its content plus padding, clamped to the min/max.</summary>
public readonly record struct NotchSize(double Width, double Height);
