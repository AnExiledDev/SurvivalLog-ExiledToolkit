using System;
using HarmonyLib;
using Vuplex.WebView.Internal;

namespace ExiledToolkit.Web;

/// <summary>The one Harmony prefix on the game's web message handler. Static because a Harmony prefix has nowhere else to look.</summary>
public static partial class PageInbox
{
    internal static void Apply(Harmony harmony, Action<string> log)
    {
        UseLog(log);

        try
        {
            harmony.Patch(AccessTools.Method(typeof(BaseWebView), "HandleMessageEmitted"), prefix: new HarmonyMethod(typeof(PageInbox), nameof(TakeOurs)));
            IsListening = true;
            log("listening for page messages");
        }
        catch (Exception ex)
        {
            log("could not hook page messages, so toasts will not show and mod windows will not answer clicks (mods are still told about toasts): " + ex.Message);
        }
    }

    /// <summary>A throw here would break every web message in the game, so any failure lets the game handle the message.</summary>
    private static bool TakeOurs(string serializedMessage)
    {
        try
        {
            return !Route(serializedMessage);
        }
        catch (Exception ex)
        {
            ReportRouteFailureOnce(ex);

            return true;
        }
    }
}
