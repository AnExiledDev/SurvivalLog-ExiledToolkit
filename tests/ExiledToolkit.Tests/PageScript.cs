using ExiledToolkit.Core;
using Jint;

namespace ExiledToolkit.Tests;

/// <summary>
/// toasts.js exactly as the plugin embeds it, run in Jint on top of FakePage.js. Ops go in as the script WebPage runs,
/// built by the real ToastScript, and what the page posts is parsed with the real PageCommand, so a test sees the
/// commands the plugin would act on.
/// </summary>
internal sealed class PageScript
{
    private readonly Engine _engine = new();

    /// <param name="hasBridge">False loads the script before the vuplex bridge exists, as can happen in the game.</param>
    /// <param name="uiPercent">Puts ui.js in first at this Interface &gt; UiScale, as the plugin does.</param>
    public PageScript(bool hasBridge = true, int? uiPercent = null)
    {
        _engine.Execute(Resource("ExiledToolkit.Tests.FakePage.js"));

        if (hasBridge) _engine.Execute("connectVuplex()");

        if (uiPercent is { } percent)
        {
            _engine.Execute(Resource("ExiledToolkit.ui.js"));
            _engine.Execute(UiSize.SetScript(percent));
        }

        _engine.Execute(Resource("ExiledToolkit.toasts.js"));
        Hear();
    }

    /// <summary>Every command the page sent, oldest first.</summary>
    public List<PageCommand> Sent { get; } = new();

    public void Apply(params PageOp[] ops) => Run(ToastScript.Apply(ops));

    /// <summary>Moves the page's clock on, firing its timers on the way.</summary>
    public void Advance(int milliseconds) => Run($"advance({milliseconds})");

    /// <summary>The page draws its next frame: what it put off until then runs.</summary>
    public void NextFrame() => Run("nextFrame()");

    public void ConnectBridge() => Run("connectVuplex()");

    /// <summary>A click on the box at this place in the stack, top first; on its close button with <paramref name="part"/>.</summary>
    public void Click(int box, string? part = null) => Run(part == null ? $"dispatch(boxes()[{box}], 'click')" : $"dispatch(part(boxes()[{box}], '{part}'), 'click')");

    /// <summary>What each box not on its way out reads, top first: title, then the count badge if one shows.</summary>
    public string[] Labels() => Text("shown().map(label).join('\\n')").Split('\n', StringSplitOptions.RemoveEmptyEntries);

    public int BoxCount() => (int)_engine.Evaluate("boxes().length").AsNumber();

    public void Run(string statements)
    {
        _engine.Execute(statements);
        Hear();
    }

    public string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

    public int Number(string expression) => (int)_engine.Evaluate($"Number({expression})").AsNumber();

    /// <summary>Takes what the page posted since the last look.</summary>
    private void Hear()
    {
        var count = (int)_engine.Evaluate("posted.length").AsNumber();
        var messages = Enumerable.Range(0, count).Select(i => _engine.Evaluate($"posted[{i}]").AsString()).ToList();

        _engine.Execute("posted.length = 0");
        Sent.AddRange(messages.Select(PageCommand.Parse));
    }

    private static string Resource(string name)
    {
        using var stream = typeof(PageScript).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
