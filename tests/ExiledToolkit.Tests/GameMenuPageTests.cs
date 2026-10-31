using ExiledToolkit.Core;
using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// gamemenu.js whole, as the plugin embeds it, on fake pause menu and title screen pages laid out like the game's CSS:
/// where each mod's button lands, that it comes back when the game redraws the menu, what a click tells the plugin,
/// and that the menu shrinks to fit the screen however many buttons mods add.
/// </summary>
public class GameMenuPageTests
{
    private readonly Engine _engine = new();

    public GameMenuPageTests()
    {
        _engine.Execute(Resource("ExiledToolkit.Tests.GameMenuFake.js"));
        _engine.Execute(Resource("ExiledToolkit.gamemenu.js"));
    }

    private void Run(string statements) => _engine.Execute(statements);

    private string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

    private double Number(string expression) => _engine.Evaluate($"Number({expression})").AsNumber();

    private bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    private List<GameMenuCommand> Sent()
    {
        var count = (int)Number("posted.length");

        return Enumerable.Range(0, count).Select(i => GameMenuCommand.Parse(Text($"posted[{i}]"))).ToList();
    }

    private void Set(params GameMenuRow[] rows) => Run(GameMenuScript.Set(rows));

    private static GameMenuRow Row(string id, string spot, int order = 500, bool wanted = true, string label = "", string accent = "") =>
        new(id, label.Length > 0 ? label : id, spot, order, accent, wanted);

    [Fact]
    public void The_page_tells_the_plugin_it_is_ready_when_it_loads()
    {
        Assert.Contains(Sent(), command => command is GameMenuCommand.Ready);
    }

    [Fact]
    public void Top_buttons_go_right_after_Settings_in_order_and_bottom_ones_just_above_Save_and_Exit()
    {
        Run("var menu = pauseMenu(false);");
        Set(Row("mm.mods", "top", 100), Row("nlp.end", "bottom", 900), Row("sb.backup", "bottom", 200), Row("sb.now", "bottom", 300));

        Assert.Equal("back,settings,mm.mods,codex,achievement,wishlist-container,sb.backup,sb.now,nlp.end,saveExit", Text("pauseOrder(menu)"));
    }

    [Fact]
    public void Bottom_buttons_sit_above_the_games_Restart_when_it_shows_and_only_the_first_takes_the_bottom_spacing()
    {
        Run("var menu = pauseMenu(true);");
        Set(Row("sb.backup", "bottom", 200), Row("nlp.end", "bottom", 900));

        Assert.Equal("back,settings,codex,achievement,wishlist-container,sb.backup,nlp.end,restartRound,saveExit", Text("pauseOrder(menu)"));
        Assert.True(Is("menu.getElementById('slet-gm-sb.backup').classList.contains('slet-sink')"));
        Assert.False(Is("menu.getElementById('slet-gm-nlp.end').classList.contains('slet-sink')"));
    }

    [Fact]
    public void Title_buttons_go_right_before_Exit_drawn_like_the_games_items()
    {
        Run("var title = titleScreen();");
        Set(Row("mm.mods", "title", label: "MODS"));

        Assert.Equal("continue,newGame,pureEndless,loadGame,settings,mm.mods,exit", Text("titleOrder(title)"));
        Assert.Equal("MODS", Text("title.getElementById('slet-gm-mm.mods').querySelector('.menu-text').textContent"));
        Assert.True(Is("title.getElementById('slet-gm-mm.mods').querySelector('.bar')"));
    }

    [Fact]
    public void Pause_buttons_stay_off_the_title_screen_and_title_buttons_off_the_pause_menu()
    {
        Run("var menu = pauseMenu(false); var title = titleScreen();");
        Set(Row("mm.mods", "top"), Row("mm.title", "title"));

        Assert.False(Is("title.getElementById('slet-gm-mm.mods')"));
        Assert.False(Is("menu.getElementById('slet-gm-mm.title')"));
    }

    [Fact]
    public void A_button_its_mod_does_not_want_now_leaves_the_menu_and_comes_back_when_wanted()
    {
        Run("var menu = pauseMenu(false);");
        Set(Row("sb.backup", "bottom"));
        Set(Row("sb.backup", "bottom", wanted: false));

        Assert.False(Is("menu.getElementById('slet-gm-sb.backup')"));

        Set(Row("sb.backup", "bottom"));

        Assert.True(Is("menu.getElementById('slet-gm-sb.backup')"));
    }

    [Fact]
    public void A_button_the_plugin_drops_leaves_the_menu()
    {
        Run("var menu = pauseMenu(false);");
        Set(Row("sb.backup", "bottom"), Row("mm.mods", "top"));
        Set(Row("mm.mods", "top"));

        Assert.Equal("back,settings,mm.mods,codex,achievement,wishlist-container,saveExit", Text("pauseOrder(menu)"));
    }

