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

    private void Load() => _engine.Execute(Resource("ExiledToolkit.editor.js"));

    private bool Bool(string expression) => _engine.Evaluate(expression).AsBoolean();

    private static string Resource(string name)
    {
        using var stream = typeof(EditorTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the tests");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
