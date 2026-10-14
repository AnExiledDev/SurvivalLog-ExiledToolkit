using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>The C# side of the page: messages it understands, script it builds, and where a click counts as on a toast.</summary>
public class PageTests
{
    private const char LineSeparator = (char)0x2028;
    private const char Backslash = (char)92;

    [Fact]
    public void Page_messages_parse_to_their_commands()
    {
        Assert.IsType<PageCommand.Ready>(PageCommand.Parse("slmod|toolkit|ready"));
        Assert.IsType<PageCommand.NoScript>(PageCommand.Parse("slmod|toolkit|noscript"));
        Assert.Equal(new ToastId(12), Assert.IsType<PageCommand.Click>(PageCommand.Parse("slmod|toolkit|click|12")).Id);
        Assert.Equal(new PageCommand.Gone(new ToastId(7), 3, false), PageCommand.Parse("slmod|toolkit|gone|7|3|timeout"));
        Assert.Equal(new PageCommand.Gone(new ToastId(7), 3, true), PageCommand.Parse("slmod|toolkit|gone|7|3|closed"));
    }

    [Theory]
    [InlineData("slmod|toolkit|click|0")]
    [InlineData("slmod|toolkit|click|-3")]
    [InlineData("slmod|toolkit|click|abc")]
    [InlineData("slmod|toolkit|click")]
    [InlineData("slmod|toolkit|gone|1")]
    [InlineData("slmod|toolkit|gone|1|2")]
    [InlineData("slmod|toolkit|gone|1|0|timeout")]
    [InlineData("slmod|toolkit|gone|1|two|timeout")]
    [InlineData("slmod|toolkit|gone|1|2|Closed")]
    [InlineData("slmod|toolkit|gone|1|2|bored")]
    [InlineData("slmod|toolkit|rects|0.1,0.2,0.3")]
    [InlineData("slmod|toolkit|rects|0.1,0.2,-0.3,0.4")]
    [InlineData("slmod|toolkit|rects|0.1,0.2,NaN,0.4")]
    [InlineData("slmod|toolkit|shout")]
    public void A_malformed_message_of_ours_is_unknown_rather_than_acted_on(string message)
    {
        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse(message));
    }

    [Fact]
    public void Messages_of_other_mods_and_of_the_game_are_left_alone()
    {
        Assert.False(PageCommand.IsOurs("slmod|storagehub|ready"));
        Assert.False(PageCommand.IsOurs("9|CoreUI1||{}"));
        Assert.False(PageCommand.IsOurs(null));
    }

    [Fact]
    public void Box_positions_parse_as_page_fractions_and_an_empty_list_clears_them()
    {
        var rects = Assert.IsType<PageCommand.Rects>(PageCommand.Parse("slmod|toolkit|rects|0.75,0.02,0.2,0.1;0.75,0.13,0.2,0.08"));
        var cleared = Assert.IsType<PageCommand.Rects>(PageCommand.Parse("slmod|toolkit|rects|"));

        Assert.Equal(new[] { new NormalRect(0.75, 0.02, 0.2, 0.1), new NormalRect(0.75, 0.13, 0.2, 0.08) }, rects.Boxes);
        Assert.Empty(cleared.Boxes);
    }

    [Fact]
    public void A_box_list_longer_than_any_stack_is_not_from_our_page()
    {
        var boxes = string.Join(";", Enumerable.Repeat("0.1,0.1,0.1,0.1", PageCommand.MaxRects + 1));

        Assert.IsType<PageCommand.Unknown>(PageCommand.Parse("slmod|toolkit|rects|" + boxes));
    }

    [Fact]
    public void Text_becomes_a_string_literal_that_cannot_break_out_of_the_script()
    {
        var text = "say \"hi\" " + Backslash + " </script>" + LineSeparator + "\n" + (char)1;

        var literal = ToastScript.Str(text);

        var u = Backslash + "u";
        Assert.Equal("\"say " + Backslash + "\"hi" + Backslash + "\" " + Backslash + Backslash + " " + u + "003c/script>" + u + "2028" + u + "000a" + u + "0001\"", literal);
    }

    [Fact]
    public void A_missing_text_becomes_an_empty_string()
    {
        Assert.Equal("\"\"", ToastScript.Str(null));
    }

    [Fact]
    public void The_frame_script_applies_the_ops_or_reports_the_script_missing()
    {
        var toast = new ToastView(new ToastId(4), new ToastContent("Mod", "A <b>", null, ToastKind.Error, null), 2, 7_000, true);

        var script = ToastScript.Apply(new PageOp[] { new PageOp.Place(ToastPosition.BottomLeft), new PageOp.Show(toast), new PageOp.Merge(toast with { Count = 3, HasClick = false }), new PageOp.Remove(new ToastId(4)) });

        Assert.StartsWith("window.__exiledtoolkit?window.__exiledtoolkit.apply([", script);
        Assert.EndsWith("]):window.vuplex&&window.vuplex.postMessage('slmod|toolkit|noscript')", script);
        Assert.Contains("{\"op\":\"place\",\"pos\":\"bottom-left\"}", script);
        Assert.Contains("{\"op\":\"show\",\"id\":4,\"source\":\"Mod\",\"title\":\"A " + Backslash + "u003cb>\",\"body\":\"\",\"kind\":\"error\",\"icon\":\"\",\"count\":2,\"ms\":7000,\"click\":true}", script);
        Assert.Contains("{\"op\":\"merge\",\"id\":4,\"source\":\"Mod\",\"title\":\"A " + Backslash + "u003cb>\",\"body\":\"\",\"kind\":\"error\",\"icon\":\"\",\"count\":3,\"ms\":7000,\"click\":false}", script);
        Assert.Contains("{\"op\":\"remove\",\"id\":4}", script);
    }

    [Fact]
    public void Only_the_last_merge_of_each_box_in_a_frame_reaches_the_page_and_everything_else_keeps_its_order()
    {
        var a = new ToastId(1);
        var b = new ToastId(2);
        var shown = new PageOp.Show(new ToastView(b, new ToastContent("Mod", "B", null, ToastKind.Info, null), 1, 7_000, false));
        var ops = new PageOp[] { Merge(a, 2, false), shown, Merge(a, 3, true), Merge(b, 2, false), new PageOp.Remove(a) };

        var coalesced = PageOp.Coalesce(ops);

        Assert.Equal(new PageOp[] { shown, Merge(a, 3, true), Merge(b, 2, false), new PageOp.Remove(a) }, coalesced);
    }

    [Fact]
    public void Every_kind_and_corner_has_a_page_name()
    {
        Assert.All(Enum.GetValues<ToastKind>(), kind => Assert.NotEmpty(ToastScript.KindName(kind)));
        Assert.All(Enum.GetValues<ToastPosition>(), position => Assert.NotEmpty(ToastScript.PositionName(position)));
    }

    /// <summary>A kind from a newer mod must not cost the rest of the frame's toasts.</summary>
    [Fact]
    public void A_toast_of_a_kind_this_version_does_not_know_is_drawn_as_info_and_the_frame_goes_on()
    {
        var odd = new ToastView(new ToastId(1), new ToastContent("Newer mod", "Odd", null, (ToastKind)99, null), 1, 7_000, false);
        var normal = new ToastView(new ToastId(2), new ToastContent("Mod", "Normal", null, ToastKind.Success, null), 1, 7_000, false);

        var script = ToastScript.Apply(new PageOp[] { new PageOp.Show(odd), new PageOp.Show(normal) });

        Assert.Contains("{\"op\":\"show\",\"id\":1,\"source\":\"Newer mod\",\"title\":\"Odd\",\"body\":\"\",\"kind\":\"info\"", script);
        Assert.Contains("{\"op\":\"show\",\"id\":2,\"source\":\"Mod\",\"title\":\"Normal\",\"body\":\"\",\"kind\":\"success\"", script);
    }

    [Fact]
    public void A_corner_this_version_does_not_know_places_the_stack_top_right()
    {
        var script = ToastScript.Apply(new PageOp[] { new PageOp.Place((ToastPosition)7) });

        Assert.Contains("{\"op\":\"place\",\"pos\":\"top-right\"}", script);
    }

    [Fact]
    public void Reported_boxes_take_clicks_only_while_the_page_keeps_reporting_them()
    {
        var area = new ClickArea();
        var boxes = new HitArea(new[] { new NormalRect(0, 0, 0.5, 0.5) });

        area.Heard(boxes, 10);

        Assert.Same(boxes, area.At(10 + ClickArea.StaleSeconds));
        Assert.True(area.At(10 + ClickArea.StaleSeconds + 0.1).IsEmpty);
    }

    [Fact]
    public void Reported_boxes_are_forgotten_when_the_page_reloads_or_the_web_view_goes()
    {
        var area = new ClickArea();

        area.Heard(new HitArea(new[] { new NormalRect(0, 0, 0.5, 0.5) }), 10);
        area.Forget();

        Assert.True(area.At(10).IsEmpty);
    }

    [Theory]
    [InlineData(-1, 1, true)]
    [InlineData(0, 0, false)]
    [InlineData(-950, 530, true)]
    [InlineData(-960, 540, true)]
    [InlineData(-960, -540, false)]
    [InlineData(959, 0, false)]
    public void A_point_maps_to_page_fractions_the_way_the_game_maps_its_own_pages(double localX, double localY, bool expected)
    {
        // A 1920x1080 web view with a centred pivot: local (0,0) is the page's middle, and Unity's y grows upward.
        var area = new HitArea(new[] { new NormalRect(0, 0, 0.5, 0.5) });

        Assert.Equal(expected, area.ContainsLocalPoint(localX, localY, 1920, 1080, 0.5, 0.5));
    }

    [Fact]
    public void A_boxs_right_and_bottom_edges_are_outside_it_as_in_the_game()
    {
        var box = new NormalRect(0.25, 0.25, 0.5, 0.5);

        Assert.True(box.Contains(0.25, 0.25));
        Assert.False(box.Contains(0.75, 0.5));
        Assert.False(box.Contains(0.5, 0.75));
    }

    [Fact]
    public void No_boxes_or_a_collapsed_view_never_takes_a_click()
    {
        Assert.False(HitArea.Empty.ContainsLocalPoint(0, 0, 1920, 1080, 0.5, 0.5));
        Assert.False(new HitArea(new[] { new NormalRect(0, 0, 1, 1) }).ContainsLocalPoint(0, 0, 0, 1080, 0.5, 0.5));
    }

    private static PageOp.Merge Merge(ToastId id, int count, bool hasClick) =>
        new(new ToastView(id, new ToastContent("Mod", "Same", null, ToastKind.Info, null), count, 7_000, hasClick));
}