    [Fact]
    public void A_button_the_game_drops_when_it_redraws_the_menu_is_back_before_the_next_paint()
    {
        Run("var menu = pauseMenu(false);");
        Set(Row("mm.mods", "top"));
        Run("var list = menu.querySelector('.menu-list'); list.children.filter(function (c) { return c.getAttribute('data-slet-gm'); })[0].remove(); el(list, 'div.main-btn'); flush();");

        Assert.True(Is("menu.getElementById('slet-gm-mm.mods').isConnected"));
        Assert.True(Number("observerRounds") <= 2, "the page's watcher should settle instead of waking itself");
    }

    [Fact]
    public void A_menu_page_the_game_opens_later_gets_its_buttons_from_the_poll()
    {
        Set(Row("mm.mods", "top"));
        Run("var menu = pauseMenu(false); poll();");

        Assert.True(Is("menu.getElementById('slet-gm-mm.mods')"));
    }

    [Fact]
    public void Clicking_a_pause_button_tells_the_plugin_which_one()
    {
        Run("var menu = pauseMenu(false); posted.length = 0;");
        Set(Row("sb.now", "bottom"));
        Run("click(menu.getElementById('slet-gm-sb.now'));");

        Assert.Equal(new GameMenuCommand[] { new GameMenuCommand.Run("sb.now") }, Sent());
    }

    [Fact]
    public void Clicking_a_title_button_plays_the_click_first_then_tells_the_plugin()
    {
        Run("var title = titleScreen(); posted.length = 0;");
        Set(Row("mm.mods", "title"));
        Run("click(title.getElementById('slet-gm-mm.mods'));");

        Assert.Empty(Sent());

        Run("runTimeouts();");

        Assert.Equal(new GameMenuCommand[] { new GameMenuCommand.Run("mm.mods") }, Sent());
    }

    [Fact]
    public void A_buttons_accent_colours_its_edge_and_a_changed_label_is_redrawn_in_place()
    {
        Run("var menu = pauseMenu(false);");
        Set(Row("nlp.end", "bottom", label: "End this run", accent: "#EBA308"));
        Run("var first = menu.getElementById('slet-gm-nlp.end');");
        Set(Row("nlp.end", "bottom", label: "结束本局", accent: "#EBA308"));

        Assert.True(Is("menu.getElementById('slet-gm-nlp.end') === first"));
        Assert.Equal("结束本局", Text("first.textContent"));
        Assert.Equal("#EBA308", Text("first.style.getPropertyValue('--slet-accent')"));
        Assert.True(Is("first.classList.contains('slet-accent')"));
    }

    // At 1080 the game's own pause menu has room for about four more buttons; these cases are all about the fit.

    [Fact]
    public void A_pause_menu_that_already_fits_keeps_the_games_own_size()
    {
        Run("var menu = pauseMenu(false, 1080);");
        Set(Row("mm.mods", "top"));

        Assert.Equal(1, Number("fit(menu)"));
    }

    [Fact]
    public void A_pause_menu_with_more_buttons_than_fit_shrinks_just_enough_to_show_them_all()
    {
        Run("var menu = pauseMenu(true, 1080);");
        Run(GameMenuScript.Set(Enumerable.Range(0, 8).Select(i => Row("mod.b" + i, i < 2 ? "top" : "bottom", i))));

        var size = Number("fit(menu)");

        Assert.InRange(size, 0.5, 0.99);
        Assert.False(Is("menu.querySelector('.left-panel').classList.contains('slet-scroll')"), "8 buttons fit without scrolling");
        Assert.True(Number("drawnHeight(menu.querySelector('.left-panel'))") <= 1080, "every button should be on screen");

        // Just enough: a step bigger would not fit.
        Run($"menu.documentElement.style.setProperty('--slet-fit', {size + 0.04});");
        Assert.True(Number("drawnHeight(menu.querySelector('.left-panel'))") > 1080, "the fit should not shrink more than it has to");
    }

    [Fact]
    public void More_buttons_than_fit_at_half_size_stop_at_half_size_and_the_list_scrolls()
    {
        Run("var menu = pauseMenu(false, 1080);");
        Run(GameMenuScript.Set(Enumerable.Range(0, 40).Select(i => Row("mod.b" + i, "bottom", i))));

        Assert.Equal(0.5, Number("fit(menu)"));
        Assert.True(Is("menu.querySelector('.left-panel').classList.contains('slet-scroll')"));
    }

    [Fact]
    public void Fewer_buttons_again_grow_the_menu_back_and_stop_the_scrolling()
    {
        Run("var menu = pauseMenu(false, 1080);");
        Run(GameMenuScript.Set(Enumerable.Range(0, 40).Select(i => Row("mod.b" + i, "bottom", i))));
        Set(Row("mm.mods", "top"));

        Assert.Equal(1, Number("fit(menu)"));
        Assert.False(Is("menu.querySelector('.left-panel').classList.contains('slet-scroll')"));
    }

