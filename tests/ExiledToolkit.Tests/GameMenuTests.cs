using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>What a mod adding a button to the game's menus can count on: its text in the game's language, its show rule followed, a bad button refused at once, and the page sent the list only when it changed.</summary>
public class GameMenuTests
{
    private static readonly GameMenuItem Mods = new("mm.mods", "MODS", "模组", GameMenuSpot.PauseTop, 100, "");
    private static readonly GameMenuItem Backup = new("sb.now", "Back up now", "", GameMenuSpot.PauseBottom, 300, "#8fc7bf");

    private readonly GameMenuBoard _board = new();

    [Fact]
    public void Rows_come_in_order_then_id()
    {
        _board.Put(Backup);
        _board.Put(Mods);
        _board.Put(Mods with { Id = "aaa.first", Order = 300 });

        Assert.Equal(new[] { "mm.mods", "aaa.first", "sb.now" }, _board.Rows(false, _ => true).Select(row => row.Id));
    }

    [Fact]
    public void Chinese_game_shows_the_Chinese_label_and_falls_back_to_English_when_there_is_none()
    {
        _board.Put(Mods);
        _board.Put(Backup);

        var rows = _board.Rows(true, _ => true).ToDictionary(row => row.Id);

        Assert.Equal("模组", rows["mm.mods"].Label);
        Assert.Equal("Back up now", rows["sb.now"].Label);
    }

    [Fact]
    public void A_button_its_mod_does_not_want_now_is_sent_as_not_wanted()
    {
        _board.Put(Mods);
        _board.Put(Backup);

        var rows = _board.Rows(false, id => id == "mm.mods").ToDictionary(row => row.Id);

        Assert.True(rows["mm.mods"].IsWanted);
        Assert.False(rows["sb.now"].IsWanted);
    }

    [Fact]
    public void The_page_is_sent_the_list_once_and_again_only_after_a_change_or_a_reload()
    {
        _board.Put(Mods);

        var first = _board.Pending(false, _ => true);

        Assert.NotNull(first);
        _board.Shown(first!);
        Assert.Null(_board.Pending(false, _ => true));
        Assert.NotNull(_board.Pending(true, _ => true));
        Assert.NotNull(_board.Pending(false, _ => false));

        _board.PageReset();

        Assert.Equal(first, _board.Pending(false, _ => true));
    }

    [Theory]
    [InlineData("has space", GameMenuSpot.PauseTop, "")]
    [InlineData("", GameMenuSpot.PauseTop, "")]
    [InlineData("ok.id", "middle", "")]
    [InlineData("ok.id", GameMenuSpot.Title, "red")]
    [InlineData("ok.id", GameMenuSpot.Title, "#12345")]
    [InlineData("ok.id", GameMenuSpot.Title, "#fff;color:red")]
    public void A_bad_id_place_or_colour_is_refused_when_the_button_is_added(string id, string spot, string accent)
    {
        Assert.Throws<ArgumentException>(() => _board.Put(new GameMenuItem(id, "x", "", spot, 0, accent)));
    }

    [Fact]
    public void A_removed_button_is_no_longer_sent()
    {
        _board.Put(Mods);
        _board.Put(Backup);

        Assert.True(_board.Remove("sb.now"));
        Assert.Equal(new[] { "mm.mods" }, _board.Rows(false, _ => true).Select(row => row.Id));
    }

    [Fact]
    public void Labels_with_quotes_reach_the_page_as_text_not_script()
    {
        _board.Put(Mods with { Label = "it's \"new\"</script>" });

        var script = _board.Pending(false, _ => true)!;

        Assert.Contains("\"label\":\"it's \\\"new\\\"", script);
        Assert.DoesNotContain("</script>", script);
    }

    [Theory]
    [InlineData("slmod|toolkitgamemenu|ready", typeof(GameMenuCommand.Ready))]
    [InlineData("slmod|toolkitgamemenu|noscript", typeof(GameMenuCommand.NoScript))]
    [InlineData("slmod|toolkitgamemenu|run|sb.now", typeof(GameMenuCommand.Run))]
    [InlineData("slmod|toolkitgamemenu|diag|no Settings|button", typeof(GameMenuCommand.Diagnostic))]
    [InlineData("slmod|toolkitgamemenu|run|bad id", typeof(GameMenuCommand.Unknown))]
    [InlineData("slmod|toolkitmenu|run|sb.now", typeof(GameMenuCommand.Unknown))]
    public void Page_messages_parse_to_what_the_plugin_acts_on(string message, Type expected)
    {
        Assert.IsType(expected, GameMenuCommand.Parse(message));
    }

    [Fact]
    public void A_diagnostic_keeps_its_whole_text()
    {
        Assert.Equal(new GameMenuCommand.Diagnostic("no Settings|button"), GameMenuCommand.Parse("slmod|toolkitgamemenu|diag|no Settings|button"));
    }
}
