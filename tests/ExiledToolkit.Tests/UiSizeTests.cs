using System.Globalization;
using ExiledToolkit.Core;
using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// ui.js, which draws mod windows and panels at Interface &gt; UiScale, run in Jint on fake game pages (UiFake.js). Panels
/// are handed over with the same shim the mods carry, and the size goes in as the plugin sends it.
/// </summary>
public class UiSizeTests
{
    // ===== The size =====

    [Fact]
    public void A_panel_is_drawn_bigger_at_once_and_exactly_as_the_mod_drew_it_back_at_100()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 300, addPage()); uiSize(p)");

        ui.Set(125);
        var big = ui.Text("drawn(p)");
        ui.Set(100);

        Assert.Equal("500x375", big);
        Assert.Equal("", ui.Text("zoomOf(p)"));
    }

    [Fact]
    public void A_size_below_100_draws_the_panel_smaller()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 300, addPage()); uiSize(p)");

        ui.Set(75);

        Assert.Equal("300x225", ui.Text("drawn(p)"));
    }

    [Theory]
    [InlineData(500, "800x600")]
    [InlineData(10, "300x225")]
    public void A_size_outside_75_to_200_is_drawn_at_the_nearest_end(int percent, string drawn)
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 300, addPage()); uiSize(p)");

        ui.Set(percent);

        Assert.Equal(drawn, ui.Text("drawn(p)"));
    }

    /// <summary>A PC set to a language that writes 1,25 must still send the page a number it can read.</summary>
    [Fact]
    public void The_size_reaches_the_page_whatever_language_the_pc_uses()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("de-DE");

        try
        {
            var ui = new UiScript();
            ui.Run("var p = panel(400, 300, addPage()); uiSize(p)");

            ui.Set(125);

            Assert.Equal("500x375", ui.Text("drawn(p)"));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    // ===== Fitting the screen =====

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_panel_too_tall_for_the_screen_at_that_size_is_drawn_as_big_as_still_fits(bool observerSeesZoom)
    {
        var ui = new UiScript(observerSeesZoom: observerSeesZoom);
        ui.Run("var p = panel(400, 900, addPage()); uiSize(p)");

        ui.Set(150);

        // 1350 px at 150% would run off a 1080 px screen.
        Assert.InRange(ui.Number("p.getBoundingClientRect().height"), 1000, 1080);
    }

    [Fact]
    public void A_panel_already_too_big_at_its_own_size_is_never_drawn_smaller_than_the_mod_drew_it()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 1200, addPage()); uiSize(p)");

        ui.Set(150);

        Assert.Equal("400x1200", ui.Text("drawn(p)"));
    }

    [Fact]
    public void A_panel_handed_over_as_never_shrunk_takes_the_full_size_even_past_the_screen()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 900, addPage()); uiSize(p, { fit: false })");

        ui.Set(150);

        Assert.Equal("600x1350", ui.Text("drawn(p)"));
    }

    /// <summary>A window centred on a full-screen backdrop: the backdrop is zoomed, the window inside it must fit.</summary>
    [Fact]
    public void A_backdrop_is_zoomed_so_the_window_inside_it_fits_the_screen()
    {
        var ui = new UiScript();
        ui.Run("var page = addPage(); var back = panel(0, 0, page); back.fills = true; var win = panel(400, 900, page, back); uiSize(back, { fit: win })");

        ui.Set(150);

        Assert.Equal("1920x1080", ui.Text("drawn(back)"));
        Assert.InRange(ui.Number("win.getBoundingClientRect().height"), 1000, 1080);
    }

    /// <summary>
    /// The workbench zooms its own body down when its panels are too wide for the window, and a column a mod adds is one
    /// of those panels. Fitted at the page's own size, the column grows only as far as the page still shows at full
    /// size; 600 + 1000 x 1.24 is 96% of 1920.
    /// </summary>
    [Fact]
    public void A_column_on_a_page_that_fits_itself_grows_only_as_far_as_the_page_keeps_its_own_size()
    {
        var ui = new UiScript();
        ui.Run("var page = selfFittingPage(600, 1000, 700); uiSize(page.column, { fit: page.box, pageFits: true, onZoom: page.fit })");

        ui.Set(150);

        Assert.InRange(double.Parse(ui.Text("zoomOf(page.column)"), CultureInfo.InvariantCulture), 1.2, 1.25);
        Assert.Equal("", ui.Text("page.document.body.style.zoom"));
    }

    /// <summary>
    /// After the window gets smaller the page has already zoomed itself down by the time the Toolkit looks, so the box
    /// fits on screen. Measured on screen the column would keep its size and the game's panels would stay small; at the
    /// page's own size the column shrinks until the page needs no zoom of its own.
    /// </summary>
    [Fact]
    public void A_column_on_a_page_that_fits_itself_gives_way_when_the_window_gets_smaller_so_the_game_keeps_its_size()
    {
        var ui = new UiScript();
        ui.Run("resizeWindow(2560, 1440); var page = selfFittingPage(600, 1000, 700); uiSize(page.column, { fit: page.box, pageFits: true, onZoom: page.fit })");
        ui.Set(200);
        var wide = double.Parse(ui.Text("zoomOf(page.column)"), CultureInfo.InvariantCulture);

        ui.Run("resizeWindow(1700, 1080); page.fit()");
        ui.Settle();

        Assert.InRange(wide, 1.8, 1.86);
        Assert.InRange(double.Parse(ui.Text("zoomOf(page.column)"), CultureInfo.InvariantCulture), 1.0, 1.05);
        Assert.Equal("", ui.Text("page.document.body.style.zoom"));
    }

    [Fact]
    public void A_panel_hidden_when_the_size_changes_takes_it_and_is_fitted_once_it_shows()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 900, addPage()); p.hidden = true; uiSize(p)");

        ui.Set(150);
        var whileHidden = ui.Text("zoomOf(p)");
        ui.Run("p.hidden = false");
        ui.Settle();

        Assert.Equal("1.5", whileHidden);
        Assert.InRange(ui.Number("p.getBoundingClientRect().height"), 1000, 1080);
    }

    /// <summary>A panel the player opens and closes all session must fit every time and keep following its own size.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_panel_opened_and_closed_many_times_fits_every_time_and_still_grows_back_when_it_gets_shorter(bool observerSeesZoom)
    {
        var ui = new UiScript(observerSeesZoom: observerSeesZoom);
        ui.Run("var p = panel(400, 900, addPage()); uiSize(p)");
        ui.Set(150);
        var heights = new List<int>();

        for (var i = 0; i < 12; i++)
        {
            ui.Run("p.hidden = true");
            ui.Settle();
            ui.Run("p.hidden = false");
            ui.Settle();
            heights.Add(ui.Number("p.getBoundingClientRect().height"));
        }

        ui.Run("p.height = 700");
        ui.Settle();

        Assert.All(heights, height => Assert.InRange(height, 1000, 1080));
        Assert.Equal("600x1050", ui.Text("drawn(p)"));
    }

    /// <summary>A big window the player closes and opens again must show at the size that fits from its first frame,
    /// not at the full size until the next frame fits it again.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_panel_shrunk_to_fit_and_closed_opens_again_at_the_size_that_fits_not_bigger(bool observerSeesZoom)
    {
        var ui = new UiScript(observerSeesZoom: observerSeesZoom);
        ui.Run("var p = panel(400, 900, addPage()); uiSize(p)");
        ui.Set(150);
        var fittedZoom = ui.Text("zoomOf(p)");

        ui.Run("p.hidden = true");
        ui.Settle();
        ui.Run("p.hidden = false");

        Assert.Equal("1.152", fittedZoom);
        Assert.Equal(fittedZoom, ui.Text("zoomOf(p)"));
        Assert.InRange(ui.Number("p.getBoundingClientRect().height"), 1000, 1080);
    }

    /// <summary>A window opened for the first time at a big size must fit on the frame it first shows in.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_panel_opened_for_the_first_time_fits_on_its_first_frame(bool observerSeesZoom)
    {
        var ui = new UiScript(observerSeesZoom: observerSeesZoom);
        ui.Run("var p = panel(400, 900, addPage()); p.hidden = true; uiSize(p)");
        ui.Set(150);

        ui.Run("p.hidden = false; nextFrame()");

        Assert.InRange(ui.Number("p.getBoundingClientRect().height"), 1000, 1080);
    }

    /// <summary>
    /// A mod that draws its window again puts a new window in the backdrop it handed over and drops the old one. The new
    /// one must still fit, not show at the full size because the old one measures nothing.
    /// </summary>
    [Fact]
    public void A_window_the_mod_draws_again_inside_its_backdrop_still_fits_the_screen()
    {
        var ui = new UiScript();
        ui.Run("var page = addPage(); var back = panel(0, 0, page); back.fills = true; var win = panel(400, 900, page, back); win.className = 'win';" +
            "uiSize(back, { fit: win })");
        ui.Set(150);

        ui.Run("win.parentNode = null; var again = panel(400, 1000, page, back); again.className = 'win'");
        ui.Settle();
        ui.Set(160);

        Assert.InRange(ui.Number("again.getBoundingClientRect().height"), 1000, 1080);
    }

    [Fact]
    public void A_backdrop_whose_window_is_gone_keeps_its_size_rather_than_taking_a_bigger_one_unfitted()
    {
        var ui = new UiScript();
        ui.Run("var page = addPage(); var back = panel(0, 0, page); back.fills = true; var win = panel(400, 600, page, back); win.className = 'win';" +
            "uiSize(back, { fit: win })");
        ui.Set(150);

        ui.Run("win.parentNode = null");
        ui.Set(200);

        Assert.Equal("1.5", ui.Text("zoomOf(back)"));
    }

    /// <summary>A mod that hands over something that is not an element must not stop every other panel's size.</summary>
    [Fact]
    public void A_fit_that_is_not_an_element_fits_the_panel_itself_and_other_panels_still_take_the_size()
    {
        var ui = new UiScript();
        ui.Run("var page = addPage(); var odd = panel(400, 900, page); uiSize(odd, { fit: 'card' }); var other = panel(400, 300, page); uiSize(other)");

        ui.Set(150);

        Assert.InRange(ui.Number("odd.getBoundingClientRect().height"), 1000, 1080);
        Assert.Equal("600x450", ui.Text("drawn(other)"));
    }

    [Fact]
    public void A_closed_panel_that_was_shrunk_takes_a_new_size_only_as_far_as_it_still_fits()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 900, addPage()); uiSize(p)");
        ui.Set(150);
        ui.Run("p.hidden = true");
        ui.Settle();

        ui.Set(200);
        var atBigger = ui.Text("zoomOf(p)");
        ui.Set(110);
        var atSmaller = ui.Text("zoomOf(p)");

        Assert.Equal("1.152", atBigger);
        Assert.Equal("1.1", atSmaller);
    }

    [Fact]
    public void A_panel_shrunk_to_fit_grows_back_to_the_full_size_when_the_game_window_gets_bigger()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 900, addPage()); uiSize(p)");
        ui.Set(150);

        ui.Run("resizeWindow(2560, 1440)");
        ui.Settle();

        Assert.Equal("600x1350", ui.Text("drawn(p)"));
    }

    /// <summary>A panel whose size jumps with its own zoom must not have the page laid out again every frame.</summary>
    [Fact]
    public void A_panel_that_never_settles_is_left_alone_after_a_few_tries()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 600, addPage()); p.heightAt = function (z) { return z > 1.2 ? 1000 * z : 600 * z; }; uiSize(p)");

        ui.Run(UiSize.SetScript(150));

        Assert.InRange(ui.Number("settle()"), 1, 15);
    }

    [Fact]
    public void A_panel_that_fits_settles_in_a_couple_of_frames_and_the_same_size_again_changes_nothing()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 900, addPage()); uiSize(p)");

        ui.Run(UiSize.SetScript(150));
        var frames = ui.Number("settle()");
        var writes = ui.Number("p.style.zoomWrites");
        ui.Set(150);

        Assert.InRange(frames, 1, 3);
        Assert.Equal(writes, ui.Number("p.style.zoomWrites"));
    }

    // ===== Handing panels over =====

    [Fact]
    public void Panels_handed_over_before_the_toolkit_script_loads_get_the_size_once_it_does_and_one_whose_page_closed_is_skipped()
    {
        var ui = new UiScript(isLoaded: false);
        ui.Run("var p = panel(400, 300, addPage()); uiSize(p); var page = addPage(); var gone = panel(400, 300, page); uiSize(gone); closePage(page)");

        ui.Load();
        ui.Set(125);

        Assert.Equal("500x375", ui.Text("drawn(p)"));
        Assert.Equal("", ui.Text("zoomOf(gone)"));
    }

    [Theory]
    [InlineData("reloadPage(page)")]
    [InlineData("closePage(page)")]
    [InlineData("p.parentNode = null")]
    public void A_panel_its_page_left_behind_is_let_go(string leave)
    {
        var ui = new UiScript();
        ui.Run("var page = addPage(); var p = panel(400, 300, page); uiSize(p)");
        ui.Set(125);

        ui.Run(leave);
        ui.Set(150);

        Assert.Equal("1.25", ui.Text("zoomOf(p)"));
    }

    [Fact]
    public void A_mod_hears_each_new_zoom_of_its_panel()
    {
        var ui = new UiScript();
        ui.Run("var seen = []; var p = panel(400, 300, addPage()); uiSize(p, { onZoom: function (z) { seen.push(z); } })");

        ui.Set(125);
        ui.Set(125);
        ui.Set(100);

        Assert.Equal("1.25,1", ui.Text("seen.join(',')"));
    }

    [Fact]
    public void A_mod_whose_follow_up_fails_does_not_stop_other_panels_taking_the_size()
    {
        var ui = new UiScript();
        ui.Run("var page = addPage(); var other = panel(400, 300, page); uiSize(other); var p = panel(400, 300, page); uiSize(p, { onZoom: function () { throw new Error('broken'); } })");

        ui.Set(125);

        Assert.Equal("500x375", ui.Text("drawn(other)"));
        Assert.Equal("500x375", ui.Text("drawn(p)"));
    }

    [Fact]
    public void A_panel_handed_over_again_keeps_one_place_and_only_its_newest_follow_up_runs()
    {
        var ui = new UiScript();
        ui.Run("var first = 0; var second = 0; var p = panel(400, 300, addPage()); uiSize(p, { onZoom: function () { first++; } }); uiSize(p, { onZoom: function () { second++; } })");

        ui.Set(125);

        Assert.Equal((0, 1, 1), (ui.Number("first"), ui.Number("second"), ui.Number("p.style.zoomWrites")));
    }

    [Fact]
    public void The_script_sent_twice_keeps_the_panels_the_first_copy_holds()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 300, addPage()); uiSize(p)");

        ui.Load();
        ui.Set(125);

        Assert.Equal("500x375", ui.Text("drawn(p)"));
    }

    // ===== A tooltip matching its panel =====

    /// <summary>
    /// A tooltip drawn on the page's body for a row inside a panel takes zoomAt, so it must carry the panel's zoom and
    /// not the page's own: the game zooms some pages itself, and a tooltip on that page's body already has that zoom.
    /// </summary>
    [Fact]
    public void ZoomAt_gives_a_row_its_panels_zoom_without_the_pages_own()
    {
        var ui = new UiScript();
        ui.Run("var page = addPage(); page.document.body.style.zoom = '0.8';" +
            "var p = panel(400, 300, page); var row = panel(100, 20, page, p); var loose = panel(100, 20, page);" +
            "uiSize(p, { fit: false })");

        ui.Set(150);

        Assert.Equal(("1.5", "1"), (ui.Text("ExiledUi.zoomAt(row)"), ui.Text("ExiledUi.zoomAt(loose)")));
    }

    [Fact]
    public void ZoomAt_multiplies_a_panel_inside_another_and_follows_a_new_size()
    {
        var ui = new UiScript();
        ui.Run("var outer = panel(800, 600); var inner = panel(200, 100, null, outer); var row = panel(100, 20, null, inner);" +
            "uiSize(outer, { fit: false }); uiSize(inner, { fit: false })");

        ui.Set(150);
        var first = ui.Text("ExiledUi.zoomAt(row)");
        ui.Set(80);

        Assert.Equal(("2.25", "0.64"), (first, ui.Text("ExiledUi.zoomAt(row)")));
    }

    [Fact]
    public void ZoomAt_is_1_before_any_panel_is_handed_over()
    {
        var ui = new UiScript();
        ui.Run("var row = panel(100, 20)");

        ui.Set(150);

        Assert.Equal("1", ui.Text("ExiledUi.zoomAt(row)"));
    }

    // ===== A mod's own size =====

    [Fact]
    public void A_mod_with_its_own_size_draws_its_panels_at_that_size_and_every_other_panel_keeps_the_players()
    {
        var ui = new UiScript();
        ui.Run("var page = addPage(); var farm = panel(400, 300, page); var trap = panel(400, 300, page); var plain = panel(400, 300, page);" +
            "uiSize(farm, { mod: 'FarmHub' }); uiSize(trap, { mod: 'TrapHub' }); uiSize(plain)");

        ui.Set(125);
        ui.SetMod("FarmHub", 150);

        Assert.Equal(("600x450", "500x375", "500x375"), (ui.Text("drawn(farm)"), ui.Text("drawn(trap)"), ui.Text("drawn(plain)")));
    }

    [Fact]
    public void A_mod_size_of_0_has_its_panels_follow_the_players_size_again()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 300, addPage()); uiSize(p, { mod: 'FarmHub' })");
        ui.Set(125);
        ui.SetMod("FarmHub", 75);
        var own = ui.Text("drawn(p)");

        ui.SetMod("FarmHub", 0);

        Assert.Equal(("300x225", "500x375"), (own, ui.Text("drawn(p)")));
    }

    [Fact]
    public void A_new_player_size_leaves_a_mod_with_its_own_size_where_it_is()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 300, addPage()); uiSize(p, { mod: 'FarmHub' })");
        ui.SetMod("FarmHub", 125);

        ui.Set(150);

        Assert.Equal("500x375", ui.Text("drawn(p)"));
    }

    [Fact]
    public void A_mod_size_sent_before_the_panel_is_handed_over_applies_to_it()
    {
        var ui = new UiScript();
        ui.SetMod("FarmHub", 150);

        ui.Run("var p = panel(400, 300, addPage()); uiSize(p, { mod: 'FarmHub' })");
        ui.Settle();

        Assert.Equal("600x450", ui.Text("drawn(p)"));
    }

    [Fact]
    public void A_panel_a_mod_queued_before_ui_js_loaded_keeps_its_mods_size()
    {
        var ui = new UiScript(isLoaded: false);
        ui.Run("var p = panel(400, 300, addPage()); uiSize(p, { mod: 'FarmHub' })");

        ui.Load();
        ui.SetMod("FarmHub", 150);

        Assert.Equal("600x450", ui.Text("drawn(p)"));
    }

    [Fact]
    public void A_panel_too_tall_for_the_screen_at_its_mods_size_is_drawn_as_big_as_still_fits()
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 900, addPage()); uiSize(p, { mod: 'FarmHub' })");

        ui.SetMod("FarmHub", 150);

        Assert.InRange(ui.Number("p.getBoundingClientRect().height"), 1000, 1080);
    }

    [Theory]
    [InlineData(500, "800x600")]
    [InlineData(10, "300x225")]
    public void A_mod_size_outside_75_to_200_is_drawn_at_the_nearest_end(int percent, string drawn)
    {
        var ui = new UiScript();
        ui.Run("var p = panel(400, 300, addPage()); uiSize(p, { mod: 'FarmHub' })");

        ui.SetMod("FarmHub", percent);

        Assert.Equal(drawn, ui.Text("drawn(p)"));
    }

    /// <summary>The name goes into the page's script as a string; one that could end the string is refused.</summary>
    [Theory]
    [InlineData("Farm\"Hub")]
    [InlineData("Farm Hub")]
    [InlineData("")]
    public void A_mod_name_that_is_not_letters_digits_dots_dashes_or_underscores_is_refused(string mod)
    {
        Assert.Throws<ArgumentException>(() => UiSize.ModScript(mod, 120));
    }

    /// <summary>ui.js on UiFake.js. <see cref="Set"/> sends the size as the plugin does, then lets the page settle.</summary>
    private sealed class UiScript
    {
        private readonly Engine _engine = new();

        public UiScript(bool isLoaded = true, bool observerSeesZoom = true)
        {
            _engine.Execute(Resource("ExiledToolkit.Tests.UiFake.js"));
            _engine.Execute("observerSeesZoom = " + (observerSeesZoom ? "true" : "false"));

            if (isLoaded) Load();
        }

        public void Load() => _engine.Execute(Resource("ExiledToolkit.ui.js"));

        public void Set(int percent)
        {
            _engine.Execute(UiSize.SetScript(percent));
            Settle();
        }

        public void SetMod(string mod, int percent)
        {
            _engine.Execute(UiSize.ModScript(mod, percent));
            Settle();
        }

        public void Settle() => _engine.Execute("settle()");

        public void Run(string statements) => _engine.Execute(statements);

        public string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

        public int Number(string expression) => (int)Math.Round(_engine.Evaluate($"Number({expression})").AsNumber());

        private static string Resource(string name)
        {
            using var stream = typeof(UiSizeTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
            using var reader = new StreamReader(stream);

            return reader.ReadToEnd();
        }
    }
}
