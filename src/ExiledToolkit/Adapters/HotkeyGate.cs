using System;
using GameCore.HotUpdate.Battle.Show;
using HarmonyLib;

namespace ExiledToolkit.Game;

/// <summary>
/// Stops the game's hotkeys while a mod window holds them, so typing "e" in a search box does not also open a menu,
/// and the Esc that closes the window does not also close what is under it. Each window holds and releases under its
/// own name. One hook on HotKeyManager.Update serves every mod. Static because a Harmony prefix has nowhere else to
/// look; main thread only. Toolkit 0.19.0 and newer (Mod Manager used it before).
/// </summary>
public static class HotkeyGate
{
    private static readonly KeyHolds Holds = new();
    private static readonly Func<int> Frame = UnityFrame.Count;

    /// <summary>Whether any window holds the hotkeys now.</summary>
    public static bool IsHeld => Holds.IsHeld;

    /// <summary>Holding twice under one name is one hold.</summary>
    public static void Hold(string holder) => Holds.Hold(holder);

    /// <summary>Keeps hotkeys stopped through the next frame, so the key that closed the window (Esc) is not seen by the game too. A name that was not holding changes nothing.</summary>
    public static void Release(string holder) => Holds.Release(holder, Frame);

    internal static void Apply(Harmony harmony, Action<string> log)
    {
        try
        {
            harmony.Patch(AccessTools.Method(typeof(HotKeyManager), "Update"), prefix: new HarmonyMethod(typeof(HotkeyGate), nameof(SkipWhileHeld)));
            log("game hotkeys can be held by mod windows");
        }
        catch (Exception ex)
        {
            log("could not hook the game's hotkeys, so keys pressed in a mod window also reach the game: " + ex.Message);
        }
    }

    private static bool SkipWhileHeld() => Holds.LetsGameRun(Frame);
}