    [Fact]
    public void The_games_own_Restart_showing_up_refits_the_menu()
    {
        Run("var menu = pauseMenu(false, 1080);");
        Run(GameMenuScript.Set(Enumerable.Range(0, 5).Select(i => Row("mod.b" + i, "bottom", i))));
        Run("var before = fit(menu); var list = menu.querySelector('.menu-list'); button(list, 'restartRound', '.btn-restart'); list.insertBefore(list.children.filter(function (c) { return c.classList.contains('btn-restart'); })[0], list.children.filter(function (c) { return c.classList.contains('btn-exit'); })[0]); flush();");

        Assert.True(Number("fit(menu)") < Number("before"), "one more button should shrink the menu");
        Assert.True(Number("drawnHeight(menu.querySelector('.left-panel'))") <= 1080);
    }

    [Fact]
    public void A_smaller_window_refits_the_menu()
    {
        Run("var menu = pauseMenu(false, 1080);");
        Set(Row("mm.mods", "top"), Row("sb.backup", "bottom"));
        Run("resize(menu, 720);");

        Assert.True(Number("fit(menu)") < 1);
        Assert.True(Number("drawnHeight(menu.querySelector('.left-panel'))") <= 720);
    }

    // The game animates its buttons' sizes, so a fit that measured them mid-animation saw the shrunk menu as full
    // size and put it back to full size, running Save & Exit off the screen.
    [Fact]
    public void A_menu_already_shrunk_still_fits_when_it_is_fitted_again()
    {
        Run("var menu = pauseMenu(false, 864);");
        Run(GameMenuScript.Set(Enumerable.Range(0, 10).Select(i => Row("mod.b" + i, i < 1 ? "top" : "bottom", i))));
        Run("frame();");
        Run(GameMenuScript.Set(Enumerable.Range(0, 9).Select(i => Row("mod.b" + i, i < 1 ? "top" : "bottom", i))));

        Assert.InRange(Number("fit(menu)"), 0.5, 0.99);
        Assert.False(Is("menu.querySelector('.left-panel').classList.contains('slet-scroll')"), "9 buttons fit without scrolling");
        Assert.True(Number("drawnHeight(menu.querySelector('.left-panel'))") <= 864, "every button should be on screen");
    }

    // A list scrolling from the last fit is only as tall as the screen, and measured that way it looked like it fit at
    // full size.
    [Fact]
    public void A_menu_left_scrolling_stays_on_screen_when_it_is_fitted_again()
    {
        Run("var menu = pauseMenu(false, 864);");
        Run(GameMenuScript.Set(Enumerable.Range(0, 14).Select(i => Row("mod.b" + i, i < 1 ? "top" : "bottom", i))));
        Run("frame();");
        Run(GameMenuScript.Set(Enumerable.Range(0, 13).Select(i => Row("mod.b" + i, i < 1 ? "top" : "bottom", i))));

        Assert.Equal(0.5, Number("fit(menu)"));
        Assert.True(Is("menu.querySelector('.left-panel').classList.contains('slet-scroll')"), "13 buttons still need the list to scroll");
        Assert.True(Number("drawnHeight(menu.querySelector('.left-panel'))") <= 864, "the menu should stay on screen");
    }

    [Fact]
    public void A_title_screen_already_shrunk_still_fits_when_it_is_fitted_again()
    {
        Run("var title = titleScreen(700);");
        Set(Row("mm.mods", "title"));
        Run("frame(); resize(title, 500);");

        var size = Number("fit(title)");

        Assert.True(Number("drawnHeight(title.querySelector('.container'))") <= 500, "every item should be on screen");

        // Just enough: a step bigger would not fit.
        Run($"title.documentElement.style.setProperty('--slet-fit', {size + 0.04});");
        Assert.True(Number("drawnHeight(title.querySelector('.container'))") > 500, "the fit should not shrink more than it has to");
    }

    [Fact]
    public void A_title_screen_with_too_little_room_shrinks_to_fit_too()
    {
        Run("var title = titleScreen(600);");
        Set(Row("mm.mods", "title"));

        Assert.InRange(Number("fit(title)"), 0.5, 0.99);
        Assert.True(Number("drawnHeight(title.querySelector('.container'))") <= 600);
    }

    [Fact]
    public void A_hidden_menu_page_is_fitted_once_it_shows()
    {
        Run("var menu = pauseMenu(false, 720); menu.defaultView.frameElement.style.display = 'none';");
        Set(Row("mm.mods", "top"), Row("sb.backup", "bottom"));

        Assert.Equal(1, Number("fit(menu)"));

        Run("menu.defaultView.frameElement.style.display = ''; poll();");

        Assert.True(Number("drawnHeight(menu.querySelector('.left-panel'))") <= 720);
    }

    [Fact]
    public void A_poll_with_nothing_changed_reads_no_layout()
    {
        Run("var menu = pauseMenu(false, 1080);");
        Set(Row("mm.mods", "top"), Row("sb.backup", "bottom"));
        Run("layoutReads = 0; poll(); poll();");

        Assert.Equal(0, Number("layoutReads"));
    }

    private static string Resource(string name)
    {
        using var stream = typeof(GameMenuPageTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
