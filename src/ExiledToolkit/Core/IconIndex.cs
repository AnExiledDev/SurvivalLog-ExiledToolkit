using System;
using System.Collections.Generic;

namespace ExiledToolkit.Web;

/// <summary>
/// Finds an item's picture among the game's web icons (WebUI/Res/&lt;Folder&gt;/&lt;name&gt;.png). The tables name an
/// icon in a form the web pages turn into a path themselves (such as
/// "../../Res/Furniture/UI_Item_Icon_Furnit_M_831300313.png"), so a lookup matches on the file name alone. Toolkit
/// 0.19.0 and newer; <c>WebIcons</c> builds the game's one.
/// </summary>
public sealed class IconIndex
{
    public static readonly IconIndex Empty = new(new Dictionary<string, List<string>>());

    private readonly IReadOnlyDictionary<string, List<string>> _pathsByName;

    private IconIndex(IReadOnlyDictionary<string, List<string>> pathsByName)
    {
        _pathsByName = pathsByName;
    }

    public int Count => _pathsByName.Count;

    /// <summary>Indexes icon files given relative to the WebUI folder, such as "Res/Furniture/UI_Item_Icon_Furnit_M_x.png".</summary>
    public static IconIndex Build(IEnumerable<string> relativePaths)
    {
        var pathsByName = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in relativePaths)
        {
            var name = FileStem(path);

            if (!pathsByName.TryGetValue(name, out var paths)) pathsByName[name] = paths = new List<string>();

            paths.Add(path.Replace('\\', '/'));
        }

        return new IconIndex(pathsByName);
    }

    /// <summary>
    /// The WebUI-relative path of the icon a table names, or null when the game has no such web icon. Of several files
    /// with the name, the one whose path the table's ends with wins, else the first found.
    /// </summary>
    public string? Resolve(string? named)
    {
        if (string.IsNullOrWhiteSpace(named) || !_pathsByName.TryGetValue(FileStem(named), out var paths)) return null;

        var hint = named.Replace('\\', '/');

        for (var i = 0; i < paths.Count; i++)
        {
            if (hint.EndsWith(paths[i], StringComparison.OrdinalIgnoreCase)) return paths[i];
        }

        return paths[0];
    }

    private static string FileStem(string path)
    {
        var name = path.Substring(path.Replace('\\', '/').LastIndexOf('/') + 1);
        var dot = name.LastIndexOf('.');

        return dot > 0 ? name.Substring(0, dot) : name;
    }
}
