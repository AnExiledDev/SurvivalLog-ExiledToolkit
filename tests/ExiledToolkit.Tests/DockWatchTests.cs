using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// dock.js's watcher, run in Jint on the fake root page with observers and animation frames. The HUD's tool column is
/// watched from the first poll; after that a button that comes, goes, shows or hides is laid out in the next frame,
/// and nothing else the HUD redraws makes the dock read the page. None of these tests run the poll unless they say so.
/// </summary>
public class DockWatchTests
{
    private readonly Engine _engine = new();

    public DockWatchTests()
    {
        _engine.Execute(Resource("ExiledToolkit.Tests.PhoneFake.js"));
        _engine.Execute(@"
            var hud = openFrame('file:///WebUI/UI/CoreUI1/CoreUI1.html');
            var page = makeDocument(hud.src);
            hud.contentDocument = page;
            var column = page.body.appendChild(page.createElement('div'));
            column.classList.add('vertical-tools');

            function hudButton(name, order) {
              var el = page.createElement('div');
              el.name = name;
              if (order) el.setAttribute('data-etk-dock', order);
              return column.appendChild(el);
            }

            function find(name) {
              return column.children.filter(function (el) { return el.name === name; })[0];
            }

            function placeOf(name) {
              var el = find(name);
              return !el.style.gridColumn ? '-' : el.style.gridColumn + ':' + el.style.gridRow;
            }

            hudButton('phone');
            hudButton('bag');");
        _engine.Execute(Resource("ExiledToolkit.dock.js"));
    }

    private void Run(string statements) => _engine.Execute(statements);

    private bool IsDocked() => _engine.Evaluate("column.classList.contains('etk-dock')").AsBoolean();

    private string Place(string name) => _engine.Evaluate($"placeOf('{name}')").AsString();

    private int Number(string expression) => (int)_engine.Evaluate(expression).AsNumber();

    /// <summary>Three mods add their buttons, each in its own change, all before the frame is drawn.</summary>
    private void AddModButtons() => Run("hudButton('farm', 30); flush(); hudButton('bank', 20); flush(); hudButton('trap', 10); flush();");

    [Fact]
    public void Buttons_mods_add_are_in_the_dock_by_the_next_frame_without_a_poll()
    {
        AddModButtons();
        Run("nextFrame();");

        Assert.True(IsDocked());
        Assert.Equal("2:1", Place("trap"));
        Assert.Equal("1:1", Place("bank"));
        Assert.Equal("1:4", Place("bag"));
    }

    [Fact]
    public void Many_buttons_added_in_one_frame_are_laid_out_once_and_that_layout_does_not_wake_itself()
    {
        Run("var reads = page.styleReads;");
        AddModButtons();
        Run("nextFrame(); nextFrame();");

        // One layout reads each of the five buttons once.
        Assert.Equal(5, Number("page.styleReads - reads"));
    }

    [Fact]
    public void A_button_the_game_hides_takes_its_slot_back_in_the_next_frame()
    {
        AddModButtons();
        Run("nextFrame(); find('trap').style.display = 'none'; find('trap').changed('style'); flush(); nextFrame();");

        Assert.False(IsDocked());
        Assert.Equal("-", Place("bank"));
    }

    [Fact]
    public void What_a_button_redraws_inside_itself_does_not_make_the_dock_read_the_page()
    {
        AddModButtons();
        Run("nextFrame(); var reads = page.styleReads;");
        Run(@"var tip = find('bag').appendChild(page.createElement('span'));
              tip.setAttribute('class', 'cooldown');
              tip.style.display = 'none'; tip.changed('style');
              find('bag').changed('style');
              flush(); nextFrame();");

        Assert.Equal(0, Number("page.styleReads - reads"));
    }

    [Fact]
    public void The_poll_leaves_a_watched_column_to_its_watcher()
    {
        AddModButtons();
        Run("nextFrame(); var reads = page.styleReads; var scans = page.scans; tick(); tick(); flush(); nextFrame();");

        Assert.Equal(0, Number("page.styleReads - reads"));
        Assert.Equal(0, Number("page.scans - scans"));
    }

    [Fact]
    public void A_HUD_page_loaded_again_is_found_and_laid_out_by_the_poll()
    {
        AddModButtons();
        Run(@"nextFrame();
              page = makeDocument(hud.src);
              hud.contentDocument = page;
              column = page.body.appendChild(page.createElement('div'));
              column.classList.add('vertical-tools');
              hudButton('phone'); hudButton('bag'); hudButton('farm', 30); hudButton('bank', 20); hudButton('trap', 10);
              tick();");

        Assert.True(IsDocked());
        Assert.Equal("2:1", Place("trap"));
    }

    private static string Resource(string name)
    {
        using var stream = typeof(DockWatchTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
