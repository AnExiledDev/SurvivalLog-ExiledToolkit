using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// api.js, the page API mods' page scripts call (window.ExiledToolkit), exactly as the plugin embeds it, run in Jint on
/// the fake root page (PhoneFake.js with ApiFake.js): frames whose document is swapped when a page loads, and an
/// interval the tests fire by hand with tick().
/// </summary>
public class PageApiTests
{
    private readonly Engine _engine = new();

    public PageApiTests()
    {
        _engine.Execute(Resource("ExiledToolkit.Tests.PhoneFake.js"));
        _engine.Execute(Resource("ExiledToolkit.Tests.ApiFake.js"));
    }

    private void LoadApi() => _engine.Execute(Resource("ExiledToolkit.api.js"));

    private void Run(string statements) => _engine.Execute(statements);

    private string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

    private int Number(string expression) => (int)_engine.Evaluate($"Number({expression})").AsNumber();

    private bool IsTrue(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    // ===== Load order =====

    /// <summary>The line every mod uses to wait for the Toolkit, whichever script the game ran first.</summary>
    private const string WhenReady = "(window.ExiledToolkitReady = window.ExiledToolkitReady || []).push";

    [Fact]
    public void A_mod_script_that_runs_before_the_toolkit_starts_once_the_toolkit_loads_with_the_api()
    {
        Run($"var started = []; {WhenReady}(function (etk) {{ started.push(typeof etk.addHudButton); }});");

        Assert.Equal(0, Number("started.length"));

        LoadApi();

        Assert.Equal("function", Text("started.join(',')"));
    }

    [Fact]
    public void A_mod_script_that_runs_after_the_toolkit_starts_at_once_with_the_api()
    {
        LoadApi();

        Run($"var started = []; {WhenReady}(function (etk) {{ started.push(etk === window.ExiledToolkit); }});");

        Assert.Equal("true", Text("started.join(',')"));
    }

    [Fact]
    public void Mods_waiting_start_in_the_order_they_queued_and_one_that_throws_does_not_stop_the_next()
    {
        Run($"var started = []; {WhenReady}(function () {{ started.push('a'); }}); {WhenReady}(function () {{ throw new Error('broken mod'); }}); {WhenReady}(function () {{ started.push('c'); }});");

        LoadApi();

        Assert.Equal("a,c", Text("started.join(',')"));
    }

    [Fact]
    public void The_toolkit_script_sent_again_keeps_the_api_mods_already_hold()
    {
        LoadApi();
        Run("var first = window.ExiledToolkit;");

        LoadApi();

        Assert.True(IsTrue("window.ExiledToolkit === first"));
    }

    // ===== send =====

    [Fact]
    public void Send_posts_the_mods_prefix_and_message_to_its_csharp_side()
    {
        Run("connectVuplex();");
        LoadApi();

        var isSent = IsTrue("ExiledToolkit.send('fh|', 'toggle')");

        Assert.True(isSent);
        Assert.Equal("fh|toggle", Text("posted.join(',')"));
    }

    [Fact]
    public void Send_without_the_bridge_says_so_instead_of_throwing()
    {
        LoadApi();

        Assert.False(IsTrue("ExiledToolkit.send('fh|', 'toggle')"));
    }

    // ===== addStyle =====

    [Fact]
    public void A_style_added_twice_is_in_the_page_once_and_takes_the_newer_text()
    {
        LoadApi();
        Run("var doc = makeDocument('file:///WebUI/UI/CoreUI1/CoreUI1.html'); ExiledToolkit.addStyle(doc, 'fh-style', '.a{}'); ExiledToolkit.addStyle(doc, 'fh-style', '.b{}');");

        Assert.Equal(1, Number("doc.head.children.filter(function (el) { return el.id === 'fh-style'; }).length"));
        Assert.Equal(".b{}", Text("doc.getElementById('fh-style').textContent"));
    }

    [Fact]
    public void A_style_taken_out_of_the_page_is_put_back()
    {
        LoadApi();
        Run("var doc = makeDocument('file:///x.html'); var first = ExiledToolkit.addStyle(doc, 's', '.a{}'); doc.head.removeChild(first); ExiledToolkit.addStyle(doc, 's', '.a{}');");

        Assert.True(IsTrue("doc.getElementById('s') && doc.getElementById('s').isConnected"));
    }

    // ===== watchPages =====

    private void WatchStations(string options = "undefined") =>
        Run($"var seen = []; var watch = ExiledToolkit.watchPages(/ToolTable\\.html/, function (doc) {{ seen.push(doc.id); return doc.answer; }}, {options});");

    private void OpenStation() => Run("var station = openFrame('file:///WebUI/UI/ToolTable/ToolTable.html');");

    private void LoadStation(string id) => Run($"loadPage(station, function (doc) {{ doc.id = '{id}'; }});");

    [Fact]
    public void A_page_is_handed_over_once_when_it_loads_and_again_when_its_frame_loads_a_new_one()
    {
        LoadApi();
        WatchStations();
        OpenStation();

        Run("tick();");
        LoadStation("first");
        Run("tick(); tick();");
        LoadStation("second");
        Run("tick(); tick();");

        Assert.Equal("first,second", Text("seen.join(',')"));
    }

    [Fact]
    public void Only_pages_whose_address_matches_are_handed_over()
    {
        LoadApi();
        WatchStations();

        Run("var other = openFrame('file:///WebUI/UI/BackpackUI/BackpackUI.html'); loadPage(other, function (doc) { doc.id = 'bag'; }); tick();");

        Assert.Equal("", Text("seen.join(',')"));
    }

    [Fact]
    public void A_page_already_shown_when_the_watch_starts_is_handed_over_at_once()
    {
        LoadApi();
        OpenStation();
        LoadStation("open");

        WatchStations();

        Assert.Equal("open", Text("seen.join(',')"));
    }

    [Fact]
    public void A_watcher_that_answers_false_is_asked_again_each_check_until_it_has_what_it_looked_for()
    {
        LoadApi();
        OpenStation();
        Run("loadPage(station, function (doc) { doc.id = 'p'; doc.answer = false; });");
        WatchStations();

        Run("tick(); tick();");
        Run("station.contentDocument.answer = true; tick(); tick(); tick();");

        Assert.Equal("p,p,p,p", Text("seen.join(',')"));
    }

    [Fact]
    public void Every_check_hands_the_page_over_each_time_for_a_mod_that_keeps_something_in_it()
    {
        LoadApi();
        OpenStation();
        LoadStation("p");
        WatchStations("{ everyCheck: true }");

        Run("tick(); tick();");

        Assert.Equal("p,p,p", Text("seen.join(',')"));
    }

    [Fact]
    public void A_watcher_that_throws_is_asked_again_and_does_not_stop_the_others()
    {
        LoadApi();
        OpenStation();
        LoadStation("p");
        Run("var broken = 0; ExiledToolkit.watchPages(/ToolTable\\.html/, function () { broken++; throw new Error('reshaped page'); });");
        WatchStations();

        Run("tick();");

        Assert.Equal(2, Number("broken"));
        Assert.Equal("p", Text("seen.join(',')"));
    }

    [Fact]
    public void Refresh_hands_over_every_page_shown_now_and_stop_ends_the_watch()
    {
        LoadApi();
        OpenStation();
        LoadStation("p");
        WatchStations();

        Run("watch.refresh(); watch.stop(); watch.refresh();");
        LoadStation("q");
        Run("tick();");

        Assert.Equal("p,p", Text("seen.join(',')"));
    }

    [Fact]
    public void All_mods_watching_pages_share_one_interval()
    {
        LoadApi();

        Run("ExiledToolkit.watchPages(/a/, function () {}); ExiledToolkit.watchPages(/b/, function () {}); ExiledToolkit.addHudButton({ id: 'th-hud-btn' });");

        Assert.Equal(1, Number("intervals.length"));
    }

    // ===== HUD buttons =====

    private void OpenHud() => Run($"var hud = openFrame(COREUI1); var page = loadPage(hud, buildColumn);");

    private const string TrapHub = "{ id: 'th-hud-btn', order: 10, icon: 'M2 2h20v20H2z', title: 'Trap Hub', tip: 'Your traps', key: 'F8', onClick: function (e) { clicks++; } }";

    private void AddTrapHub(string options = TrapHub) => Run($"var clicks = 0; var trap = ExiledToolkit.addHudButton({options});");

    [Fact]
    public void A_hud_button_goes_on_top_of_the_tool_column_drawn_like_the_games_with_its_dock_order()
    {
        LoadApi();
        OpenHud();

        AddTrapHub();

        Assert.Equal("th-hud-btn,phone,bag", Text("columnNames(page)"));
        Assert.Equal("10", Text("find(page, 'th-hud-btn').getAttribute('data-etk-dock')"));
        Assert.True(IsTrue("find(page, 'th-hud-btn').classList.contains('ui-btn') && find(page, 'th-hud-btn').classList.contains('icon-sq')"));
        Assert.Equal("M2 2h20v20H2z", Text("find(page, 'th-hud-btn').children[0].children[0].getAttribute('d')"));
        Assert.Equal("Trap Hub", Text("find(page, 'entry-tip-title').textContent"));
        Assert.Equal("F8", Text("find(page, 'entry-tip-key').textContent"));
        Assert.Equal("Your traps", Text("find(page, 'entry-tip-body').textContent"));
    }

    [Fact]
    public void A_click_on_the_button_reaches_the_mod_and_not_the_games_own_handlers()
    {
        LoadApi();
        OpenHud();
        AddTrapHub();

        var isStopped = IsTrue("click(find(page, 'th-hud-btn'))");

        Assert.True(isStopped);
        Assert.Equal(1, Number("clicks"));
    }

    [Fact]
    public void A_button_added_before_the_hud_page_loads_goes_in_once_it_does()
    {
        LoadApi();
        AddTrapHub();
        Run("var hud = openFrame(COREUI1); tick(); var page = loadPage(hud, buildColumn);");

        Run("tick();");

        Assert.Equal("th-hud-btn,phone,bag", Text("columnNames(page)"));
    }

    [Fact]
    public void The_button_is_put_back_when_the_hud_page_reloads_and_when_the_game_draws_its_column_again()
    {
        LoadApi();
        OpenHud();
        AddTrapHub();

        Run("navigate(hud, COREUI1); tick(); page = loadPage(hud, buildColumn); tick();");
        var afterReload = Text("columnNames(page)");
        Run("rebuildColumn(page); tick();");

        Assert.Equal("th-hud-btn,phone,bag", afterReload);
        Assert.Equal("th-hud-btn,phone,bag", Text("columnNames(page)"));
    }

    [Fact]
    public void A_check_with_the_button_still_in_its_column_searches_nothing()
    {
        LoadApi();
        OpenHud();
        AddTrapHub();
        Run("var scansBefore = page.scans;");

        Run("tick(); tick(); tick();");

        Assert.Equal(0, Number("page.scans - scansBefore"));
    }

    [Fact]
    public void Update_writes_the_badge_tip_and_key_into_the_same_button_without_building_a_new_one()
    {
        LoadApi();
        OpenHud();
        AddTrapHub();
        Run("var placed = find(page, 'th-hud-btn');");

        Run("trap.update({ badge: 3, tip: 'Two catches', key: 'F9' }); tick();");
        var three = Text("find(page, 'etk-hud-badge').textContent");
        Run("trap.update({ badge: 150 });");
        var many = Text("find(page, 'etk-hud-badge').textContent");
        Run("trap.update({ badge: 0 });");

        Assert.True(IsTrue("find(page, 'th-hud-btn') === placed"));
        Assert.Equal(("3", "99+"), (three, many));
        Assert.False(IsTrue("find(page, 'etk-hud-badge')"));
        Assert.Equal("Two catches", Text("find(page, 'entry-tip-body').textContent"));
        Assert.Equal("F9", Text("find(page, 'entry-tip-key').textContent"));
    }

    [Fact]
    public void An_empty_key_takes_the_keycap_out_of_the_tip()
    {
        LoadApi();
        OpenHud();
        AddTrapHub();

        Run("trap.update({ key: '' });");

        Assert.False(IsTrue("find(page, 'entry-tip-key')"));
    }

    [Fact]
    public void A_button_not_to_be_shown_leaves_the_column_and_comes_back_when_shown_again()
    {
        LoadApi();
        OpenHud();
        AddTrapHub();

        Run("trap.update({ shown: false }); tick();");
        var hidden = Text("columnNames(page)");
        Run("trap.update({ shown: true });");

        Assert.Equal("phone,bag", hidden);
        Assert.Equal("th-hud-btn,phone,bag", Text("columnNames(page)"));
    }

    [Fact]
    public void A_button_added_hidden_stays_out_until_shown()
    {
        LoadApi();
        OpenHud();

        AddTrapHub("{ id: 'th-hud-btn', shown: false }");
        Run("tick();");

        Assert.Equal("phone,bag", Text("columnNames(page)"));
    }

    [Fact]
    public void Remove_takes_the_button_out_for_good()
    {
        LoadApi();
        OpenHud();
        AddTrapHub();

        Run("trap.remove(); rebuildColumn(page); tick();");

        Assert.Equal("phone,bag", Text("columnNames(page)"));
    }

    [Fact]
    public void Adding_a_button_with_an_id_in_use_replaces_it()
    {
        LoadApi();
        OpenHud();
        AddTrapHub();

        Run("ExiledToolkit.addHudButton({ id: 'th-hud-btn', title: 'Trap Hub 2' }); tick();");

        Assert.Equal("th-hud-btn,phone,bag", Text("columnNames(page)"));
        Assert.Equal("Trap Hub 2", Text("find(page, 'entry-tip-title').textContent"));
    }

    [Fact]
    public void A_button_an_older_copy_of_the_mods_script_left_in_the_page_is_taken_out()
    {
        LoadApi();
        OpenHud();
        Run("var old = page.createElement('button'); old.classList.add('th-hud-btn'); columnOf(page).insertBefore(old, columnOf(page).firstChild);");

        AddTrapHub();

        Assert.Equal("th-hud-btn,phone,bag", Text("columnNames(page)"));
        Assert.False(IsTrue("old.isConnected"));
    }

    [Fact]
    public void Two_mods_buttons_each_go_in_with_their_own_order()
    {
        LoadApi();
        OpenHud();

        AddTrapHub();
        Run("ExiledToolkit.addHudButton({ id: 'fh-hud-btn', order: 30 });");

        Assert.Equal("fh-hud-btn,th-hud-btn,phone,bag", Text("columnNames(page)"));
        Assert.Equal("30", Text("find(page, 'fh-hud-btn').getAttribute('data-etk-dock')"));
    }

    [Fact]
    public void Before_the_disaster_the_button_goes_in_coreui0_in_its_wrapper_with_the_tip_beside_it()
    {
        LoadApi();
        Run("var prep = openFrame(COREUI0); var prepPage = loadPage(prep, buildColumn);");

        AddTrapHub("{ id: 'pb-hud-btn', order: 20, title: 'Package Bank', tip: 'Bank a package', badge: 2, pages: /CoreUI[01]\\.html/, onClick: function () { clicks++; } }");

        Assert.True(IsTrue("columnOf(prepPage).children[0].classList.contains('tooltip-wrapper')"));
        Assert.Equal("20", Text("columnOf(prepPage).children[0].getAttribute('data-etk-dock')"));
        Assert.Equal("Package Bank", Text("find(prepPage, 'tip-name').textContent"));
        Assert.Equal("Bank a package", Text("find(prepPage, 'tip-desc').textContent"));
        Assert.True(IsTrue("find(prepPage, 'etk-hud-badge').parentNode === find(prepPage, 'ui-btn')"));
        Assert.Equal("2", Text("find(prepPage, 'etk-hud-badge').textContent"));
        Assert.True(IsTrue("click(find(prepPage, 'ui-btn')) && clicks === 1"));
    }

    [Fact]
    public void By_default_the_button_stays_out_of_coreui0()
    {
        LoadApi();
        Run("var prep = openFrame(COREUI0); var prepPage = loadPage(prep, buildColumn);");

        AddTrapHub();

        Assert.Equal("phone,bag", Text("columnNames(prepPage)"));
    }

    [Fact]
    public void OnPlace_hears_each_time_the_button_goes_into_a_page()
    {
        LoadApi();
        OpenHud();

        Run("var placedIn = [];");

        AddTrapHub("{ id: 'th-hud-btn', onPlace: function (node, doc) { placedIn.push(doc === page && node.parentNode === columnOf(page)); } }");
        Run("tick(); rebuildColumn(page); tick(); tick();");

        Assert.Equal("true,true", Text("placedIn.join(',')"));
    }

    private static string Resource(string name)
    {
        using var stream = typeof(PageApiTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
