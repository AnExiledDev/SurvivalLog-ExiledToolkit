using System;
using GameCore.HotUpdate;

namespace ExiledToolkit.Game;

/// <summary>The reads that need the game's config tables. Main thread only.</summary>
public static partial class GameNames
{
    /// <summary>
    /// The game's text for a language key (<c>GetLocalTxt</c>) in the language it shows, or null when it has none. Never
    /// throws.
    /// </summary>
    public static string? Translate(ConfigManager config, string? key)
    {
        if (string.IsNullOrEmpty(key)) return null;

        try
        {
            return Translated(key, config.GetLocalTxt(key));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>The same in a language of your choosing (<c>GetLocalTxtByLanguage</c>), whatever the game shows. Never throws.</summary>
    public static string? Translate(ConfigManager config, string? key, LanguageType language)
    {
        if (string.IsNullOrEmpty(key)) return null;

        try
        {
            return Translated(key, config.GetLocalTxtByLanguage(key, language));
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// An item's name as the game shows it: the text for its name key, else its own local name, else the key itself;
    /// null when it has none of them. Read through the backing fields, which cost no call into the game.
    /// </summary>
    public static string? ItemName(ConfigManager config, Config_Item? item)
    {
        if (item == null) return null;

        var nameKey = item._ItemName_k__BackingField;

        return FirstName(Translate(config, nameKey), item._ItemName_Local_k__BackingField, nameKey);
    }

    /// <summary>
    /// The same by config id. A missing id is null: the row is looked up in the table itself, since the game's
    /// getter logs an error for every id it lacks.
    /// </summary>
    public static string? ItemName(ConfigManager config, int configId)
    {
        try
        {
            return config._Config_Item_Dict is { } items && items.TryGetValue(configId, out var item) ? ItemName(config, item) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
