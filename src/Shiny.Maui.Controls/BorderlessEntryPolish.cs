namespace Shiny.Maui.Controls;

/// <summary>
/// Lets a platform package strip native chrome off a <see cref="BorderlessEntry"/>.
/// </summary>
/// <remarks>
/// <see cref="BorderlessEntry"/> covers Android, iOS, Mac Catalyst and Windows through handler mappers
/// registered by <c>UseShinyControls</c>. The macOS AppKit and Linux GTK4 heads ship their own handler
/// types, so those mappers never run there — and those two heads only exist in the
/// <c>Shiny.Maui.Controls.Desktop</c> add-on, which cannot be a dependency of this package. Hence a
/// hook the add-on fills rather than a reference this package cannot take.
/// <para>
/// Every <see cref="BorderlessEntry"/> goes through it, not one control's: it was the quick entry
/// prompt's alone, and the table's <c>EntryCell</c> drew a full bezelled text field inside every row
/// on AppKit.
/// </para>
/// </remarks>
static class BorderlessEntryPolish
{
    /// <summary>Invoked with the entry's platform view whenever its handler is created. Null on hosts that need no help.</summary>
    internal static Action<object?>? Handler { get; set; }

    internal static void Apply(object? platformView)
    {
        if (platformView is null)
            return;

        try
        {
            Handler?.Invoke(platformView);
        }
        catch
        {
            // Cosmetic only — a host that dislikes being poked keeps its default chrome rather than
            // taking the control down with it.
        }
    }
}
