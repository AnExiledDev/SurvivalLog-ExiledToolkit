using System;
using System.Collections.Generic;
using System.IO;
using ExiledToolkit.Core;

namespace ExiledToolkit.Adapters;

/// <summary>
/// A Toolkit script that lives in the game's web UI (toasts.js, menu.js), run in the root document the game's own
/// pages sit in, above all of them. The web view can reload at any time and lose the script, so it is checked for
/// every couple of seconds and put back when the page says it is missing.
/// </summary>
internal sealed class WebPage
{
    private const float CheckSeconds = 2f;
    private readonly Action<string> _log;
    private readonly string _resource;
    private readonly string _probe;
    private readonly string _name;
    private string? _script;
    private float _nextCheckAt;
    private bool _hasReportedRunFailure;

    /// <param name="probe">Script that is silent while the page script is installed and reports "noscript" when it is not.</param>
    public WebPage(Action<string> log, string resource, string probe, string name)
    {
        _log = log;
        _resource = resource;
        _probe = probe;
        _name = name;
    }

    /// <summary>Whether the last script run found the web view; false until the first run.</summary>
    public bool HasWebView { get; private set; }

    public void KeepInstalled(float now)
    {
        if (now < _nextCheckAt) return;

        _nextCheckAt = now + CheckSeconds;
        Run(_probe);
    }

    public void Install()
    {
        if (Run(Script())) _log(_name + " script sent to the web UI");
    }

    public void Apply(IReadOnlyList<PageOp> ops)
    {
        if (ops.Count == 0) return;

        Run(ToastScript.Apply(ops));
    }

    /// <summary>False when the web view is missing or the script failed.</summary>
    public bool Send(string javaScript) => Run(javaScript);

    private bool Run(string javaScript)
    {
        var hasRun = WebUi.TryRun(javaScript, out var failure);
        HasWebView = hasRun || failure != null;

        if (failure != null && !_hasReportedRunFailure) _log($"could not run the {_name} script in the web UI (reported once): " + failure.Message);

        _hasReportedRunFailure |= failure != null;

        return hasRun;
    }

    private string Script()
    {
        if (_script != null) return _script;

        using var stream = typeof(WebPage).Assembly.GetManifestResourceStream(_resource) ?? throw new InvalidOperationException(_resource + " is not embedded");
        using var reader = new StreamReader(stream);

        return _script = reader.ReadToEnd();
    }
}
