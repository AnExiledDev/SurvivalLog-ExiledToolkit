using System;
using GameCore.HotUpdate;

namespace ExiledToolkit.Game;

/// <summary>
/// Which save is being played. The game writes each save to its own file named by GameSaveData.FileName, so that name
/// is the save's identity for anything a mod keeps per save. Main thread only. Toolkit 0.19.0 and newer.
/// </summary>
public static class SaveSlot
{
    /// <summary>The loaded save's file name, or null before a save is loaded or when it cannot be read.</summary>
    public static string? CurrentKey()
    {
        try
        {
            var name = GameSingleton.Existing<GameSaveManager>()?.gameSave?.FileName;

            return string.IsNullOrEmpty(name) ? null : name;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
