using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// phone.js exactly as the plugin embeds it, run in Jint on a fake root page. The game builds a new phone page each
/// time the phone opens and shows it two frames later; a mod's app must be in the dock by then, not on the next poll.
/// None of these tests run the poll unless they say so.
/// </summary>
public class PhoneTests
{
    private readonly Engine _engine = new();

    public PhoneTests()
    {
        _engine.Execute(Resource("ExiledToolkit.Tests.PhoneFake.js"));
    }

    private void Run(string statements) => _engine.Execute(statements);

    private string Dock(string doc) => _engine.Evaluate($"dockNames({doc})").AsString();

    private void LoadScript() => Run(Resource("ExiledToolkit.phone.js"));

    /// <summary>The phone opening: Root makes the frame, the page arrives a frame later, and a mod adds its app as the page is built.</summary>
    private void OpenPhone()
    {
        Run("var frame = openFrame(PHONE_URL); flush(); nextFrame();");
        Run("var doc = loadPhone(frame); modApp(doc, 'guide', 10); flush();");
    }

    [Fact]
    public void A_mod_app_on_a_phone_page_built_after_start_is_in_the_dock_by_the_next_frame_without_a_poll()
    {
        LoadScript();
        OpenPhone();
        Run("nextFrame();");

        Assert.Equal("camp,sms,map,guide", Dock("doc"));
    }

    [Fact]
    public void An_app_a_mod_adds_once_the_page_is_watched_goes_into_the_dock_before_the_next_paint()
    {
        LoadScript();
        OpenPhone();
        Run("nextFrame(); modApp(doc, 'basket', 20); flush();");

        Assert.Equal("camp,sms,map,guide,basket", Dock("doc"));
    }

    [Fact]
    public void A_phone_page_loaded_into_the_frame_again_is_watched_again()
    {
        LoadScript();
        OpenPhone();
        Run("nextFrame(); navigate(frame, PHONE_URL + '?t=2'); flush();");
        Run("var again = loadPhone(frame); fireLoad(frame); flush(); modApp(again, 'guide', 10); flush();");

        Assert.Equal("camp,sms,map,guide", Dock("again"));
    }

    [Fact]
    public void Marked_apps_follow_the_games_own_lowest_order_first_whatever_order_the_mods_added_them()
    {
        LoadScript();
        OpenPhone();
        Run("modApp(doc, 'notes', 5); modApp(doc, 'basket', 20); flush(); nextFrame();");

        Assert.Equal("camp,sms,map,notes,guide,basket", Dock("doc"));
    }

    [Fact]
    public void A_phone_page_that_was_open_before_the_script_started_is_still_arranged_by_the_poll()
    {
        Run("var frame = openFrame(PHONE_URL); var doc = loadPhone(frame); modApp(doc, 'guide', 10);");
        LoadScript();
        Run("modApp(doc, 'basket', 20); tick(); flush();");

        Assert.Equal("camp,sms,map,guide,basket", Dock("doc"));
    }

    [Fact]
    public void A_game_page_that_is_not_the_phone_is_left_alone()
    {
        LoadScript();
        Run("var hud = openFrame('file:///WebUI/UI/CoreUI1/CoreUI1.html'); flush(); var page = loadPhone(hud); modApp(page, 'guide', 10); fireLoad(hud); flush(); nextFrame(); tick(); flush();");

        Assert.Equal("camp,sms,map", Dock("page"));
    }

    [Fact]
    public void Loading_the_script_twice_keeps_one_poll()
    {
        LoadScript();
        LoadScript();

        Assert.Equal(1, (int)_engine.Evaluate("intervals.length").AsNumber());
    }

    private static string Resource(string name)
    {
        using var stream = typeof(PhoneTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
