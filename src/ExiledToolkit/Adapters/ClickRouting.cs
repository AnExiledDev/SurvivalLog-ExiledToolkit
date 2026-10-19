using System;
using System.Collections.Generic;
using ExiledToolkit.Core;
using GameCore.HotUpdate.ReduxUI;
using HarmonyLib;
using UnityEngine;

namespace ExiledToolkit.Adapters;

/// <summary>
/// Lets clicks and hovering over a toast reach the web view. The game sends the pointer to its web UI only where one of
/// its pages reported a button, so everywhere else, toast boxes included, the click would go to the world below. The
/// page reports where its boxes are; this answers yes for those points and leaves every other answer to the game.
/// A mod window that covers the whole screen captures every click while it is open. Static because a Harmony postfix
/// has nowhere else to look.
/// </summary>
internal static class ClickRouting
{
    private static readonly HashSet<string> Capturers = new();
    private static volatile HitArea _area = HitArea.Empty;
    private static Action<string> _log = _ => { };
    private static bool _hasReportedFailure;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        try
        {
            harmony.Patch(AccessTools.Method(typeof(WebUILayer), "ValidateRaycast"), postfix: new HarmonyMethod(typeof(ClickRouting), nameof(RouteClicksToToasts)));
            log("routing clicks to toasts");
        }
        catch (Exception ex)
        {
            log("could not route clicks to toasts, so clicking one does nothing and the click goes to the game: " + ex.Message);
        }
    }

    public static void SetArea(HitArea area) => _area = area;

    /// <summary>Main thread only, like the raycasts that read it.</summary>
    public static void Capture(string window, bool isCapturing)
    {
        if (isCapturing) Capturers.Add(window);
        else Capturers.Remove(window);
    }

    /// <summary>Fails soft: any error leaves the game's own answer standing.</summary>
    private static void RouteClicksToToasts(WebUILayer __instance, Vector2 screenPoint, Camera eventCamera, ref bool __result)
    {
        var area = _area;

        if (__result) return;

        if (Capturers.Count > 0)
        {
            __result = true;

            return;
        }

        if (area.IsEmpty) return;

        try
        {
            var rect = __instance.cachedStaticMask?.Rect;

            if (rect == null) return;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screenPoint, eventCamera, out var local)) return;

            var size = rect.rect;
            var pivot = rect.pivot;

            if (area.ContainsLocalPoint(local.x, local.y, size.width, size.height, pivot.x, pivot.y)) __result = true;
        }
        catch (Exception ex)
        {
            ReportFailureOnce(ex);
        }
    }

    private static void ReportFailureOnce(Exception ex)
    {
        if (_hasReportedFailure) return;

        _hasReportedFailure = true;

        _log("checking a click against the toasts failed (reported once), the game decides where it goes: " + ex.Message);
    }
}
