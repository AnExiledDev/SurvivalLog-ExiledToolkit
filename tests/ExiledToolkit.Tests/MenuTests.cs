using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>What a mod adding to the Mods menu can count on: its text in the game's language, its show rule followed, and the page sent only what changed.</summary>
public class MenuTests
{
    private static readonly MenuItem Loot = new("loot.run", "Loot all", "全部搜刮", "Loot this store", "搜刮这家店", "M0 0h24v24H0z", 300);
    private static readonly MenuItem Unpack = new("unpack.run", "Unpack all", "", "Unpack packages", "", "", 200);

    private readonly MenuBoard _board = new();

    [Fact]
    public void Rows_come_in_order_then_id()
    {
        _board.Put(Loot);
        _board.Put(Unpack);
        _board.Put(Unpack with { Id = "aaa.first", Order = 300 });

        var ids = _board.Rows(false, _ => true).Select(row => row.Id);

        Assert.Equal(new[] { "unpack.run", "aaa.first", "loot.run" }, ids);
    }

    [Fact]
    public void Chinese_game_shows_the_Chinese_text_and_falls_back_to_English_when_there_is_none()
    {
        _board.Put(Loot);
        _board.Put(Unpack);

        var rows = _board.Rows(true, _ => true).ToDictionary(row => row.Id);

        Assert.Equal("全部搜刮", rows["loot.run"].Label);
        Assert.Equal("搜刮这家店", rows["loot.run"].Title);
        Assert.Equal("Unpack all", rows["unpack.run"].Label);
        Assert.Equal("Unpack packages", rows["unpack.run"].Title);
    }

    [Fact]
    public void An_entry_is_wanted_only_while_its_mod_says_so()
    {
        _board.Put(Loot);
        _board.Put(Unpack);

        var rows = _board.Rows(false, id => id == "unpack.run").ToDictionary(row => row.Id);

        Assert.False(rows["loot.run"].IsWanted);
        Assert.True(rows["unpack.run"].IsWanted);
    }

    [Fact]
    public void The_page_is_sent_the_list_once_until_something_changes()
    {
        _board.Put(Loot);
        var first = _board.Pending(false, _ => true);
        _board.Shown(first!);

        var again = _board.Pending(false, _ => true);
        var afterHide = _board.Pending(false, _ => false);

        Assert.NotNull(first);
        Assert.Null(again);
        Assert.NotNull(afterHide);
        Assert.Contains("\"wanted\":false", afterHide);
    }

    [Fact]
    public void A_language_change_resends_the_list()
    {
        _board.Put(Loot);
        _board.Shown(_board.Pending(false, _ => true)!);

        var inChinese = _board.Pending(true, _ => true);

        Assert.NotNull(inChinese);
        Assert.Contains("全部搜刮", inChinese);
    }

    [Fact]
    public void A_reloaded_page_gets_the_whole_list_again()
    {
        _board.Put(Loot);
        _board.Shown(_board.Pending(false, _ => true)!);

        _board.PageReset();

        Assert.NotNull(_board.Pending(false, _ => true));
    }

    [Fact]
    public void A_removed_entry_leaves_the_list_sent_to_the_page()
    {
        _board.Put(Loot);
        _board.Put(Unpack);
        _board.Shown(_board.Pending(false, _ => true)!);

        _board.Remove("loot.run");
        var script = _board.Pending(false, _ => true);

        Assert.NotNull(script);
        Assert.DoesNotContain("loot.run", script);
        Assert.Contains("unpack.run", script);
    }

    [Fact]
    public void Adding_an_entry_with_the_same_id_replaces_it()
    {
        _board.Put(Loot);
        _board.Put(Loot with { Label = "Loot store" });

        var rows = _board.Rows(false, _ => true);

        Assert.Single(rows);
        Assert.Equal("Loot store", rows[0].Label);
    }

    [Theory]
    [InlineData("")]
    [InlineData("has|bar")]
    [InlineData("has space")]
    public void An_id_that_cannot_travel_in_a_page_message_is_refused(string id)
    {
        Assert.Throws<ArgumentException>(() => _board.Put(Loot with { Id = id }));
    }

    [Fact]
    public void Entry_text_cannot_break_out_of_the_script()
    {
        _board.Put(Loot with { Label = "a\");alert(1);//</script>" });

        var script = _board.Pending(false, _ => true)!;

        Assert.Contains("\"a\\\");alert(1);//\\u003c/script>\"", script);
        Assert.DoesNotContain("</script>", script);
    }

    [Fact]
    public void Page_messages_parse_to_what_the_plugin_acts_on()
    {
        Assert.IsType<MenuCommand.Ready>(MenuCommand.Parse("slmod|toolkitmenu|ready"));
        Assert.IsType<MenuCommand.NoScript>(MenuCommand.Parse("slmod|toolkitmenu|noscript"));
        Assert.Equal(new MenuCommand.Run("loot.run"), MenuCommand.Parse("slmod|toolkitmenu|run|loot.run"));
        Assert.Equal(new MenuCommand.Open(true), MenuCommand.Parse("slmod|toolkitmenu|open|1"));
        Assert.Equal(new MenuCommand.Open(false), MenuCommand.Parse("slmod|toolkitmenu|open|0"));
        Assert.Equal(new MenuCommand.Diagnostic("cannot read | the tab"), MenuCommand.Parse("slmod|toolkitmenu|diag|cannot read | the tab"));
    }

    [Theory]
    [InlineData("slmod|toolkit|ready")]
    [InlineData("slmod|toolkitmenu|run")]
    [InlineData("slmod|toolkitmenu|run|bad id")]
    [InlineData("slmod|toolkitmenu|open|yes")]
    [InlineData("slmod|toolkitmenu|explode")]
    public void Anything_else_is_unknown(string message)
    {
        Assert.IsType<MenuCommand.Unknown>(MenuCommand.Parse(message));
    }
}
