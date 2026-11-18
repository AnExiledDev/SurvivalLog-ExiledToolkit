using System;
using System.Collections.Generic;
using ExiledToolkit.Core;
using GameCore.HotUpdate;
using UnityEngine;

namespace ExiledToolkit.Game;

/// <summary>One of the game's hotkey actions and the key the player bound to it.</summary>
/// <param name="Action">The action's name in the game's code (HotKeyAction), such as "Backpack".</param>
/// <param name="Label">The action's name as the game's key settings show it, in the language the game shows.</param>
public sealed record GameHotkey(string Action, KeyCode Key, string Label, bool IsMouse);

/// <summary>
/// The game's hotkeys, as the player bound them. Reading one goes through the game's SettingManager, so each key
/// is kept once read and dropped when the game saves its settings (every rebind and every reset ends in
/// SettingManager.SaveSetting). Before the game has made its settings nothing is read and no key is known. The game
/// checks its keys with a plain Input.GetKeyDown, so a mod's Ctrl+F also presses the game's F. Main thread. Toolkit
/// 0.25.0 and newer.
/// </summary>
public static class GameKeys
{
    private static readonly KeyBook Book = new(Read);

    /// <summary>The key bound to one action, or None when it has none or the game has not made its settings yet.</summary>
    public static KeyCode KeyFor(HotKeyAction action) => Book.KeyFor((int)action) is { } key ? (KeyCode)key : KeyCode.None;

    /// <summary>Every action that has a key, in the game's order. Empty before the game has made its settings.</summary>
    public static IReadOnlyList<GameHotkey> All()
    {
        var keys = new List<GameHotkey>();

        foreach (HotKeyAction action in Enum.GetValues(typeof(HotKeyAction)))
        {
            var key = KeyFor(action);

            if (key == KeyCode.None) continue;

            keys.Add(new GameHotkey(action.ToString(), key, LabelOf(action), HotKeyBinding.IsMouseKey(key)));
        }

        return keys;
    }

    /// <summary>Drops the keys kept. The Toolkit does this itself each time the game saves its settings.</summary>
    public static void Forget() => Book.Forget();

    internal static void Apply(PatchKit hooks) =>
        Book.IsWatched = hooks.Patch(typeof(GameKeys), typeof(SettingManager), "SaveSetting", postfix: nameof(Forget),
            done: "game keys: each is read once and again after the game's key settings change",
            consequence: "the game's keys are read from its settings on every ask");

    private static int? Read(int action) =>
        GameSingleton.Existing<SettingManager>() is { } settings ? (int)settings.GetKeyBind((HotKeyAction)action) : null;

    /// <summary>The action's name as the game shows it. Its text may not be loaded yet, and then the action's code name stands in.</summary>
    private static string LabelOf(HotKeyAction action)
    {
        try
        {
            return HotKeyBinding.Label(action) is { Length: > 0 } label ? label : action.ToString();
        }
        catch (Exception)
        {
            return action.ToString();
        }
    }
}
