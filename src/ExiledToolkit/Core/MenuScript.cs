using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExiledToolkit.Core;

/// <summary>The JavaScript the game's web view runs for the Mods menu. Text goes in as string literals built by <see cref="ToastScript.Str"/>.</summary>
internal static class MenuScript
{
    public const string Global = "window.__exiledtoolkitmenu";

    public const string NoScriptReply = "window.vuplex&&window.vuplex.postMessage('" + MenuCommand.Prefix + "noscript')";

    /// <summary>Run every couple of seconds; silent while the script is installed.</summary>
    public const string Probe = Global + "||" + NoScriptReply;

    /// <summary>Closes the shared menu, whichever mod's copy of the block runs it.</summary>
    public const string Close = "window.__slmodmenu&&window.__slmodmenu.close()";

    public static string Set(IEnumerable<MenuRow> rows) => $"{Global}?{Global}.set([{string.Join(",", rows.Select(RowJson))}]):{NoScriptReply}";

    private static string RowJson(MenuRow row) =>
        $"{{\"id\":{ToastScript.Str(row.Id)},\"label\":{ToastScript.Str(row.Label)},\"title\":{ToastScript.Str(row.Title)},\"icon\":{ToastScript.Str(row.Icon)}," +
        $"\"order\":{row.Order.ToString(CultureInfo.InvariantCulture)},\"wanted\":{(row.IsWanted ? "true" : "false")}}}";
}
