using System;
using System.Collections.Generic;
using System.IO;
using ExiledToolkit.Core;
using GameCore.HotUpdate.ReduxUI;
using Vuplex.WebView;

namespace ExiledToolkit.Adapters;

/// <summary>
/// The toast stack's half that lives in the game's web UI: toasts.js, run in the root document the game's own pages sit
/// in, above all of them. The web view can reload at any time and lose the script, so it is checked for every couple
/// of seconds and put back when the page says it is missing.
/// </summary>
internal sealed class WebPage
{
    private const float CheckSeconds = 2f;
    private const string ScriptResource = "ExiledToolkit.toasts.js";

    private readonly Action<string> _log;
    private string? _script;
    private float _nextCheckAt;
    private bool _hasReportedRunFailure;

    public WebPage(Action<string> log)
    {
        _log = log;
    }

    /// <summary>Whether the last script run found the web view; false until the first run.</summary>
    public bool HasWebView { get; private set; }

    public void KeepInstalled(float now)
    {
        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;
        Run(ToastScript.Probe);
    }

    public void Install()
    {
        if (Run(Script())) _log("toast script sent to the web UI");
    }

    public void Apply(IReadOnlyList<PageOp> ops)
    {
        if (ops.Count == 0) return;

        Run(ToastScript.Apply(ops));
    }

    private bool Run(string javaScript)
    {
        var webView = GameSingleton.Existing<ReduxUISystem>()?.GetWebUILayer()?.canvasWebViewPrefab?.WebView;
        HasWebView = webView != null;

        if (webView == null) return false;

        try
        {
            webView.ExecuteJavaScript(javaScript, null);

            return true;
        }
        catch (Exception ex)
        {
            if (!_hasReportedRunFailure) _log("could not run script in the web UI (reported once): " + ex.Message);

            _hasReportedRunFailure = true;

            return false;
        }
    }

    private string Script()
    {
        if (_script != null) return _script;

        using var stream = typeof(WebPage).Assembly.GetManifestResourceStream(ScriptResource) ?? throw new InvalidOperationException(ScriptResource + " is not embedded");
        using var reader = new StreamReader(stream);

        return _script = reader.ReadToEnd();
    }
}
