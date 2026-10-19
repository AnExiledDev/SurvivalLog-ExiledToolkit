using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExiledToolkit;

/// <summary>An item and how many of it, as an ItemCounts setting holds them.</summary>
public readonly record struct ItemCount(int Id, int Count);

/// <summary>
/// Reads and writes the string formats behind the Mod Manager's item controls: "1203,7" for ItemList and
/// "1203:5,7:2" for ItemCounts. Parsing never throws: a .cfg edited by hand can hold anything, so an entry that is
/// not a whole number of 0 or more (or an amount under 1) is skipped and the rest are kept.
/// </summary>
public static class ItemValues
{
    /// <summary>The item ids of an ItemList value, in order, each once.</summary>
    public static IReadOnlyList<int> ParseList(string? text)
    {
        var ids = new List<int>();

        foreach (var entry in Entries(text))
        {
            if (IdOf(entry) is { } id && !ids.Contains(id)) ids.Add(id);
        }

        return ids;
    }

    /// <summary>The items and amounts of an ItemCounts value, in order. An item listed twice keeps its first amount.</summary>
    public static IReadOnlyList<ItemCount> ParseCounts(string? text)
    {
        var counts = new List<ItemCount>();

        foreach (var entry in Entries(text))
        {
            var pair = entry.Split(':');

            if (pair.Length != 2 || IdOf(pair[0].Trim()) is not { } id || IdOf(pair[1].Trim()) is not ({ } count and > 0)) continue;

            if (counts.All(item => item.Id != id)) counts.Add(new ItemCount(id, count));
        }

        return counts;
    }

    /// <summary>The ItemList value for <paramref name="ids"/>, such as "1203,7". Negative ids and repeats are left out.</summary>
    public static string FormatList(IEnumerable<int> ids)
    {
        if (ids == null) throw new ArgumentNullException(nameof(ids));

        return string.Join(",", ids.Where(id => id >= 0).Distinct().Select(Text));
    }

    /// <summary>The ItemCounts value for <paramref name="counts"/>, such as "1203:5,7:2". Amounts under 1, negative ids and repeated items are left out.</summary>
    public static string FormatCounts(IEnumerable<ItemCount> counts)
    {
        if (counts == null) throw new ArgumentNullException(nameof(counts));

        var kept = new List<ItemCount>();

        foreach (var item in counts.Where(item => item.Id >= 0 && item.Count > 0))
        {
            if (kept.All(other => other.Id != item.Id)) kept.Add(item);
        }

        return string.Join(",", kept.Select(item => Text(item.Id) + ":" + Text(item.Count)));
    }

    private static IEnumerable<string> Entries(string? text) =>
        (text ?? "").Split(',').Select(entry => entry.Trim()).Where(entry => entry.Length > 0);

    /// <summary>Digits only, so a sign, a fraction or a number too big for an int never gets through.</summary>
    private static int? IdOf(string text) =>
        text.Length > 0 && text.All(c => c >= '0' && c <= '9') && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    private static string Text(int number) => number.ToString(CultureInfo.InvariantCulture);
}
