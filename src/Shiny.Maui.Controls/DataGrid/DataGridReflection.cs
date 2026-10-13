using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Shiny.Maui.Controls.DataGrid;

/// <summary>
/// Reflection-based property access for string-path columns. This is the same trade-off MAUI's own
/// string-path bindings make. Consumers targeting full trimming / NativeAOT should set a column's
/// <c>ValueGetter</c>/<c>ValueSetter</c> to avoid reflection entirely.
/// </summary>
static class DataGridReflection
{
    static readonly ConcurrentDictionary<(Type, string), PropertyInfo?> Cache = new();

    // Not a GetOrAdd lambda: the type would reach GetProperty through a tuple field, which the
    // analyzer reports separately (IL2080) and this method's suppression does not cover.
    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "DataGrid binds to user model properties by name; set Column.ValueGetter for full-trim scenarios.")]
    static PropertyInfo? GetProperty(Type type, string name)
    {
        if (Cache.TryGetValue((type, name), out var cached))
            return cached;

        var pi = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        Cache[(type, name)] = pi;
        return pi;
    }

    public static object? GetValue(object? item, string? propertyName)
    {
        if (item is null || string.IsNullOrEmpty(propertyName))
            return null;

        return GetProperty(item.GetType(), propertyName)?.GetValue(item);
    }

    public static void SetValue(object? item, string? propertyName, object? value)
    {
        if (item is null || string.IsNullOrEmpty(propertyName))
            return;

        var pi = GetProperty(item.GetType(), propertyName);
        if (pi is null || !pi.CanWrite)
            return;

        pi.SetValue(item, ConvertValue(value, pi.PropertyType));
    }

    public static Type GetPropertyType(Type itemType, string? propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return typeof(string);

        var pi = GetProperty(itemType, propertyName);
        if (pi is null)
            return typeof(string);

        return Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
    }

    static object? ConvertValue(object? value, Type targetType)
    {
        if (value is null)
            return null;
        if (targetType.IsInstanceOfType(value))
            return value;

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try
        {
            if (underlying.IsEnum)
                return value is string s ? Enum.Parse(underlying, s, true) : Enum.ToObject(underlying, value);
            return Convert.ChangeType(value, underlying, System.Globalization.CultureInfo.CurrentCulture);
        }
        catch
        {
            return value;
        }
    }
}
