using System.Collections;
using System.Collections.Specialized;
using Microsoft.Maui.Controls;

namespace Shiny.Maui.Controls.Infrastructure;

/// <summary>
/// Keeps what a <see cref="TableView"/> has drawn in step with its sections and cells one row at a time, so a
/// change to an <c>ItemsSource</c> costs the rows it names - not a new cell for every item and a new layout for
/// every section, which made filling a collection one item at a time cost n²/2 cells.
/// </summary>
/// <remarks>
/// A row is a cell in a section's layout, or a section in the table's. Rows are separated - one separator between
/// every two rows, none after the last - so row <c>r</c> of a run that starts at child <c>first</c> is child
/// <c>first + 2r</c>, or <c>first + r</c> where there are no separators.
/// </remarks>
static class TableRows
{
    /// <summary>
    /// Applies an <c>Add</c>, <c>Remove</c>, <c>Replace</c> or <c>Move</c> to <paramref name="list"/>, which holds
    /// one entry per item of the collection that changed, creating entries only for the items that came in.
    /// </summary>
    /// <returns>
    /// False for a <c>Reset</c>, for a change whose indices do not fit the list, and for an item
    /// <paramref name="create"/> makes nothing for: the caller then builds the whole list again.
    /// </returns>
    public static bool TryApply<T>(List<T> list, NotifyCollectionChangedEventArgs e, Func<object?, T?> create, Action<T> release) where T : class
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems is not { } added || e.NewStartingIndex < 0 || e.NewStartingIndex > list.Count)
                    return false;

                if (Create(added, create, release) is not { } addedEntries)
                    return false;

                list.InsertRange(e.NewStartingIndex, addedEntries);
                return true;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems is not { } removed || e.OldStartingIndex < 0 || e.OldStartingIndex + removed.Count > list.Count)
                    return false;

                Release(list, e.OldStartingIndex, removed.Count, release);
                return true;

            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems is not { } replaced || e.NewItems is not { } replacements
                    || e.OldStartingIndex < 0 || e.NewStartingIndex != e.OldStartingIndex
                    || e.OldStartingIndex + replaced.Count > list.Count)
                    return false;

                if (Create(replacements, create, release) is not { } replacementEntries)
                    return false;

                Release(list, e.OldStartingIndex, replaced.Count, release);
                list.InsertRange(e.OldStartingIndex, replacementEntries);
                return true;

            case NotifyCollectionChangedAction.Move:
                // The items come out at the old index and go back in at the new one, which is where the first
                // of them ends up - ObservableCollection.Move's convention.
                if (e.OldItems is not { } moved || e.OldStartingIndex < 0 || e.NewStartingIndex < 0
                    || e.OldStartingIndex + moved.Count > list.Count
                    || e.NewStartingIndex + moved.Count > list.Count)
                    return false;

                var entries = list.GetRange(e.OldStartingIndex, moved.Count);
                list.RemoveRange(e.OldStartingIndex, moved.Count);
                list.InsertRange(e.NewStartingIndex, entries);
                return true;

            default:
                // Reset: anything may have changed, so there is nothing to apply row by row.
                return false;
        }
    }


    /// <summary>
    /// Turns <paramref name="shown"/> into <paramref name="target"/> touching only the rows that differ: one
    /// removal or insertion for each row that left or came, a removal and an insertion for a row that moved.
    /// </summary>
    /// <remarks>
    /// <paramref name="remove"/> is called while the row is still in <paramref name="shown"/>, and
    /// <paramref name="insert"/> before the row goes in, so both see the rows as they are on screen at that
    /// moment. Either returns false when the screen is not what it expects; this then stops and returns false,
    /// and the caller draws everything again.
    /// </remarks>
    public static bool Reconcile<T>(List<T> shown, IReadOnlyList<T> target, Func<int, bool> remove, Func<int, T, bool> insert) where T : class
    {
        var wanted = new HashSet<T>(target, ReferenceEqualityComparer.Instance);
        for (var i = shown.Count - 1; i >= 0; i--)
        {
            if (wanted.Contains(shown[i]))
                continue;

            if (!remove(i))
                return false;
            shown.RemoveAt(i);
        }

        var present = new HashSet<T>(shown, ReferenceEqualityComparer.Instance);
        for (var i = 0; i < target.Count; i++)
        {
            var row = target[i];
            if (i < shown.Count && ReferenceEquals(shown[i], row))
                continue;

            if (present.Contains(row))
            {
                if (i + 1 < shown.Count && ReferenceEquals(shown[i + 1], row))
                {
                    // The row here moved further down. Out it goes - it comes back in when the loop reaches its
                    // new place - and the row that belongs here is then already in place.
                    if (!remove(i))
                        return false;
                    present.Remove(shown[i]);
                    shown.RemoveAt(i);
                    continue;
                }

                // This row moved up from further down.
                var from = i + 1 < shown.Count ? shown.IndexOf(row, i + 1) : -1;
                if (from < 0 || !remove(from))
                    return false;
                shown.RemoveAt(from);
            }
            else
            {
                present.Add(row);
            }

            if (!insert(i, row))
                return false;
            shown.Insert(i, row);
        }

        return shown.Count == target.Count;
    }


    /// <summary>
    /// Puts <paramref name="view"/> in as row <paramref name="row"/> of the <paramref name="rowCount"/> rows that
    /// start at child <paramref name="first"/>, with a separator from <paramref name="separator"/> between it and
    /// its neighbours - or none at all when that is null.
    /// </summary>
    /// <returns>False, with nothing put in, when the layout has fewer children than those rows need.</returns>
    public static bool Insert(Layout layout, int first, int row, int rowCount, View view, Func<View>? separator)
    {
        var children = layout.Children;
        if (separator is null)
        {
            if (first + row > children.Count)
                return false;

            children.Insert(first + row, view);
            return true;
        }

        if (rowCount == 0 || row == 0)
        {
            if (first > children.Count)
                return false;

            if (rowCount > 0)
                children.Insert(first, separator());
            children.Insert(first, view);
            return true;
        }

        // Straight after the row above, and the separator that already followed that row now sits between
        // this one and the row below.
        var at = first + (2 * row) - 1;
        if (at > children.Count)
            return false;

        children.Insert(at, view);
        children.Insert(at, separator());
        return true;
    }


    /// <summary>
    /// Takes row <paramref name="row"/> of <paramref name="rowCount"/> out, with the separator below it - or the
    /// one above it, for the last row.
    /// </summary>
    /// <returns>False, with nothing taken out, when <paramref name="expected"/> is not where that row should be.</returns>
    public static bool RemoveAt(Layout layout, int first, int row, int rowCount, View expected, bool separated)
    {
        var children = layout.Children;
        var at = separated ? first + (2 * row) : first + row;
        if (at >= children.Count || !ReferenceEquals(children[at], expected))
            return false;

        children.RemoveAt(at);
        if (separated && rowCount > 1)
            children.RemoveAt(row < rowCount - 1 ? at : at - 1);
        return true;
    }


    static List<T>? Create<T>(IList items, Func<object?, T?> create, Action<T> release) where T : class
    {
        var entries = new List<T>(items.Count);
        foreach (var item in items)
        {
            if (create(item) is not { } entry)
            {
                // The list would no longer line up with the collection by index.
                foreach (var made in entries)
                    release(made);
                return null;
            }

            entries.Add(entry);
        }
        return entries;
    }

    static void Release<T>(List<T> list, int index, int count, Action<T> release)
    {
        for (var i = index; i < index + count; i++)
            release(list[i]);
        list.RemoveRange(index, count);
    }
}
