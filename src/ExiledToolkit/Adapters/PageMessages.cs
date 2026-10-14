using System;
using System.Collections.Concurrent;
using ExiledToolkit.Core;
using HarmonyLib;
using Vuplex.WebView.Internal;

namespace ExiledToolkit.Adapters;

/// <summary>
/// Catches the toast page's messages before the game's web layer sees them (it would log them as unknown) and queues
/// them for the next frame. Static because a Harmony prefix has nowhere else to look.
/// </summary>
internal static class PageMessages
{
    private static readonly ConcurrentQueue<PageCommand> Queue = new();
    private static Action<string> _log = _ => { };
    private static bool _hasReportedFailure;

    public static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        try
        {
            harmony.Patch(AccessTools.Method(typeof(BaseWebView), "HandleMessageEmitted"), prefix: new HarmonyMethod(typeof(PageMessages), nameof(TakeOurs)));
            log("listening for toast page messages");
        }
        catch (Exception ex)
        {
            log("could not listen for toast page messages, so toasts will not show (mods are still told about them): " + ex.Message);
        }
    }

    public static bool TryTake(out PageCommand command)
    {
        if (Queue.TryDequeue(out var queued))
        {
            command = queued;

            return true;
        }

        command = new PageCommand.Unknown("");

        return false;
    }

    /// <summary>A throw here would break every web message in the game, so any failure lets the game handle the message.</summary>
    private static bool TakeOurs(string serializedMessage)
    {
        try
        {
            if (!PageCommand.IsOurs(serializedMessage)) return true;

            Queue.Enqueue(PageCommand.Parse(serializedMessage));

            return false;
        }
        catch (Exception ex)
        {
            ReportFailureOnce(ex);

            return true;
        }
    }

    private static void ReportFailureOnce(Exception ex)
    {
        if (_hasReportedFailure) return;

        _hasReportedFailure = true;

        _log("reading a toast page message failed, the game handles it instead: " + ex.Message);
    }
}
