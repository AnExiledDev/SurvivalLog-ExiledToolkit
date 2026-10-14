using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// The rule: toasts sent right after another merge into one with a count, only when the contents are
/// exactly the same, like the "not enough stamina" toast.
/// </summary>
public class MergeTests
{
    [Fact]
    public void An_identical_toast_while_the_first_is_on_screen_merges_into_it_and_counts()
    {
        var harness = new Harness();

        var first = harness.Show(Harness.Stamina());
        var second = harness.Show(Harness.Stamina());
        var third = harness.Show(Harness.Stamina());
        var ops = harness.Pump();

        Assert.Equal(first, second);
        Assert.Equal(first, third);
        Assert.Single(ops.OfType<PageOp.Show>());
        Assert.Equal(3, Assert.Single(ops.OfType<PageOp.Merge>()).Count);
        Assert.Equal(new[] { 1, 2, 3 }, harness.Heard.Select(record => record.Count));
        Assert.Equal(new[] { false, true, true }, harness.Heard.Select(record => record.IsMerge));
        Assert.All(harness.Heard, record => Assert.Equal(first, record.Id));
    }

    [Fact]
    public void An_identical_toast_while_the_first_waits_for_room_merges_and_shows_the_count_when_it_appears()
    {
        var harness = new Harness(maxVisible: 1);
        var blocker = harness.Show(Harness.Titled("On screen"));
        var waiting = harness.Show(Harness.Stamina());

        harness.Pump();
        var merged = harness.Show(Harness.Stamina());
        var whileWaiting = harness.Pump();
        harness.Center.Gone(blocker);
        var shown = Assert.Single(harness.Pump().OfType<PageOp.Show>());

        Assert.Equal(waiting, merged);
        Assert.Empty(whileWaiting);
        Assert.Equal(waiting, shown.Toast.Id);
        Assert.Equal(2, shown.Toast.Count);
    }

    [Theory]
    [InlineData("source")]
    [InlineData("title")]
    [InlineData("title case")]
    [InlineData("body")]
    [InlineData("no body")]
    [InlineData("kind")]
    [InlineData("icon")]
    public void A_toast_that_differs_in_any_shown_part_does_not_merge(string part)
    {
        var harness = new Harness();
        var different = OneCharacterOff(part);

        var first = harness.Show(Harness.Stamina());
        var second = harness.Show(different);
        var ops = harness.Pump();

        Assert.NotEqual(first, second);
        Assert.Equal(2, ops.OfType<PageOp.Show>().Count());
        Assert.Empty(ops.OfType<PageOp.Merge>());
        Assert.All(harness.Heard, record => Assert.Equal(1, record.Count));
    }

    [Fact]
    public void A_different_click_action_or_time_still_merges_because_neither_is_shown()
    {
        var harness = new Harness();

        var first = harness.Show(Harness.Stamina());
        var second = harness.Show(Harness.Stamina() with { OnClick = () => { }, Seconds = 20 });

        Assert.Equal(first, second);
    }

    [Fact]
    public void An_identical_toast_after_the_first_has_gone_starts_fresh_at_one()
    {
        var harness = new Harness();
        var first = harness.Show(Harness.Stamina());

        harness.Show(Harness.Stamina());
        harness.Pump();
        harness.Center.Gone(first);
        var again = harness.Show(Harness.Stamina());
        var ops = harness.Pump();

        Assert.NotEqual(first, again);
        Assert.Equal(1, Assert.Single(ops.OfType<PageOp.Show>()).Toast.Count);
        Assert.Equal(1, harness.Heard[^1].Count);
        Assert.False(harness.Heard[^1].IsMerge);
    }

    [Fact]
    public void A_merge_restarts_the_toasts_time()
    {
        var harness = new Harness(seconds: 7);
        var first = harness.Show(Harness.Stamina());

        harness.Pump();
        harness.Now = 7_000 + ToastBoard.LateMilliseconds - 1;
        harness.Show(Harness.Stamina());
        var merge = Assert.Single(harness.Pump().OfType<PageOp.Merge>());
        harness.Now = 7_000 + ToastBoard.LateMilliseconds + 1_000;
        var later = harness.Pump();

        Assert.Equal(first, merge.Id);
        Assert.Equal(7_000, merge.Milliseconds);
        Assert.Empty(later.OfType<PageOp.Remove>());
    }

    [Fact]
    public void A_merge_keeps_the_toasts_id_and_its_place_in_the_stack()
    {
        var harness = new Harness();
        var older = harness.Show(Harness.Stamina());
        var newer = harness.Show(Harness.Titled("Put away 12 items"));

        harness.Pump();
        var merged = harness.Show(Harness.Stamina());
        var ops = harness.Pump();

        Assert.Equal(older, merged);
        Assert.Empty(ops.OfType<PageOp.Show>());
        Assert.Empty(ops.OfType<PageOp.Replace>());
        Assert.Equal(older, Assert.Single(ops.OfType<PageOp.Merge>()).Id);
        Assert.NotEqual(older, newer);
    }

    [Fact]
    public void The_latest_click_action_wins_when_toasts_merge()
    {
        var harness = new Harness();
        var ran = new List<string>();
        var id = harness.Show(Harness.Stamina() with { OnClick = () => ran.Add("first") });

        harness.Show(Harness.Stamina() with { OnClick = () => ran.Add("second") });
        harness.Center.Activate(id);

        Assert.Equal(new[] { "second" }, ran);
    }

    [Fact]
    public void A_merge_without_a_click_action_leaves_the_toast_with_none()
    {
        var harness = new Harness();
        var id = harness.Show(Harness.Stamina() with { OnClick = () => { } });

        harness.Show(Harness.Stamina());
        var merge = Assert.Single(harness.Pump().OfType<PageOp.Merge>());

        Assert.False(merge.HasClick);
        Assert.False(harness.Center.Activate(id));
        Assert.False(harness.Heard[^1].HasClickAction);
    }

    [Fact]
    public void A_burst_of_one_toast_in_a_frame_costs_the_page_one_box_and_one_update()
    {
        var harness = new Harness();

        for (var i = 0; i < 1_000; i++) harness.Show(Harness.Stamina());

        var ops = harness.Pump();

        Assert.Equal(2, ops.Count);
        Assert.Equal(1_000, Assert.IsType<PageOp.Merge>(ops[1]).Count);
        Assert.Equal(1_000, harness.Heard.Count);
    }

    private static ToastRequest OneCharacterOff(string part) => part switch
    {
        "source" => Harness.Stamina() with { Source = "Survival Lo" },
        "title" => Harness.Stamina() with { Title = "Not enough stamina." },
        "title case" => Harness.Stamina() with { Title = "not enough stamina" },
        "body" => Harness.Stamina() with { Body = "Rest first!" },
        "no body" => Harness.Stamina() with { Body = null },
        "kind" => Harness.Stamina() with { Kind = ToastKind.Error },
        "icon" => Harness.Stamina() with { Icon = "icons/stamina.jpg" },
        _ => throw new ArgumentOutOfRangeException(nameof(part), part, null),
    };
}
