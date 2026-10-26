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

    private bool IsRows(string doc) => _engine.Evaluate($"{doc}.documentElement.querySelector('.app-dock').classList.contains('etk-phone-dock')").AsBoolean();

    private int Number(string expression) => (int)_engine.Evaluate(expression).AsNumber();

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
        Run("nextFrame(); modApp(doc, 'basket', 20); flush(); nextFrame();");

        Assert.Equal("camp,sms,map,guide,basket", Dock("doc"));
    }

    [Fact]
    public void A_phone_page_loaded_into_the_frame_again_is_watched_again()
    {
        LoadScript();
        OpenPhone();
        Run("nextFrame(); navigate(frame, PHONE_URL + '?t=2'); flush();");
        Run("var again = loadPhone(frame); fireLoad(frame); flush(); modApp(again, 'guide', 10); flush(); nextFrame();");

        Assert.Equal("camp,sms,map,guide", Dock("again"));
    }

    [Fact]
    public void A_page_watched_before_the_game_builds_its_home_screen_has_the_dock_laid_out_on_the_first_frame_after()
    {
        LoadScript();
        Run("var frame = openFrame(PHONE_URL); flush(); var doc = loadEmptyPhone(frame); nextFrame();");
        Run("buildHome(doc); modApp(doc, 'guide', 10); flush(); nextFrame();");

        Assert.Equal("camp,sms,map,guide", Dock("doc"));
    }

    [Fact]
    public void Past_four_apps_the_dock_is_laid_out_in_rows_and_four_or_fewer_are_left_to_the_game()
    {
        LoadScript();
        OpenPhone();
        Run("nextFrame();");

        Assert.False(IsRows("doc"));

        Run("modApp(doc, 'basket', 20); flush(); nextFrame();");

        Assert.True(IsRows("doc"));
    }

    [Fact]
    public void Changes_on_the_phone_page_that_cannot_move_an_app_do_not_make_it_look_at_the_page()
    {
        LoadScript();
        OpenPhone();
        Run("nextFrame(); var before = doc.scans;");
        Run("var note = doc.body.appendChild(doc.createElement('div')); note.appendChild(doc.createElement('span')); note.setAttribute('title', 'new message'); flush(); nextFrame();");

        Assert.Equal(0, Number("doc.scans - before"));
    }

    [Fact]
    public void Many_changes_in_one_frame_make_one_look_at_the_page_and_that_look_does_not_wake_itself()
    {
        LoadScript();
        OpenPhone();
        Run("nextFrame(); var before = doc.scans;");
        Run("modApp(doc, 'basket', 20); flush(); modApp(doc, 'notes', 5); flush(); modApp(doc, 'traps', 30); flush(); nextFrame(); nextFrame();");

        Assert.Equal("camp,sms,map,notes,guide,basket,traps", Dock("doc"));
        Assert.Equal(1, Number("doc.scans - before"));
    }

    [Fact]
    public void The_poll_leaves_a_watched_phone_page_to_its_watcher()
    {
        LoadScript();
        OpenPhone();
        Run("modApp(doc, 'basket', 20); modApp(doc, 'notes', 5); flush(); nextFrame(); var scans = doc.scans; var reads = doc.styleReads;");
        Run("tick(); flush(); nextFrame(); tick(); flush(); nextFrame();");

        Assert.Equal(0, Number("doc.scans - scans"));
        Assert.Equal(0, Number("doc.styleReads - reads"));
    }

    [Fact]
    public void A_frame_being_waited_on_is_waited_on_once_however_often_it_is_reported()
    {
        LoadScript();
        Run("var frame = openFrame(PHONE_URL); flush(); frame.changed('src'); flush(); fireLoad(frame); fireLoad(frame); frame.changed('src'); flush();");

        Assert.Equal(1, Number("frameCallbacks.length"));
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
        Run("modApp(doc, 'basket', 20); tick(); flush(); nextFrame();");

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
