using ExiledToolkit.Web;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// A mod's page script kept in the game's web view: put in from the mod's own assembly, checked for with a tiny probe
/// on a steady beat, put back when the page lost it, its words sent only when the page lacks them, and a broken web
/// view reported once rather than every frame.
/// </summary>
public class WebPageTests
{
    private const string Resource = "ExiledToolkit.dock.js";
    private const string Extra = "ExiledToolkit.phone.js";

    private readonly List<string> _runs = new();
    private readonly List<string> _log = new();
    private bool _hasWebView = true;
    private Exception? _failure;
    private bool _isChinese;

    [Fact]
    public void Install_sends_the_mods_embedded_script_and_the_ones_that_travel_with_it()
    {
        var page = new WebPage(typeof(WebPageTests).Assembly, Resource, "window.__sltest", "slmod|test|noscript", _log.Add, Run, null) { ExtraResources = new[] { Extra } };

        Assert.True(page.Install());

        Assert.Equal(Embedded(Resource) + "\n;" + Embedded(Extra), _runs.Single());
        Assert.Equal(new[] { "page script sent to the web UI" }, _log);
    }

    [Fact]
    public void The_probe_is_silent_while_the_script_is_there_and_asks_for_it_when_it_is_not()
    {
        var page = Page();

        Assert.Equal("window.__sltest||window.vuplex&&window.vuplex.postMessage(\"slmod|test|noscript\")", page.Probe);
    }

    [Fact]
    public void The_probe_goes_on_a_steady_beat_from_the_first_check()
    {
        var page = Page();

        Assert.True(page.KeepInstalled(10.0));
        Assert.False(page.KeepInstalled(11.9));
        Assert.True(page.KeepInstalled(12.3));
        Assert.True(page.KeepInstalled(14.0));
        Assert.False(page.KeepInstalled(15.9));

        Assert.All(_runs, run => Assert.Equal(page.Probe, run));
    }

    [Fact]
    public void An_offset_keeps_the_first_probe_off_the_frames_of_the_mods_other_timers()
    {
        var page = new WebPage(typeof(WebPageTests).Assembly, Resource, "window.__sltest", "slmod|test|noscript", _log.Add, Run, () => _isChinese) { CheckOffset = 0.6 };

        Assert.False(page.KeepInstalled(10.0));
        Assert.True(page.KeepInstalled(10.6));
        Assert.False(page.KeepInstalled(12.5));
        Assert.True(page.KeepInstalled(12.6));
    }

    [Fact]
    public void A_long_stall_starts_the_beat_again_instead_of_probing_every_frame_to_catch_up()
    {
        var page = Page();

        page.KeepInstalled(0);

        Assert.True(page.KeepInstalled(30));
        Assert.False(page.KeepInstalled(30.1));
        Assert.True(page.KeepInstalled(32));
    }

    [Fact]
    public void A_call_runs_on_the_script_when_it_is_there_and_asks_for_it_when_not()
    {
        var page = Page();

        Assert.True(page.Call("open({\"a\":1})"));

        Assert.Equal("window.__sltest?window.__sltest.open({\"a\":1}):" + page.NoScriptReply, _runs.Single());
    }

    [Fact]
    public void The_words_go_with_the_first_call_and_again_only_when_the_language_changes()
    {
        var page = Page(words: isChinese => isChinese ? "W.zh;" : "W.en;");

        page.Call("a()");
        page.Call("b()");
        _isChinese = true;
        page.Call("c()");

        Assert.StartsWith("W.en;window.__sltest?", _runs[0]);
        Assert.StartsWith("window.__sltest?", _runs[1]);
        Assert.StartsWith("W.zh;window.__sltest?", _runs[2]);
    }

    [Fact]
    public void A_reinstalled_page_gets_its_words_again()
    {
        var page = Page(words: _ => "W;");

        page.Call("a()");
        page.Install();
        page.Call("b()");
        page.ForgetWords();
        page.Call("c()");

        Assert.StartsWith("W;", _runs[2]);
        Assert.StartsWith("W;", _runs[3]);
    }

    [Fact]
    public void Words_that_did_not_reach_the_page_go_with_the_next_call()
    {
        var page = Page(words: _ => "W;");

        _hasWebView = false;
        Assert.False(page.Call("a()"));
        _hasWebView = true;
        page.Call("b()");

        Assert.StartsWith("W;", _runs.Last());
    }

    [Fact]
    public void A_script_that_fails_is_reported_once_however_often_it_runs()
    {
        var page = Page();

        _failure = new InvalidOperationException("web view gone");

        Assert.False(page.Run("x()"));
        Assert.False(page.Run("y()"));

        Assert.Equal(new[] { "could not run the page script in the web UI (reported once): web view gone" }, _log);
        Assert.True(page.HasWebView);
    }

    [Fact]
    public void No_web_view_yet_is_not_a_failure()
    {
        var page = Page();

        _hasWebView = false;

        Assert.False(page.Install());
        Assert.False(page.HasWebView);
        Assert.Empty(_log);
    }

    [Fact]
    public void A_script_the_mod_did_not_embed_says_so()
    {
        var page = new WebPage(typeof(WebPageTests).Assembly, "Nope.page.js", "window.__sltest", "slmod|test|noscript", _log.Add, Run, null);

        var error = Assert.Throws<InvalidOperationException>(() => page.Install());

        Assert.Contains("Nope.page.js", error.Message);
    }

    private WebPage Page(Func<bool, string>? words = null) =>
        new(typeof(WebPageTests).Assembly, Resource, "window.__sltest", "slmod|test|noscript", _log.Add, Run, () => _isChinese) { Words = words };

    private bool Run(string javaScript, out Exception? failure)
    {
        failure = _failure;

        if (failure != null || !_hasWebView) return false;

        _runs.Add(javaScript);

        return true;
    }

    private static string Embedded(string name)
    {
        using var stream = typeof(WebPageTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the tests");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
