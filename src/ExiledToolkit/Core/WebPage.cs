using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using ExiledToolkit.Core;

namespace ExiledToolkit.Web;

/// <summary>Runs script in the web view; false when there is none. A script error is passed back for the caller to report.</summary>
internal delegate bool ScriptRunner(string javaScript, out Exception? failure);

/// <summary>
/// A script a mod keeps in the game's web UI: its embedded page.js, run in the root document the game's own pages sit
/// in, above all of them. The web view can reload at any time and lose the script, so a tiny probe asks every couple
/// of seconds whether it is still there; the page answers with the mod's "noscript" message when it is not, and the mod
/// calls <see cref="Install"/> again. Main thread only. Toolkit 0.19.0 and newer.
/// </summary>
public sealed class WebPage
{
    /// <summary>The Toolkit puts the game's web view here at load; until then nothing runs.</summary>
    internal static ScriptRunner DefaultRunner = NoWebView;

    /// <summary>The Toolkit puts the game's language here at load; until then it reads as English.</summary>
    internal static Func<bool> DefaultIsChinese = () => false;

    private readonly Assembly _assembly;
    private readonly string _resource;
    private readonly Action<string> _log;
    private readonly ScriptRunner? _run;
    private readonly Func<bool>? _isChinese;
    private string? _script;
    private double _nextCheckAt = double.NaN;
    private bool? _wordsSentIn;
    private bool _hasReportedRunFailure;

    /// <param name="assembly">The mod's own assembly, which embeds the script: <c>typeof(Plugin).Assembly</c>.</param>
    /// <param name="resource">The script's manifest resource name, such as "FarmHub.page.js".</param>
    /// <param name="global">The object the script puts on the page, such as "window.__slfarmhub". The probe and every <see cref="Call"/> check it first.</param>
    /// <param name="noScriptMessage">What the page sends when the script is missing, such as "slmod|farmhub|noscript".</param>
    /// <param name="log">The mod's log, for the install line and a run failure (once).</param>
    public WebPage(Assembly assembly, string resource, string global, string noScriptMessage, Action<string> log)
        : this(assembly, resource, global, noScriptMessage, log, null, null)
    {
    }

    /// <summary>For tests: a fake web view and language.</summary>
    internal WebPage(Assembly assembly, string resource, string global, string noScriptMessage, Action<string> log, ScriptRunner? run, Func<bool>? isChinese)
    {
        if (string.IsNullOrEmpty(global)) throw new ArgumentException("the page script's global object is needed", nameof(global));
        if (string.IsNullOrEmpty(noScriptMessage)) throw new ArgumentException("the page's noscript message is needed", nameof(noScriptMessage));

        _assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        _resource = resource ?? throw new ArgumentNullException(nameof(resource));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _run = run;
        _isChinese = isChinese;
        Global = global;
        NoScriptReply = "window.vuplex&&window.vuplex.postMessage(" + JsText.Literal(noScriptMessage) + ")";
        Probe = Global + "||" + NoScriptReply;
    }

    /// <summary>The Toolkit's own scripts, with a probe of their own; an empty probe is never sent.</summary>
    internal WebPage(Action<string> log, string resource, string probe, string name)
    {
        _assembly = typeof(WebPage).Assembly;
        _resource = resource;
        _log = log;
        Global = "";
        NoScriptReply = "";
        Probe = probe;
        Name = name;
    }

    /// <summary>What the install line in the log calls the script: "&lt;Name&gt; script sent to the web UI".</summary>
    public string Name { get; init; } = "page";

    /// <summary>Seconds between probes. Default: 2.</summary>
    public double CheckSeconds { get; init; } = 2;

    /// <summary>Seconds after the first <see cref="KeepInstalled"/> call before the first probe, to keep it off the frames of the mod's other timers.</summary>
    public double CheckOffset { get; init; }

