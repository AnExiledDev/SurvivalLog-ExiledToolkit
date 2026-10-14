using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>What is on screen: at most MaxVisible boxes, the rest waiting their turn, and keyed toasts replacing in place.</summary>
public class StackTests
{
    [Fact]
    public void Toasts_beyond_the_most_on_screen_wait_and_appear_as_earlier_ones_close()
    {
        var harness = new Harness(maxVisible: 2);
        var first = harness.Show(Harness.Titled("one"));

        harness.Show(Harness.Titled("two"));
        var third = harness.Show(Harness.Titled("three"));
        var shownAtOnce = harness.Pump().OfType<PageOp.Show>().Count();
        harness.Center.Gone(first);
        var next = Assert.Single(harness.Pump().OfType<PageOp.Show>());

        Assert.Equal(2, shownAtOnce);
        Assert.Equal(third, next.Toast.Id);
    }

    [Fact]
    public void A_waiting_toast_gets_its_full_time_when_it_appears()
    {
        var harness = new Harness(maxVisible: 1, seconds: 7);
        var first = harness.Show(Harness.Titled("one"));

        harness.Show(Harness.Titled("two"));
        harness.Pump();
        harness.Now = 6_000;
        harness.Center.Gone(first);
        var next = Assert.Single(harness.Pump().OfType<PageOp.Show>());

        Assert.Equal(7_000, next.Toast.Milliseconds);
    }

    [Fact]
    public void A_flood_of_different_toasts_keeps_only_the_newest_waiting_ones()
    {
        var harness = new Harness(maxVisible: 1);
        var onScreen = harness.Show(Harness.Titled("on screen"));
        var waiting = Enumerable.Range(0, ToastBoard.MaxWaiting + 5).Select(i => harness.Show(Harness.Titled("waiting " + i))).ToList();

        harness.Pump();
        harness.Center.Gone(onScreen);
        var next = Assert.Single(harness.Pump().OfType<PageOp.Show>());

        Assert.Equal(waiting[5], next.Toast.Id);
        Assert.Equal(ToastBoard.MaxWaiting + 6, harness.Heard.Count);
    }

    [Fact]
    public void A_toast_with_the_same_key_replaces_the_one_on_screen_in_place_under_a_new_id()
    {
        var harness = new Harness();
        var progress = harness.Show(Harness.Titled("Organizing 1/3") with { Key = "organize" });

        harness.Show(Harness.Titled("Unrelated"));
        harness.Pump();
        var later = harness.Show(Harness.Titled("Organizing 2/3") with { Key = "organize" });
        var ops = harness.Pump();
        var replace = Assert.Single(ops.OfType<PageOp.Replace>());

        Assert.NotEqual(progress, later);
        Assert.Equal(progress, replace.OldId);
        Assert.Equal(later, replace.Toast.Id);
        Assert.Equal("Organizing 2/3", replace.Toast.Content.Title);
        Assert.Empty(ops.OfType<PageOp.Show>());
        Assert.Equal(1, harness.Heard[^1].Count);
    }

    [Fact]
    public void A_toast_with_the_same_key_replaces_one_that_is_waiting_without_taking_another_place()
    {
        var harness = new Harness(maxVisible: 1);
        var onScreen = harness.Show(Harness.Titled("on screen"));

        harness.Show(Harness.Titled("Organizing 1/3") with { Key = "organize" });
        var later = harness.Show(Harness.Titled("Organizing 2/3") with { Key = "organize" });
        harness.Pump();
        harness.Center.Gone(onScreen);
        var shown = harness.Pump().OfType<PageOp.Show>().ToList();
        harness.Center.Gone(later);

        Assert.Equal(later, Assert.Single(shown).Toast.Id);
        Assert.Empty(harness.Pump());
    }

    [Fact]
    public void Toasts_without_a_key_stack_even_when_another_has_one()
    {
        var harness = new Harness();

        harness.Show(Harness.Titled("one") with { Key = "organize" });
        harness.Show(Harness.Titled("two"));

        Assert.Equal(2, harness.Pump().OfType<PageOp.Show>().Count());
    }

    [Fact]
    public void A_box_the_page_never_reports_gone_is_taken_down_long_after_its_time_and_frees_its_place()
    {
        var harness = new Harness(maxVisible: 1, seconds: 7);
        var silent = harness.Show(Harness.Titled("one"));
        var waiting = harness.Show(Harness.Titled("two"));

        harness.Pump();
        harness.Now = 7_000 + ToastBoard.LateMilliseconds - 1;
        var early = harness.Pump();
        harness.Now = 7_000 + ToastBoard.LateMilliseconds;
        var due = harness.Pump();

        Assert.Empty(early);
        Assert.Equal(silent, Assert.IsType<PageOp.Remove>(due[0]).Id);
        Assert.Equal(waiting, Assert.IsType<PageOp.Show>(due[1]).Toast.Id);
    }

    [Fact]
    public void A_reloaded_page_gets_its_corner_and_every_toast_on_screen_with_the_time_it_has_left()
    {
        var harness = new Harness(seconds: 7);
        var older = harness.Show(Harness.Titled("older"));

        harness.Now = 4_000;
        var newer = harness.Show(Harness.Titled("newer"));
        harness.Pump();
        harness.Now = 5_000;
        harness.Center.PageReady();
        var ops = harness.Pump();
        var shows = ops.OfType<PageOp.Show>().ToList();

        Assert.Equal(ToastPosition.TopRight, Assert.IsType<PageOp.Place>(ops[0]).Position);
        Assert.Equal(new[] { older, newer }, shows.Select(show => show.Toast.Id));
        Assert.Equal(new[] { 2_000, 6_000 }, shows.Select(show => show.Toast.Milliseconds));
    }

    [Fact]
    public void A_reshown_toast_nearly_out_of_time_still_stays_long_enough_to_read()
    {
        var harness = new Harness(seconds: 7);

        harness.Show(Harness.Titled("old"));
        harness.Pump();
        harness.Now = 6_900;
        harness.Center.PageReady();
        var show = Assert.Single(harness.Pump().OfType<PageOp.Show>());

        Assert.Equal(ToastBoard.MinReshowMilliseconds, show.Toast.Milliseconds);
    }
}
