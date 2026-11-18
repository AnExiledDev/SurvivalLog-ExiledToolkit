using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// editor.js as the Toolkit embeds it, run in Jint: the reward editor two tabs share. Each tab that makes one gets its
/// own effect list, its own class name in the styles with its own lines where they belong, and an older copy of the
/// script never replaces a newer one a mod already sent.
/// </summary>
public class EditorTests
{
    private readonly Engine _engine = new();

    public EditorTests()
    {
        _engine.Execute("var window = this;");
    }

    [Fact]
    public void Each_tab_gets_its_own_effect_list_with_the_games_effects_in_it()
    {
        Load();

        _engine.Execute("var a = window.ExiledEditor.make({}), b = window.ExiledEditor.make({});");
        _engine.Execute("a.fx({ id: 'mr:only-here', src: 'mr', cat: 'home', scripts: [], params: [] });");

        Assert.True(Bool("a.FX.some(f => f.id === 'game:solar') && b.FX.some(f => f.id === 'game:solar')"));
        Assert.True(Bool("a.FX.some(f => f.id === 'mr:only-here')"));
        Assert.False(Bool("b.FX.some(f => f.id === 'mr:only-here')"));
    }

    [Fact]
    public void The_styles_carry_the_tabs_class_and_its_own_lines_at_their_place()
    {
        Load();

        _engine.Execute("var css = window.ExiledEditor.make({}).editorCss('mr', ['N .mine{color:red}']);");

        Assert.True(Bool("css.indexOf('N .mine{color:red}') > 0 && css.indexOf('N .mine{color:red}') < css.length - 1"));
        Assert.True(Bool("css.some(l => l.indexOf('.mr-top') >= 0) && css.some(l => l.indexOf('.tg.src-mr{') >= 0)"));
        Assert.False(Bool("css.some(l => l.indexOf('{c}') >= 0 || l === '@extra')"));
    }

    [Fact]
    public void An_older_copy_never_replaces_a_newer_one()
    {
        _engine.Execute("window.ExiledEditor = { version: 2, make: function () { return 'newer'; } };");

        Load();

        Assert.Equal("newer", _engine.Evaluate("window.ExiledEditor.make()").AsString());
    }

    [Fact]
    public void The_search_finds_a_reward_by_its_number_or_any_levels_text_in_either_language()
    {
        Load();
        MakeTabWithTwoRewards();

        Assert.Equal("102", Found("loot"));
        Assert.Equal("101", Found("太阳能"));
        Assert.Equal("102", Found("102"));
        Assert.Equal("101", Found("SOLAR"));
    }

    [Fact]
    public void The_search_needs_every_word_and_an_empty_search_shows_every_reward()
    {
        Load();
        MakeTabWithTwoRewards();

        Assert.Equal("", Found("solar loot"));
        Assert.Equal("101,102", Found("  "));
    }

    [Fact]
    public void Effect_text_says_one_hour_or_one_day_without_a_plural()
    {
        Load();

        _engine.Execute("var t = window.ExiledEditor.make({}), byId = id => t.FX.find(f => f.id === id), countdown = byId('game:countdown'), shelf = byId('game:short-life-discount');");

        Assert.Contains("Gives you 1 hour of", _engine.Evaluate("countdown.desc({ IntValue0: 1 }, false)").AsString());
        Assert.Contains("Gives you 3 hours of", _engine.Evaluate("countdown.desc({ IntValue0: 3 }, false)").AsString());
        Assert.Contains("less than 1 day.", _engine.Evaluate("shelf.desc({ FloatValue0: 1, FloatValue1: 10 }, false)").AsString());
        Assert.Contains("less than 5 days.", _engine.Evaluate("shelf.desc({ FloatValue0: 5, FloatValue1: 10 }, false)").AsString());
    }

    // Two of the game's rewards: 101 talks about solar power, and only 102's second level mentions loot.
    private void MakeTabWithTwoRewards() => _engine.Execute("""
        var ghosts = {
          101: { id: 101, name: { en: 'Solar Boost', zh: '太阳能增益' }, levels: [{ gameDesc: { en: 'More solar power.', zh: '太阳能板产电效率提升' } }], gives: [] },
          102: { id: 102, name: { en: 'Lucky Find', zh: '幸运发现' }, levels: [{ gameDesc: { en: 'Find more food.' } }, { gameDesc: { en: 'Find more loot.' } }], gives: [] }
        };
        var T = { st: { q: '', view: 'all', entries: [] }, allIds: function () { return [102, 101]; }, ghostOf: function (id) { return ghosts[id] || null; } };
        var tab = window.ExiledEditor.make(T);
        """);

    private string Found(string search)
    {
        _engine.Execute("T.st.q = " + System.Text.Json.JsonSerializer.Serialize(search) + ";");

        return _engine.Evaluate("tab.listIds().join(',')").AsString();
    }

    private void Load() => _engine.Execute(Resource("ExiledToolkit.editor.js"));

    private bool Bool(string expression) => _engine.Evaluate(expression).AsBoolean();

    private static string Resource(string name)
    {
        using var stream = typeof(EditorTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the tests");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
