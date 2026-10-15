namespace Shiny.Maui.Controls;

/// <summary>The outline an <see cref="Avatar"/> is clipped to.</summary>
public enum AvatarShape
{
    /// <summary>A circle - the default.</summary>
    Circle,

    /// <summary>A square with corners rounded to a quarter of its size.</summary>
    Rounded,

    /// <summary>A plain square.</summary>
    Square
}


/// <summary>The presence dot drawn on an <see cref="Avatar"/>'s bottom-right corner.</summary>
public enum AvatarStatus
{
    /// <summary>No dot.</summary>
    None,

    /// <summary>Green - available.</summary>
    Online,

    /// <summary>Amber - idle or away.</summary>
    Away,

    /// <summary>Red - busy or do not disturb.</summary>
    Busy,

    /// <summary>Grey - offline.</summary>
    Offline
}
