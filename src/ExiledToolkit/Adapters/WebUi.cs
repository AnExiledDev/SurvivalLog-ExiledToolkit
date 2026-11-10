using System;
using ExiledToolkit.Game;
using ExiledToolkit.Kit;
using GameCore.HotUpdate.ReduxUI;
using Vuplex.WebView;

namespace ExiledToolkit.Web;

/// <summary>
/// Runs script in the root document of the game's one web view, where every game page sits as an iframe. Main thread
/// only. Toolkit 0.19.0 and newer (Mod Manager used it before); the two that hand back the script's result, 0.20.0 and
/// newer.
/// </summary>
public static class WebUi
{
    /// <summary>False when there is no web view yet; a script error is the caller's to report, so it is passed back.</summary>
    public static bool TryRun(string javaScript, out Exception? failure) => Run(View(), javaScript, null, out failure);

    /// <summary>
    /// The same, and the script's result goes to <paramref name="replies"/> under a new call number, so an answer that
    /// comes after the next call went out is dropped. Read it with <see cref="ReplySlot.Take"/> on a later frame. A
    /// result of null is offered as "". No call is opened when there is no web view.
    /// </summary>
    public static bool TryRun(string javaScript, ReplySlot replies, out Exception? failure)
    {
        if (replies == null) throw new ArgumentNullException(nameof(replies));

        var webView = View();

        if (webView == null) return Run(null, javaScript, null, out failure);

        var call = replies.Open();

        return Run(webView, javaScript, (Action<string>)(result => replies.Offer(call, result ?? "")), out failure);
    }

    /// <summary>
    /// The same with a callback of the mod's own for the result, for a mod that sends often: make the callback once and
    /// pass it every time, and a send costs the script alone.
    /// </summary>
    public static bool TryRun(string javaScript, Il2CppSystem.Action<string> onResult, out Exception? failure) =>
        Run(View(), javaScript, onResult ?? throw new ArgumentNullException(nameof(onResult)), out failure);

    private static IWebView? View() => GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

    private static bool Run(IWebView? webView, string javaScript, Il2CppSystem.Action<string>? onResult, out Exception? failure)
    {
        failure = null;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(javaScript, onResult);

            return true;
        }
        catch (Exception ex)
        {
            failure = ex;

            return false;
        }
    }
}
