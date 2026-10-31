using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace ExiledToolkit.Core;

/// <summary>Where a button goes, as gamemenu.js reads it.</summary>
internal static class GameMenuSpot
{
    /// <summary>The pause menu, right after the game's Settings button.</summary>
    public const string PauseTop = "top";

    /// <summary>The pause menu, at the bottom just above the game's Restart and Save &amp; Exit buttons.</summary>
    public const string PauseBottom = "bottom";

    /// <summary>The title screen, right before the game's Exit.</summary>
    public const string Title = "title";

    public static bool IsKnown(string spot) => spot is PauseTop or PauseBottom or Title;
}

/// <summary>One button as a mod added it, in both of the game's languages. Empty Chinese text falls back to the English.</summary>
internal sealed record GameMenuItem(string Id, string Label, string ChineseLabel, string Spot, int Order, string Accent);

/// <summary>A button as sent to the page: its text in the game's current language, and whether its mod wants it shown now.</summary>
internal sealed record GameMenuRow(string Id, string Label, string Spot, int Order, string Accent, bool IsWanted);

/// <summary>
/// The buttons mods added to the game's pause menu and title screen, and what the page was last sent. The page gets the
/// whole list again only when something it shows changes, and again in full after a reload.
/// </summary>
internal sealed class GameMenuBoard
{
    private static readonly Regex AccentPattern = new("^#(?:[0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$", RegexOptions.CultureInvariant);

    private readonly Dictionary<string, GameMenuItem> _items = new(StringComparer.Ordinal);
    private string? _shownOnPage;

    /// <summary>Empty for the game's own colour, or a CSS colour as #rgb or #rrggbb.</summary>
    public static bool IsValidAccent(string? accent) => accent is not null && (accent.Length == 0 || AccentPattern.IsMatch(accent));

    /// <summary>Adds the item, or replaces the one with its id.</summary>
    public void Put(GameMenuItem item)
    {
        if (!MenuBoard.IsValidId(item.Id)) throw new ArgumentException($"menu button id \"{item.Id}\" must be 1 to 64 letters, digits, '.', '_' or '-'", nameof(item));
        if (!GameMenuSpot.IsKnown(item.Spot)) throw new ArgumentException($"menu button {item.Id} has an unknown place \"{item.Spot}\"", nameof(item));
        if (!IsValidAccent(item.Accent)) throw new ArgumentException($"menu button {item.Id} accent \"{item.Accent}\" must be empty, #rgb or #rrggbb", nameof(item));

        _items[item.Id] = item;
    }

    public bool Remove(string id) => _items.Remove(id);

    /// <summary>A fresh page shows nothing, so the next look sends everything.</summary>
    public void PageReset() => _shownOnPage = null;

    /// <summary>The script that brings the page up to date, or null when it already shows this.</summary>
    public string? Pending(bool isChinese, Func<string, bool> isWanted)
    {
        var script = GameMenuScript.Set(Rows(isChinese, isWanted));

        return script == _shownOnPage ? null : script;
    }

    /// <summary>The page ran <paramref name="script"/>, which came from <see cref="Pending"/>.</summary>
    public void Shown(string script) => _shownOnPage = script;

    public IReadOnlyList<GameMenuRow> Rows(bool isChinese, Func<string, bool> isWanted) =>
        _items.Values
            .OrderBy(item => item.Order)
            .ThenBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new GameMenuRow(item.Id, isChinese && item.ChineseLabel.Length > 0 ? item.ChineseLabel : item.Label, item.Spot, item.Order, item.Accent, isWanted(item.Id)))
            .ToList();
}

/// <summary>The JavaScript the game's web view runs for the menu buttons. Text goes in as string literals built by <see cref="ToastScript.Str"/>.</summary>
internal static class GameMenuScript
{
    public const string Global = "window.__exiledtoolkitgamemenu";

    public const string NoScriptReply = "window.vuplex&&window.vuplex.postMessage('" + GameMenuCommand.Prefix + "noscript')";

    /// <summary>Run every couple of seconds; silent while the script is installed.</summary>
    public const string Probe = Global + "||" + NoScriptReply;

    public static string Set(IEnumerable<GameMenuRow> rows) => $"{Global}?{Global}.set([{string.Join(",", rows.Select(RowJson))}]):{NoScriptReply}";

    private static string RowJson(GameMenuRow row) =>
        $"{{\"id\":{ToastScript.Str(row.Id)},\"label\":{ToastScript.Str(row.Label)},\"spot\":{ToastScript.Str(row.Spot)},\"order\":{row.Order.ToString(CultureInfo.InvariantCulture)}," +
        $"\"accent\":{ToastScript.Str(row.Accent)},\"wanted\":{(row.IsWanted ? "true" : "false")}}}";
}

/// <summary>A message from gamemenu.js, sent as <c>slmod|toolkitgamemenu|verb|args</c>. Anything else parses to Unknown.</summary>
internal abstract record GameMenuCommand
{
    public const string Prefix = "slmod|toolkitgamemenu|";

    private GameMenuCommand()
    {
    }

    /// <summary>The script was just installed in a fresh page.</summary>
    public sealed record Ready : GameMenuCommand;

    /// <summary>The page answered without the script: the web view reloaded.</summary>
    public sealed record NoScript : GameMenuCommand;

    /// <summary>The player clicked this button.</summary>
    public sealed record Run(string Id) : GameMenuCommand;

    /// <summary>Something the page could not do, for the log.</summary>
    public sealed record Diagnostic(string Text) : GameMenuCommand;

    public sealed record Unknown(string Text) : GameMenuCommand;

    public static GameMenuCommand Parse(string message)
    {
        if (!message.StartsWith(Prefix, StringComparison.Ordinal)) return new Unknown(message);

        var body = message.Substring(Prefix.Length);
        var parts = body.Split('|');

        return (parts[0], parts.Length) switch
        {
            ("ready", 1) => new Ready(),
            ("noscript", 1) => new NoScript(),
            ("run", 2) when MenuBoard.IsValidId(parts[1]) => new Run(parts[1]),
            ("diag", > 1) => new Diagnostic(body.Substring("diag|".Length)),
            _ => new Unknown(message),
        };
    }
}
