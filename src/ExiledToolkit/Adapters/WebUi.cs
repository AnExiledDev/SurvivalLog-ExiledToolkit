using System;
using GameCore.HotUpdate.ReduxUI;

namespace ExiledToolkit.Adapters;

/// <summary>Runs script in the root document of the game's one web view, where every game page sits as an iframe.</summary>
internal static class WebUi
{
    /// <summary>False when there is no web view yet; a script error is the caller's to report, so it is passed back.</summary>
    public static bool TryRun(string javaScript, out Exception? failure)
    {
        failure = null;
        var webView = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(javaScript, null);

            return true;
        }
        catch (Exception ex)
        {
            failure = ex;

            return false;
        }
    }
}
