using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// menu.js whole, as the plugin embeds it, on the fake game pages: the list the plugin sends, and the poll that keeps
/// the Mods button on inventory pages the game opens later. Every call into the shared menu is counted, as each one is
/// work the game's web view does several times a second.
/// </summary>
public class MenuPollTests
{
    private readonly Engine _engine = new();

    public MenuPollTests()
    {
        _engine.Execute(Resource("ExiledToolkit.Tests.MenuFake.js"));
        _engine.Execute(Resource("ExiledToolkit.menu.js"));
        _engine.Execute(@"
            var menuCalls = 0;
            (function () {
              var hub = window.__slmodmenu;
              ['add', 'update', 'remove', 'setWanted', 'place', 'placeAll'].forEach(function (name) {
                var call = hub[name];
                hub[name] = function () { menuCalls++; return call.apply(hub, arguments); };
              });
            })();

            function poll() {
              intervals.forEach(function (callback) { if (callback) callback(); });
            }");
    }

    private void Run(string statements) => _engine.Execute(statements);

    private bool Is(string expression) => _engine.Evaluate(expression).AsBoolean();

    private int Number(string expression) => (int)_engine.Evaluate(expression).AsNumber();

    private const string TwoWanted = "[{ id: 'loot.run', label: 'Loot all', order: 300, wanted: true }, { id: 'unpack.run', label: 'Unpack all', order: 200, wanted: true }, { id: 'later.run', label: 'Later', order: 400, wanted: false }]";

    [Fact]
    public void The_list_shows_the_button_at_once_with_only_the_wanted_entries()
    {
        Run($"var inv = inventory(0); __exiledtoolkitmenu.set({TwoWanted}); click(inv.querySelector('.slmm-btn'));");

        Assert.Equal("Unpack all,Loot all", _engine.Evaluate("rows(inv)").AsString());
    }

    [Fact]
    public void An_inventory_page_opened_later_gets_the_button_from_the_poll()
    {
        Run($"__exiledtoolkitmenu.set({TwoWanted}); var inv = inventory(0); poll();");

        Assert.True(Is("!!inv.querySelector('.slmm-wrap')"));
    }

    [Fact]
    public void Each_poll_asks_the_shared_menu_for_one_look_at_the_pages_however_many_entries_there_are()
    {
        Run($"var inv = inventory(0); __exiledtoolkitmenu.set({TwoWanted}); menuCalls = 0; poll();");

        Assert.Equal(1, Number("menuCalls"));
    }

    [Fact]
    public void With_none_of_its_entries_wanted_the_poll_leaves_the_shared_menu_alone_and_the_button_goes()
    {
        Run($"var inv = inventory(0); __exiledtoolkitmenu.set({TwoWanted});");
        Run("__exiledtoolkitmenu.set([{ id: 'loot.run', label: 'Loot all', order: 300, wanted: false }]); menuCalls = 0; poll(); poll();");

        Assert.Equal(0, Number("menuCalls"));
        Assert.False(Is("!!inv.querySelector('.slmm-wrap')"));
    }

    private static string Resource(string name)
    {
        using var stream = typeof(MenuPollTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
