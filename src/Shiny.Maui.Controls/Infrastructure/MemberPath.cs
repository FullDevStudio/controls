using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Shiny.Maui.Controls.Infrastructure;

/// <summary>
/// The one place the library resolves a property path the <em>consumer</em> handed it as a string —
/// <c>DisplayMemberPath</c>, <c>DisplayMember</c>, <c>TextMemberPath</c>, a DataGrid column's
/// <c>PropertyName</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every binding the library makes to its own types is a typed (source-generated) binding and is
/// trim safe. A path named by the app over the app's own model cannot be: the property is only known
/// at runtime, which is exactly the trade-off MAUI's own <c>new Binding("Name")</c> makes. The
/// warnings are suppressed here, once, so a trimmed app that never sets one of these properties gets
/// no warnings from the library. Each property has a trim-safe route instead: <c>ItemDisplayBinding</c>
/// (ChipGroup), <c>ValueGetter</c> (DataGrid columns), or leaving the path unset so the item's own
/// <c>ToString()</c> is shown (pickers, AutoCompleteEntry).
/// </para>
/// </remarks>
static class MemberPath
{
    const string Justification =
        "A member path the consumer supplied as a string over their own model type. Full-trim apps should use " +
        "ItemDisplayBinding / ValueGetter, or leave the path unset and override the item's ToString().";

    static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> Cache = new();

    /// <summary>A string-path <see cref="Binding"/> over a consumer-supplied path.</summary>
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = Justification)]
    public static Binding Binding(
        string path,
        BindingMode mode = BindingMode.Default,
        IValueConverter? converter = null
    )
        => new(path, mode, converter);

    /// <summary>
    /// Reads <paramref name="item"/>'s public <paramref name="propertyName"/>; <c>false</c> when the
    /// item has no such property, so the caller keeps its own fallback.
    /// </summary>
    public static bool TryGetValue(object item, string propertyName, out object? value)
    {
        var prop = GetProperty(item.GetType(), propertyName);
        value = prop?.GetValue(item);
        return prop is not null;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = Justification)]
    static PropertyInfo? GetProperty(Type type, string name)
    {
        if (Cache.TryGetValue((type, name), out var cached))
            return cached;

        var prop = type.GetProperty(name);
        Cache[(type, name)] = prop;
        return prop;
    }
}
