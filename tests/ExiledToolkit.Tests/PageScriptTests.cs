using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>The toast stack toasts.js draws in the game's web UI, fed ops the way the plugin sends them.</summary>
public class PageScriptTests
{
    private const string Stamina = "Not enough stamina";

    private static ToastView View(long id, string title, int count = 1, int milliseconds = 7_000, bool hasClick = false) =>
        new(new ToastId(id), new ToastContent("Survival Log", title, null, ToastKind.Warning, null), count, milliseconds, hasClick);

    private static PageCommand.Gone TimedOut(long id, int count) => new(new ToastId(id), count, false);

    private static PageCommand.Gone Closed(long id, int count) => new(new ToastId(id), count, true);

    private static List<PageCommand.Gone> GoneSent(PageScript page) => page.Sent.OfType<PageCommand.Gone>().ToList();

    // ===== Drawing =====

    [Fact]
    public void New_toasts_stack_newest_on_top_and_a_merge_shows_its_count_on_the_same_box()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)), new PageOp.Show(View(2, "Saved")));
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 3)));

        Assert.Equal(new[] { "Saved", Stamina + " x3" }, page.Labels());
        Assert.Equal(2, page.BoxCount());
    }

    [Fact]
    public void A_replaced_toast_takes_the_old_box_in_its_place_and_answers_to_its_new_id()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, "Organizing 1/3")), new PageOp.Show(View(2, "Unrelated")));
        page.Apply(new PageOp.Replace(new ToastId(1), View(3, "Organizing 2/3", hasClick: true)));
        var labels = page.Labels();
        page.Click(1);

        Assert.Equal(new[] { "Unrelated", "Organizing 2/3" }, labels);
        Assert.Contains(new PageCommand.Click(new ToastId(3)), page.Sent);
        Assert.Equal(new[] { Closed(3, 1) }, GoneSent(page));
    }

    [Fact]
    public void A_removed_box_reports_gone_and_leaves_the_stack_after_its_exit()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Apply(new PageOp.Remove(new ToastId(1)));
        var leaving = page.BoxCount();
        page.Advance(450);

        Assert.Equal(new[] { TimedOut(1, 1) }, GoneSent(page));
        Assert.Empty(page.Labels());
        Assert.Equal(1, leaving);
        Assert.Equal(0, page.BoxCount());
    }

    [Fact]
    public void Closing_a_toast_with_its_x_reports_it_gone_without_running_its_click_action()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina, hasClick: true)));
        page.Click(0, "etk-close");

        Assert.Empty(page.Sent.OfType<PageCommand.Click>());
        Assert.Equal(new[] { Closed(1, 1) }, GoneSent(page));
    }

    /// <summary>A merge the plugin sent before it heard of the close must not bring the box back.</summary>
    [Fact]
    public void A_merge_for_a_box_the_player_closed_is_not_drawn_again()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Click(0, "etk-close");
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 2)));
        page.Advance(450);

        Assert.Empty(page.Labels());
        Assert.Equal(0, page.BoxCount());
        Assert.Equal(new[] { Closed(1, 1) }, GoneSent(page));
    }

    // ===== Time =====

    [Fact]
    public void A_box_that_runs_out_of_time_reports_gone_with_the_count_it_showed()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Advance(3_000);
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 4)));
        page.Advance(6_999);
        var early = GoneSent(page);
        page.Advance(1);

        Assert.Empty(early);
        Assert.Equal(new[] { TimedOut(1, 4) }, GoneSent(page));
    }

    /// <summary>The "not enough stamina" spam case: one more trigger lands just as the box times out.</summary>
    [Fact]
    public void A_merge_for_a_box_on_its_way_out_draws_it_again_with_the_new_count()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Advance(7_000);
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 2)));
        page.Advance(450);
        var labels = page.Labels();
        var count = page.BoxCount();
        page.Advance(7_000);

        Assert.Equal(new[] { Stamina + " x2" }, labels);
        Assert.Equal(1, count);
        Assert.Equal(new[] { TimedOut(1, 1), TimedOut(1, 2) }, GoneSent(page));
    }

    [Fact]
    public void A_merge_for_a_box_the_page_does_not_have_draws_it()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Merge(View(5, Stamina, count: 6)));

        Assert.Equal(new[] { Stamina + " x6" }, page.Labels());
    }

    [Fact]
    public void A_burst_of_merges_starts_the_bump_animation_once_until_it_has_run()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 2)));
        page.Advance(100);
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 3)));
        var duringBump = page.Number("boxes()[0].added['etk-bump']");
        page.Advance(160);
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 4)));

        Assert.Equal(1, duringBump);
        Assert.Equal(2, page.Number("boxes()[0].added['etk-bump']"));
        Assert.Equal(new[] { Stamina + " x4" }, page.Labels());
    }

    // ===== Talking to the plugin =====

    [Fact]
    public void With_the_bridge_already_there_the_page_says_ready_at_once()
    {
        var page = new PageScript();

        Assert.IsType<PageCommand.Ready>(Assert.Single(page.Sent));
    }

    [Fact]
    public void The_ready_message_waits_for_a_bridge_that_arrives_after_the_script()
    {
        var page = new PageScript(hasBridge: false);
        var beforeBridge = page.Sent.Count;

        page.ConnectBridge();

        Assert.Equal(0, beforeBridge);
        Assert.IsType<PageCommand.Ready>(Assert.Single(page.Sent));
    }

    [Fact]
    public void While_boxes_are_up_the_page_repeats_where_they_are_every_second()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Sent.Clear();
        page.Advance(999);
        var beforeBeat = page.Sent.Count;
        page.Advance(1);
        var rects = Assert.IsType<PageCommand.Rects>(Assert.Single(page.Sent));
        var box = Assert.Single(rects.Boxes);

        Assert.Equal(0, beforeBeat);
        Assert.Equal(1600.0 / 1920, box.X, 4);
        Assert.Equal(20.0 / 1080, box.Y, 4);
        Assert.Equal(300.0 / 1920, box.W, 4);
        Assert.Equal(80.0 / 1080, box.H, 4);
    }

    [Fact]
    public void With_no_boxes_up_the_page_stays_quiet()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Apply(new PageOp.Remove(new ToastId(1)));
        page.Advance(450);
        var cleared = Assert.IsType<PageCommand.Rects>(page.Sent[^1]);
        page.Sent.Clear();
        page.Advance(5_000);

        Assert.Empty(cleared.Boxes);
        Assert.Empty(page.Sent);
    }
}
