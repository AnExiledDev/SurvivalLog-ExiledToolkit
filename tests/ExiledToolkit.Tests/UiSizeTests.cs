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
