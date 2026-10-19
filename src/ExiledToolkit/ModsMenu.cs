using System;
using System.Collections.Generic;
using ExiledToolkit.Core;

namespace ExiledToolkit;

/// <summary>
/// One entry in the inventory toolbar's "Mods" dropdown. Id, label and click action are required; the rest is optional.
/// Chinese text left empty shows the English.
/// </summary>
public sealed class ModsMenuEntry
{
    public ModsMenuEntry(string id, string label, Action onClick)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        OnClick = onClick ?? throw new ArgumentNullException(nameof(onClick));
    }

    /// <summary>Unique across mods: 1 to 64 letters, digits, '.', '_' or '-'. Use "yourmod.action".</summary>
    public string Id { get; }

    public string Label { get; }

    public string ChineseLabel { get; init; } = "";

    /// <summary>The tooltip.</summary>
    public string Title { get; init; } = "";

    public string ChineseTitle { get; init; } = "";

    /// <summary>The <c>d</c> of an SVG path on a 24-unit grid; empty for no icon.</summary>
    public string Icon { get; init; } = "";

    /// <summary>Rows sort by this, then by id. Storage Hub uses 100s, Unpack All 200.</summary>
    public int Order { get; init; } = 500;

    /// <summary>
    /// Asked on the main thread a few times a second; the entry is listed only while it returns true. Null lists it
    /// always. One that throws counts as false and is logged once. The menu itself only shows on the inventory's item tab.
    /// </summary>
    public Func<bool>? IsShown { get; init; }

    /// <summary>Runs on the game's main thread after the player picks the entry and the menu has closed.</summary>
    public Action OnClick { get; }
}

/// <summary>
/// The inventory toolbar's shared "Mods" dropdown. Declare a hard dependency on <c>com.dev.survivallog.exiledtoolkit</c>
/// and call <see cref="Add"/> once, say from your plugin's Load.
/// </summary>
public static class ModsMenu
{
    internal static readonly MenuBoard Board = new();

    private static readonly object Gate = new();
    private static readonly Dictionary<string, ModsMenuEntry> Entries = new(StringComparer.Ordinal);

    /// <summary>Adds the entry, or replaces the one with its id. Safe from any thread; the page has it within a second.</summary>
    public static void Add(ModsMenuEntry entry)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));

        lock (Gate)
        {
            Board.Put(new MenuItem(entry.Id, entry.Label, entry.ChineseLabel, entry.Title, entry.ChineseTitle, entry.Icon, entry.Order));
            Entries[entry.Id] = entry;
        }
    }

    public static bool Remove(string id)
    {
        lock (Gate)
        {
            Entries.Remove(id);

            return Board.Remove(id);
        }
    }

    internal static ModsMenuEntry? Find(string id)
    {
        lock (Gate)
        {
            return Entries.TryGetValue(id, out var entry) ? entry : null;
        }
    }

    /// <summary>Under the lock, so an Add from another thread cannot change the board mid-read.</summary>
    internal static string? Pending(bool isChinese, Func<ModsMenuEntry, bool> isShown)
    {
        lock (Gate)
        {
            return Board.Pending(isChinese, id => Entries.TryGetValue(id, out var entry) && isShown(entry));
        }
    }

    internal static void Shown(string script)
    {
        lock (Gate)
        {
            Board.Shown(script);
        }
    }

    internal static void PageReset()
    {
        lock (Gate)
        {
            Board.PageReset();
        }
    }
}
