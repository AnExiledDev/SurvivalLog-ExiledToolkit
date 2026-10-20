using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// dock.js exactly as the plugin embeds it, run in Jint on a fake HUD frame. The game's tool column holds a hidden
/// one-time buff button, the phone and the bag; mods put their buttons at the top, in whatever order they loaded.
/// </summary>
public class DockTests
{
    private readonly Engine _engine = new();

    public DockTests()
    {
        _engine.Execute(Resource("ExiledToolkit.Tests.DockFake.js"));
        _engine.Execute("var buff = button('buff'); buff.style.display = 'none'; button('phone'); button('bag');");
        _engine.Execute(Resource("ExiledToolkit.dock.js"));
    }

    private void Run(string statements) => _engine.Execute(statements);

    private string Place(string name) => _engine.Evaluate($"placeOf('{name}')").AsString();

    private bool IsDocked() => _engine.Evaluate("isDocked()").AsBoolean();

    /// <summary>Each mod puts its button on top of the column as it loads; this load order is deliberately not the dock order.</summary>
    private void AddModButtons() => Run("button('defense', 40, true); button('bank', 20, true); button('trap', 10, true); button('farm', 30, true); tick();");

    [Fact]
    public void Past_four_buttons_the_last_four_stay_in_the_first_column_and_the_rest_fill_a_column_to_the_right_top_down()
    {
        AddModButtons();

        Assert.True(IsDocked());
        Assert.Equal("2:1", Place("trap"));
        Assert.Equal("2:2", Place("bank"));
        Assert.Equal("1:1", Place("farm"));
        Assert.Equal("1:2", Place("defense"));
        Assert.Equal("1:3", Place("phone"));
        Assert.Equal("1:4", Place("bag"));
    }

    [Fact]
    public void A_hidden_button_takes_no_slot_and_one_that_shows_goes_after_the_tagged_mod_buttons()
    {
        AddModButtons();
        Run("buff.style.display = ''; tick();");

        Assert.Equal("2:1", Place("trap"));
        Assert.Equal("2:2", Place("bank"));
        Assert.Equal("2:3", Place("farm"));
        Assert.Equal("1:1", Place("defense"));
        Assert.Equal("1:2", Place("buff"));
        Assert.Equal("1:4", Place("bag"));
    }

    [Fact]
    public void A_ninth_button_starts_a_third_column()
    {
        AddModButtons();
        Run("buff.style.display = ''; button('extra1', 50, true); button('extra2', 60, true); tick();");

        Assert.Equal("2:1", Place("trap"));
        Assert.Equal("2:4", Place("defense"));
        Assert.Equal("3:1", Place("extra1"));
        Assert.Equal("1:1", Place("extra2"));
        Assert.Equal("1:4", Place("bag"));
    }

    [Fact]
    public void Four_buttons_or_fewer_are_left_to_the_game_and_a_docked_column_lets_go_when_buttons_leave()
    {
        Run("button('trap', 10, true); tick();");

        Assert.False(IsDocked());
        Assert.Equal("-", Place("trap"));

        Run("button('bank', 20, true); button('farm', 30, true); tick();");

        Assert.True(IsDocked());

        Run("column.removeChild(find('farm')); tick();");

        Assert.False(IsDocked());
        Assert.Equal("-", Place("trap"));
        Assert.Equal("-", Place("bag"));
    }

    [Fact]
    public void Loading_the_script_twice_keeps_one_dock()
    {
        _engine.Execute(Resource("ExiledToolkit.dock.js"));

        Assert.Equal(1, (int)_engine.Evaluate("intervals.length").AsNumber());
    }

    private static string Resource(string name)
    {
        using var stream = typeof(DockTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
