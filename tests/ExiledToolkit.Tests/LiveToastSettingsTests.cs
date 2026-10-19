using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>Toast settings changed in the Mod Manager while the game runs take effect without a restart.</summary>
public class LiveToastSettingsTests
{
    [Fact]
    public void A_new_position_moves_the_stack_at_the_next_frame()
    {
        var harness = new Harness();

        harness.Center.Configure(new ToastSettings(ToastPosition.BottomLeft, 7, 4, true));
        var place = Assert.Single(harness.Pump().OfType<PageOp.Place>());

        Assert.Equal(ToastPosition.BottomLeft, place.Position);
    }

    [Fact]
    public void Settings_that_keep_the_position_send_no_move()
    {
        var harness = new Harness();

        harness.Center.Configure(new ToastSettings(ToastPosition.TopRight, 30, 4, true));

        Assert.Empty(harness.Pump().OfType<PageOp.Place>());
    }

    [Fact]
    public void A_page_that_reloads_after_a_move_gets_the_new_position()
    {
        var harness = new Harness();

        harness.Center.Configure(new ToastSettings(ToastPosition.BottomRight, 7, 4, true));
        harness.Pump();
        harness.Center.PageReady();
        var place = Assert.Single(harness.Pump().OfType<PageOp.Place>());

        Assert.Equal(ToastPosition.BottomRight, place.Position);
    }

    [Fact]
    public void Raising_the_on_screen_limit_shows_waiting_toasts_at_once()
    {
        var harness = new Harness(maxVisible: 1);

        harness.Show(Harness.Titled("one"));
        var waiting = harness.Show(Harness.Titled("two"));
        harness.Pump();
        harness.Center.Configure(new ToastSettings(ToastPosition.TopRight, 7, 2, true));
        var shown = Assert.Single(harness.Pump().OfType<PageOp.Show>());

        Assert.Equal(waiting, shown.Toast.Id);
    }

    [Fact]
    public void Lowering_the_on_screen_limit_makes_new_toasts_wait_and_leaves_the_shown_ones_alone()
    {
        var harness = new Harness(maxVisible: 2);

        harness.Show(Harness.Titled("one"));
        harness.Show(Harness.Titled("two"));
        harness.Pump();
        harness.Center.Configure(new ToastSettings(ToastPosition.TopRight, 7, 1, true));
        harness.Show(Harness.Titled("three"));
        var ops = harness.Pump();

        Assert.Empty(ops.OfType<PageOp.Show>());
        Assert.Empty(ops.OfType<PageOp.Remove>());
    }

    [Fact]
    public void A_new_time_applies_to_the_next_toast()
    {
        var harness = new Harness(seconds: 7);

        harness.Center.Configure(new ToastSettings(ToastPosition.TopRight, 20, 4, true));
        harness.Show(Harness.Titled("one"));
        var shown = Assert.Single(harness.Pump().OfType<PageOp.Show>());

        Assert.Equal(20_000, shown.Toast.Milliseconds);
    }

    [Fact]
    public void Turning_toasts_off_stops_drawing_new_ones_but_listeners_still_hear_them()
    {
        var harness = new Harness();

        harness.Center.Configure(new ToastSettings(ToastPosition.TopRight, 7, 4, false));
        harness.Show(Harness.Titled("one"));
        var ops = harness.Pump();

        Assert.Empty(ops.OfType<PageOp.Show>());
        Assert.Single(harness.Heard);
    }
}
