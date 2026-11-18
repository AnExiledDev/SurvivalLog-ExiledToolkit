using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExiledToolkit;

/// <summary>A row a mod added to one of the game's tables.</summary>
/// <param name="Table">The row class's name, the way the game names the table (Config_Item).</param>
/// <param name="Row">The row's id, as text.</param>
public sealed record RowClaim(string Guid, string Table, string Row);

/// <summary>
/// A value a mod wrote into one field of a row in the game's tables. Order counts up across every write by every mod,
/// so of two mods writing one field, the one with the higher Order wrote last and its value is the one in the table.
/// </summary>
public sealed record FieldClaim(string Guid, string Table, string Row, string Field, string Value, long Order);

/// <summary>A native hook (one Harmony does not place or record) a mod put on a game method, named "Type.Method".</summary>
public sealed record HookClaim(string Guid, string Method);

/// <summary>Bytes a mod changed in a game method's machine code: Length bytes from Offset, counted from the method's start.</summary>
public sealed record BytesClaim(string Guid, string Method, int Offset, int Length);

/// <summary>Everything every mod had reported at one moment. Fields come in the order they were written; the rest are sorted.</summary>
public sealed record ClaimSet(IReadOnlyList<RowClaim> Rows, IReadOnlyList<FieldClaim> Fields, IReadOnlyList<HookClaim> Hooks, IReadOnlyList<BytesClaim> Bytes);

/// <summary>What every mod reported through <see cref="Claims"/>, kept until it takes it back. Any thread.</summary>
internal sealed class ClaimBook
{
    private readonly object _gate = new();
    private readonly HashSet<RowClaim> _rows = new();
    private readonly Dictionary<(string Guid, string Table, string Row, string Field), FieldClaim> _fields = new();
    private readonly HashSet<HookClaim> _hooks = new();
    private readonly Dictionary<(string Guid, string Method, int Offset), BytesClaim> _bytes = new();
    private long _order;

    public void AddRows(string guid, string table, IReadOnlyList<string> rows)
    {
        lock (_gate)
        {
            foreach (var row in rows) _rows.Add(new RowClaim(guid, table, row));
        }
    }

    public void RemoveRows(string guid, string table, IReadOnlyList<string> rows)
    {
        lock (_gate)
        {
            foreach (var row in rows) _rows.Remove(new RowClaim(guid, table, row));
        }
    }

    /// <summary>A second write by the same mod to the same field replaces its first, and counts as its latest.</summary>
    public void Write(string guid, string table, string row, string field, string value)
    {
        lock (_gate) _fields[(guid, table, row, field)] = new FieldClaim(guid, table, row, field, value, ++_order);
    }

    public void PutBack(string guid, string table, string row, string field)
    {
        lock (_gate) _fields.Remove((guid, table, row, field));
    }

    public void Hook(string guid, string method)
    {
        lock (_gate) _hooks.Add(new HookClaim(guid, method));
    }

    public void Unhook(string guid, string method)
    {
        lock (_gate) _hooks.Remove(new HookClaim(guid, method));
    }

    /// <summary>Changing bytes again at the same offset replaces the first report, so its length can change.</summary>
    public void ChangeBytes(string guid, string method, int offset, int length)
    {
        lock (_gate) _bytes[(guid, method, offset)] = new BytesClaim(guid, method, offset, length);
    }

    public void RestoreBytes(string guid, string method, int offset)
    {
        lock (_gate) _bytes.Remove((guid, method, offset));
    }

    public ClaimSet All()
    {
        lock (_gate)
        {
            return new ClaimSet(
                _rows.OrderBy(claim => claim.Guid, StringComparer.Ordinal).ThenBy(claim => claim.Table, StringComparer.Ordinal).ThenBy(claim => claim.Row, StringComparer.Ordinal).ToList(),
                _fields.Values.OrderBy(claim => claim.Order).ToList(),
                _hooks.OrderBy(claim => claim.Method, StringComparer.Ordinal).ThenBy(claim => claim.Guid, StringComparer.Ordinal).ToList(),
                _bytes.Values.OrderBy(claim => claim.Method, StringComparer.Ordinal).ThenBy(claim => claim.Offset).ThenBy(claim => claim.Guid, StringComparer.Ordinal).ToList());
        }
    }
}

