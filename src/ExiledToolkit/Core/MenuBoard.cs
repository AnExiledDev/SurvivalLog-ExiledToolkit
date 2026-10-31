using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExiledToolkit.Core;

/// <summary>One Mods menu entry as the page draws it, in both of the game's languages. Empty Chinese text falls back to the English.</summary>
internal sealed record MenuItem(string Id, string Label, string ChineseLabel, string Title, string ChineseTitle, string Icon, int Order, string Hotkey = "", bool Stations = false);

/// <summary>A row as sent to the page: the text in the game's current language, and whether its mod wants it shown now.</summary>
internal sealed record MenuRow(string Id, string Label, string Title, string Icon, int Order, bool IsWanted, string Hotkey = "", bool Stations = false);

/// <summary>
/// The Mods menu entries mods registered, and what the page was last sent. The page gets the whole list again only
/// when something it shows changes, and again in full after a reload.
/// </summary>
internal sealed class MenuBoard
{
    /// <summary>Ids travel in "|"-separated page messages, so they keep to a plain set of characters.</summary>
    private static readonly Regex IdPattern = new("^[A-Za-z0-9._-]{1,64}$", RegexOptions.CultureInvariant);

    private readonly Dictionary<string, MenuItem> _items = new(StringComparer.Ordinal);
    private string? _shownOnPage;

    public static bool IsValidId(string? id) => id is not null && IdPattern.IsMatch(id);

    public IReadOnlyCollection<string> Ids => _items.Keys;

    /// <summary>Adds the item, or replaces the one with its id.</summary>
    public void Put(MenuItem item)
    {
        if (!IsValidId(item.Id)) throw new ArgumentException($"menu id \"{item.Id}\" must be 1 to 64 letters, digits, '.', '_' or '-'", nameof(item));

        _items[item.Id] = item;
    }

    public bool Remove(string id) => _items.Remove(id);

    /// <summary>A fresh page shows nothing, so the next look sends everything.</summary>
    public void PageReset() => _shownOnPage = null;

    /// <summary>The script that brings the page up to date, or null when it already shows this.</summary>
    public string? Pending(bool isChinese, Func<string, bool> isWanted)
    {
        var script = MenuScript.Set(Rows(isChinese, isWanted));

        return script == _shownOnPage ? null : script;
    }

    /// <summary>The page ran <paramref name="script"/>, which came from <see cref="Pending"/>.</summary>
    public void Shown(string script) => _shownOnPage = script;

    public IReadOnlyList<MenuRow> Rows(bool isChinese, Func<string, bool> isWanted) =>
        _items.Values
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new MenuRow(item.Id, Pick(isChinese, item.ChineseLabel, item.Label), Pick(isChinese, item.ChineseTitle, item.Title), item.Icon, item.Order, isWanted(item.Id), item.Hotkey, item.Stations))
            .ToList();

    private static string Pick(bool isChinese, string chinese, string english) => isChinese && chinese.Length > 0 ? chinese : english;
}
