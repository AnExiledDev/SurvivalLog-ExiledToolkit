using System;
using GameCore.HotUpdate;

namespace ExiledToolkit.Game;

/// <summary>
/// Asks the game which language it is showing, only once the game has made its settings. Anything that goes wrong reads
/// as English. Asked on every read and every page call, so the settings wrapper is kept while the game keeps the same
/// settings object. Main thread only. Toolkit 0.19.0 and newer (Mod Manager used it before).
/// </summary>
public static class GameLanguage
{
    private static IntPtr _settingsPointer;
    private static SettingManager? _settings;

    public static bool IsChinese()
    {
        try
        {
            return Settings()?.IsChinese() == true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>The game's language setting, for keeping names read from its tables only while it stays the same; empty when unknown.</summary>
    public static string Key()
    {
        try
        {
            return Settings()?.GetLanguageKey() ?? "";
        }
        catch (Exception)
        {
            return "";
        }
    }

    private static SettingManager? Settings()
    {
        var pointer = GameSingleton.Pointer<SettingManager>();

        if (pointer == IntPtr.Zero) return null;

        if (pointer != _settingsPointer || _settings == null)
        {
            _settings = GameSingleton.Existing<SettingManager>();
            _settingsPointer = pointer;
        }

        return _settings;
    }
}