/// <summary>
/// What a mod changes in the game that Harmony does not record: rows it adds to the game's tables, values it writes
/// into them, native hooks and changed code bytes. A mod reports each change as it makes it, and takes it back when it
/// undoes it (a feature turned off). The Toolkit only keeps the list, and reporting changes nothing in the game. The
/// Mod Manager reads it to find two mods that change the same thing, and to say which mod changed what. Any thread.
/// Toolkit 0.25.0 and newer.
/// </summary>
public static class Claims
{
    internal static readonly ClaimBook Book = new();

    /// <summary>The mod added these rows to <paramref name="table"/>, the row class's name (Config_Item). Ids are written as invariant text, so 910001 and "910001" are the same row.</summary>
    public static void AddedRows<T>(string guid, string table, IEnumerable<T> rows) => Book.AddRows(Named(guid), Named(table), Texts(rows));

    /// <summary>The mod took these rows out again.</summary>
    public static void RemovedRows<T>(string guid, string table, IEnumerable<T> rows) => Book.RemoveRows(Named(guid), Named(table), Texts(rows));

    /// <summary>The mod wrote <paramref name="value"/> into <paramref name="field"/> (the property's name) of a row.</summary>
    public static void Wrote(string guid, string table, object row, string field, object? value) =>
        Book.Write(Named(guid), Named(table), Text(row ?? throw new ArgumentNullException(nameof(row))), Named(field), Text(value));

    /// <summary>The mod put the field back to what it held before the mod wrote it.</summary>
    public static void PutBack(string guid, string table, object row, string field) =>
        Book.PutBack(Named(guid), Named(table), Text(row ?? throw new ArgumentNullException(nameof(row))), Named(field));

    /// <summary>The mod placed a native hook on <paramref name="type"/>.<paramref name="method"/>, the interop type and method name.</summary>
    public static void Hooked(string guid, Type type, string method) => Book.Hook(Named(guid), MethodName(type, method));

    /// <summary>The mod took the native hook off again.</summary>
    public static void Unhooked(string guid, Type type, string method) => Book.Unhook(Named(guid), MethodName(type, method));

    /// <summary>The mod changed <paramref name="length"/> bytes of the method's machine code, <paramref name="offset"/> bytes from its start.</summary>
    public static void ChangedBytes(string guid, Type type, string method, int offset, int length)
    {
        if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));

        if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));

        Book.ChangeBytes(Named(guid), MethodName(type, method), offset, length);
    }

    /// <summary>The mod put back the bytes it changed at <paramref name="offset"/>.</summary>
    public static void RestoredBytes(string guid, Type type, string method, int offset) => Book.RestoreBytes(Named(guid), MethodName(type, method), offset);

    /// <summary>A copy of everything reported so far.</summary>
    public static ClaimSet All() => Book.All();

    /// <summary>How claims name a method: the type's short name and the method's, as the Mod Manager names Harmony patches.</summary>
    public static string MethodName(Type type, string method) => (type ?? throw new ArgumentNullException(nameof(type))).Name + "." + Named(method);

    private static string Named(string text) => string.IsNullOrWhiteSpace(text) ? throw new ArgumentException("needs a name, not blank text") : text;

    private static IReadOnlyList<string> Texts<T>(IEnumerable<T> rows) => (rows ?? throw new ArgumentNullException(nameof(rows))).Select(row => Text(row ?? throw new ArgumentException("a row id is null"))).Distinct(StringComparer.Ordinal).ToList();

    private static string Text(object? value) => value is null ? "null" : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
}