    /// <summary>
    /// More embedded scripts that travel with the first one, in this order, so a reloaded page gets all of them back at
    /// once.
    /// </summary>
    public IReadOnlyList<string> ExtraResources { get; init; } = Array.Empty<string>();

    /// <summary>
    /// The script that hands the page its words, in Chinese when given true. When set, a <see cref="Call"/> runs it first
    /// whenever the page lacks the words or holds the other language's, so a change of the game's language shows at the
    /// next call without resending the words with every call.
    /// </summary>
    public Func<bool, string>? Words { get; init; }

    public string Global { get; }

    /// <summary>Script that sends the noscript message, for a mod's own calls that check <see cref="Global"/> themselves.</summary>
    public string NoScriptReply { get; }

    /// <summary>Silent while the script is installed; sends the noscript message when it is not.</summary>
    public string Probe { get; }

    /// <summary>Whether the last script run found the web view; false until the first run.</summary>
    public bool HasWebView { get; private set; }

    /// <summary>Sends the probe when it is due. True when it was sent. <paramref name="now"/> is any clock in seconds, such as <c>Clock.Now</c>.</summary>
    public bool KeepInstalled(double now)
    {
        if (Probe.Length == 0) return false;

        if (double.IsNaN(_nextCheckAt)) _nextCheckAt = now + CheckOffset;

        if (now < _nextCheckAt) return false;

        // On its own beat, so probes started together stay apart; a long stall starts the beat again.
        _nextCheckAt += CheckSeconds;

        if (_nextCheckAt <= now) _nextCheckAt = now + CheckSeconds;

        Run(Probe);

        return true;
    }

    /// <summary>Sends the whole script. A page that needed it back has lost its words too, so they go with the next call.</summary>
    public bool Install()
    {
        _wordsSentIn = null;

        if (!Run(Script())) return false;

        _log(Name + " script sent to the web UI");

        return true;
    }

    /// <summary>The page said it installed itself afresh, so it holds no words yet.</summary>
    public void ForgetWords() => _wordsSentIn = null;

    /// <summary>
    /// Calls <c>Global.expression</c> when the script is there, and asks for the script when it is not. The words go
    /// first when they are due (see <see cref="Words"/>). False when the web view is missing or the script failed.
    /// </summary>
    public bool Call(string expression)
    {
        var isChinese = Words != null && (_isChinese ?? DefaultIsChinese)();
        var words = Words == null || _wordsSentIn == isChinese ? "" : Words(isChinese);

        if (!Run(words + Global + "?" + Global + "." + expression + ":" + NoScriptReply)) return false;

        if (Words != null) _wordsSentIn = isChinese;

        return true;
    }

    /// <summary>Runs any script in the root document. False when the web view is missing or the script failed; a failure is logged once.</summary>
    public bool Run(string javaScript)
    {
        var hasRun = (_run ?? DefaultRunner)(javaScript, out var failure);

        HasWebView = hasRun || failure != null;

        if (failure != null) ReportRunFailureOnce(failure);

        return hasRun;
    }

    internal bool Send(string javaScript) => Run(javaScript);

    /// <summary>One frame's toast changes.</summary>
    internal void Apply(IReadOnlyList<PageOp> ops)
    {
        if (ops.Count == 0) return;

        Run(ToastScript.Apply(ops));
    }

    private void ReportRunFailureOnce(Exception failure)
    {
        if (_hasReportedRunFailure) return;

        _hasReportedRunFailure = true;
        _log($"could not run the {Name} script in the web UI (reported once): " + failure.Message);
    }

    private string Script()
    {
        if (_script != null) return _script;

        var script = Resource(_resource);

        foreach (var extra in ExtraResources) script += "\n;" + Resource(extra);

        return _script = script;
    }

    private string Resource(string name)
    {
        using var stream = _assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in " + _assembly.GetName().Name);
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }

    private static bool NoWebView(string javaScript, out Exception? failure)
    {
        failure = null;

        return false;
    }
}
