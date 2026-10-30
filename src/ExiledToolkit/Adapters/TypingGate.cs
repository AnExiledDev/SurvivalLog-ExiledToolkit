using System;
using System.Collections.Generic;
using ExiledToolkit.Core;
using GameCore.HotUpdate.Battle.Show;
using HarmonyLib;
using UnityEngine;

namespace ExiledToolkit.Adapters;

/// <summary>
/// Keeps the game's hotkeys and the camera's keys off while a text box in any page has the keyboard, so typing "p" in a
/// mod's search box does not take a photo or open a panel, and WASD does not move the camera. typing.js says when a box
/// takes or loses the keyboard. The camera reads its keys straight from Unity, so its own block is held on for each of
/// its updates and put back after, which leaves the game's own block (a bench, the codex) as the game set it. Static
/// because Harmony patches have nowhere else to look; main thread only.
/// </summary>
internal static class TypingGate
{
    private const string Holder = "a text box";

    private static readonly HashSet<string> ReportedFailures = new();
    private static Action<string> _log = _ => { };

    public static bool IsActive { get; private set; }

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        try
        {
            harmony.Patch(AccessTools.Method(typeof(CameraManager), "Update"),
                prefix: new HarmonyMethod(typeof(TypingGate), nameof(BlockCameraKeys)),
                postfix: new HarmonyMethod(typeof(TypingGate), nameof(RestoreCameraKeys)) { priority = Priority.Last });
            log("camera keys stop while a text box has the keyboard");
        }
        catch (Exception ex)
        {
            log("could not hook the camera, so keys typed in a text box also move it: " + ex.Message);
        }
    }

    public static void Set(bool isTyping)
    {
        if (isTyping == IsActive) return;

        IsActive = isTyping;

        if (isTyping) HotkeyGate.Hold(Holder);
        else HotkeyGate.Release(Holder);
    }

    /// <summary>
    /// A click on the world never reaches the browser, so the box would keep the keyboard; the game drops its own search
    /// box's focus the same way. Only a click that lands on no UI counts.
    /// </summary>
    public static void Tick(WebPage? page)
    {
        if (!IsActive) return;

        if (!Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1)) return;

        if (MouseRaycastService.IsPointerOverUI()) return;

        page?.Send(TypingScript.Blur);
        Set(false);
    }

    private static void BlockCameraKeys(CameraManager __instance, out bool __state)
    {
        __state = false;

        if (!IsActive) return;

        try
        {
            if (__instance.isKeyboardMoveBlocked) return;

            __instance.isKeyboardMoveBlocked = true;
            __instance._isKeyboardMoving_k__BackingField = false;
            __state = true;
        }
        catch (Exception ex)
        {
            ReportOnce(ex);
        }
    }

    private static void RestoreCameraKeys(CameraManager __instance, bool __state)
    {
        if (!__state) return;

        try
        {
            __instance.isKeyboardMoveBlocked = false;
        }
        catch (Exception ex)
        {
            ReportOnce(ex);
        }
    }

    private static void ReportOnce(Exception ex)
    {
        if (!ReportedFailures.Add(ex.GetType().Name + ": " + ex.Message)) return;

        _log("camera key block failed (each kind of failure is logged once): " + ex);
    }
}
