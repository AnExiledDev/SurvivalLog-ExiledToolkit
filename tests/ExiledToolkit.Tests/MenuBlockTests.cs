using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// The shared slmod-menu block exactly as menu.js carries it, run in Jint on fake game pages. The Mods button is meant
/// to be in the bag panel of every station screen but the shredder and the drone trade, and on the inventory's item
/// tab, with the station screens listing only the entries made for them.
/// </summary>
public class MenuBlockTests
{
    private readonly Engine _engine = new();

    public MenuBlockTests()
    {
        _engine.Execute(Resource("ExiledToolkit.Tests.MenuFake.js"));
    }

    private void Run(string statements) => _engine.Execute(statements);

    private string Text(string expression) => _engine.Evaluate(expression).ToString();

    private bool Is(string expression) => _engine.Evaluate(expression).AsBoolean();

    private void LoadV2() => Run(Block(Resource("ExiledToolkit.menu.js"), "v2"));

    /// <summary>Put away works anywhere; the home list only on the inventory.</summary>
    private void AddEntries() => Run(
        "slmodMenu.add({ id: 'hub.putaway', label: 'Put away', order: 100, stations: true, wanted: true, run: function () { window.ran = 'putaway'; } });" +
        "slmodMenu.add({ id: 'hub.home', label: 'Home', order: 90, wanted: true, run: function () {} });");

    /// <summary>The workbench's bag toolbar: a filter, then the game's sort button.</summary>
    private void Workbench(string name = "ToolTable") => Run(
        $"var station = page('{name}'); var bar = el(el(station, 'div.bag-panel'), 'div#bagToolbar'); el(bar, 'input.filter'); el(bar, 'button#btn-sort-bag.action-btn.unity-interactive');");

    [Fact]
    public void A_station_gets_the_button_in_front_of_its_sort_button_listing_only_the_station_entries()
    {
        LoadV2();
        AddEntries();
        Workbench();

        Run("slmodMenu.placeAll();");

        Assert.Equal("btn-sort-bag", Text("station.querySelector('.slmm-wrap').nextElementSibling.id"));
        Assert.True(Is("station.querySelector('.slmm-btn').classList.contains('action-btn')"));

        Run("click(station.querySelector('.slmm-btn'));");

        Assert.Equal("Put away", Text("rows(station)"));

        Run("click(station.querySelector('.slmm-row'));");

        Assert.Equal("putaway", Text("window.ran"));
        Assert.False(Is("!!station.querySelector('.slmm-menu')"));
    }

    [Fact]
    public void The_inventory_lists_every_wanted_entry_on_the_item_tab_and_drops_the_button_on_other_tabs()
    {
        LoadV2();
        AddEntries();
        Run("var inv = inventory(0); slmodMenu.placeAll(); click(inv.querySelector('.slmm-btn'));");

        Assert.Equal("Home,Put away", Text("rows(inv)"));
        Assert.Equal("sort", Text("inv.querySelector('.slmm-wrap').nextElementSibling.id"));

        Run("inv.getElementById('app')._vnode.component.setupState.curATab = 1; slmodMenu.placeAll();");

        Assert.False(Is("!!inv.querySelector('.slmm-wrap')"));
    }

    [Theory]
    [InlineData("Shredder")]
    [InlineData("TradeUI")]
    public void The_shredder_and_the_drone_trade_get_no_button(string name)
    {
        LoadV2();
        AddEntries();
        Workbench(name);

        Run("slmodMenu.placeAll();");

        Assert.False(Is("!!station.querySelector('.slmm-wrap')"));
    }

    [Fact]
    public void A_station_with_no_entry_made_for_it_gets_no_button()
    {
        LoadV2();
        Run("slmodMenu.add({ id: 'hub.home', label: 'Home', wanted: true, run: function () {} });");
        Workbench();

        Run("slmodMenu.placeAll();");

        Assert.False(Is("!!station.querySelector('.slmm-wrap')"));
    }

    [Fact]
    public void A_title_row_station_gets_its_own_button_at_the_end_opening_downwards()
    {
        LoadV2();
        AddEntries();
        Run("var cage = page('RatCage'); var title = el(el(el(cage, 'div.generator-system'), 'div.panel'), 'div.panel-title'); el(title, 'span');");

        Run("slmodMenu.placeAll(); click(cage.querySelector('.slmm-btn'));");

        Assert.True(Is("title.children[title.children.length - 1].classList.contains('slmm-wrap')"));
        Assert.True(Is("cage.querySelector('.slmm-btn').classList.contains('slmm-own')"));
        Assert.True(Is("cage.querySelector('.slmm-menu').classList.contains('slmm-down')"));
    }

    [Fact]
    public void A_poll_keeps_an_open_menu_open()
    {
        LoadV2();
        AddEntries();
        Workbench();
        Run("slmodMenu.placeAll(); var wrap = station.querySelector('.slmm-wrap'); click(station.querySelector('.slmm-btn')); slmodMenu.placeAll();");

        Assert.True(Is("station.querySelector('.slmm-wrap') === wrap"));
        Assert.True(Is("!!station.querySelector('.slmm-menu')"));
    }

    [Fact]
    public void An_unreadable_inventory_tab_shows_the_button_and_is_reported_once()
    {
        LoadV2();
        AddEntries();
        Run("var problems = []; slmodMenu.watch('test', function () {}, function (text) { problems.push(text); });");
        Run("var inv = inventory(0); inv.getElementById('app')._vnode = null; slmodMenu.placeAll(); slmodMenu.placeAll();");

        Assert.True(Is("!!inv.querySelector('.slmm-wrap')"));
        Assert.Equal("1", Text("problems.length"));
    }

    [Fact]
    public void Version_2_takes_over_from_version_1_keeping_its_entries_and_watchers()
    {
        Run(Block(Resource("ExiledToolkit.Tests.MenuV1.js"), "v1"));
        Run("var opened = []; slmodMenu.add({ id: 'old.run', label: 'Old', wanted: true, run: function () {} }); slmodMenu.watch('old', function (isOpen) { opened.push(isOpen); });");

        LoadV2();
        Run("var inv = inventory(0); slmodMenu.placeAll(); click(inv.querySelector('.slmm-btn'));");

        Assert.Equal("2", Text("window.__slmodmenu.version"));
        Assert.Equal("Old", Text("rows(inv)"));
        Assert.Equal("true", Text("opened.join()"));
    }

    /// <summary>The block between its BEGIN and END marker lines, so the test runs exactly what the mods carry.</summary>
    private static string Block(string source, string version)
    {
        var begin = source.IndexOf("// ===== BEGIN slmod-menu " + version, StringComparison.Ordinal);
        var endMarker = "// ===== END slmod-menu " + version + " =====";
        var end = source.IndexOf(endMarker, StringComparison.Ordinal);

        if (begin < 0 || end < 0) throw new InvalidOperationException("no slmod-menu " + version + " block");

        return source.Substring(begin, end + endMarker.Length - begin);
    }

    private static string Resource(string name)
    {
        using var stream = typeof(MenuBlockTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
