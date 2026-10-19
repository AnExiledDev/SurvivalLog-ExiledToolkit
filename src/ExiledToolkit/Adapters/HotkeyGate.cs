using System;
using System.Collections.Generic;
using GameCore.HotUpdate.Battle.Show;
using HarmonyLib;
using UnityEngine;

namespace ExiledToolkit.Adapters;

/// <summary>
/// Stops the game's hotkeys while a mod window holds them, so typing "e" in a search box does not also open a menu,
/// and the Esc that closes the window does not also close what is under it. Each window holds and releases under its
/// own name. Static because a Harmony prefix has nowhere else to look; main thread only.
/// </summary>
internal static class HotkeyGate
{
    private static readonly HashSet<string> Holders = new();
    private static int _blockedThroughFrame = -1;

    public static void Apply(Harmony harmony, Action<string> log)
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

    public static void Hold(string holder) => Holders.Add(holder);

    /// <summary>Keeps hotkeys stopped through the next frame, so the key that closed the window (Esc) is not seen by the game too.</summary>
    public static void Release(string holder)
    {
        if (!Holders.Remove(holder)) return;

        _blockedThroughFrame = Time.frameCount + 1;
    }

    private static bool SkipWhileHeld() => Holders.Count == 0 && Time.frameCount > _blockedThroughFrame;
}
