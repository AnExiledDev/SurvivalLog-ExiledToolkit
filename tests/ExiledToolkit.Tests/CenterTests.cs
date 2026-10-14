using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>What a mod calling the API can count on: records, click actions, failures kept inside, threads, the off switch.</summary>
public class CenterTests
{
    [Fact]
    public void Every_trigger_is_reported_with_what_was_shown_and_when()
    {
        var harness = new Harness();
        harness.Utc = Harness.Start.AddMinutes(3);

        var id = harness.Show(Harness.Stamina() with { OnClick = () => { } });
        harness.Pump();
        var record = Assert.Single(harness.Heard);

        Assert.Equal(new ToastRecord(id, "Survival Log", "Not enough stamina", "Rest first", ToastKind.Warning, "icons/stamina.png", Harness.Start.AddMinutes(3), true, 1, false), record);
    }

    [Fact]
    public void Listeners_hear_about_toasts_only_when_the_main_thread_pumps()
    {
        var harness = new Harness();

        harness.Show(Harness.Stamina());
        var beforePump = harness.Heard.Count;
        harness.Pump();

        Assert.Equal(0, beforePump);
        Assert.Single(harness.Heard);
    }

    [Fact]
    public void A_listener_that_throws_does_not_stop_the_others_and_is_logged()
    {
        var harness = new Harness();
        var heard = new List<ToastRecord>();

        harness.Show(Harness.Stamina());
        harness.Center.Pump(new Action<ToastRecord>[] { _ => throw new InvalidOperationException("boom"), heard.Add });

        Assert.Single(heard);
        Assert.Contains(harness.Logs, line => line.Contains("boom"));
    }

    [Fact]
    public void A_listener_that_throws_on_every_toast_is_logged_in_full_once_then_only_counted()
    {
        var harness = new Harness();
        var listeners = new Action<ToastRecord>[] { _ => throw new InvalidOperationException("boom") };

        for (var i = 0; i < 25; i++)
        {
            harness.Show(Harness.Titled("burst " + i));
            harness.Center.Pump(listeners);
        }

        Assert.Equal(2, harness.Logs.Count);
        Assert.Contains("boom", harness.Logs[0]);
        Assert.Contains("10 times", harness.Logs[1]);
        Assert.DoesNotContain("boom", harness.Logs[1]);
    }

    [Fact]
    public void A_listener_failing_in_a_new_way_is_logged_in_full_again()
    {
        var harness = new Harness();
        var calls = 0;
        var listeners = new Action<ToastRecord>[]
        {
            _ =>
            {
                calls++;

                if (calls == 1) throw new InvalidOperationException("first kind");

                throw new ArgumentException("second kind");
            },
        };

        harness.Show(Harness.Titled("one"));
        harness.Center.Pump(listeners);
        harness.Show(Harness.Titled("two"));
        harness.Center.Pump(listeners);

        Assert.Equal(2, harness.Logs.Count);
        Assert.Contains("first kind", harness.Logs[0]);
        Assert.Contains("second kind", harness.Logs[1]);
    }

    [Fact]
    public void A_click_action_that_throws_is_logged_and_the_game_carries_on()
    {
        var harness = new Harness();
        var id = harness.Show(Harness.Stamina() with { OnClick = () => throw new InvalidOperationException("broken popup") });

        var activated = harness.Center.Activate(id);

        Assert.True(activated);
        Assert.Contains(harness.Logs, line => line.Contains("broken popup"));
    }

    [Fact]
    public void Activating_a_toast_without_a_click_action_does_nothing_and_says_so()
    {
        var harness = new Harness();
        var id = harness.Show(Harness.Stamina());

        Assert.False(harness.Center.Activate(id));
        Assert.False(harness.Center.Activate(new ToastId(9_999)));
    }

    [Fact]
    public void A_toast_can_still_be_opened_after_it_left_the_screen()
    {
        var harness = new Harness();
        var opened = 0;
        var id = harness.Show(Harness.Stamina() with { OnClick = () => opened++ });

        harness.Pump();
        harness.Gone(id);

        Assert.True(harness.Center.Activate(id));
        Assert.Equal(1, opened);
    }

    [Fact]
    public void Only_the_most_recent_500_click_actions_are_kept()
    {
        var harness = new Harness();
        var oldest = harness.Show(Harness.Titled("oldest") with { OnClick = () => { } });

        for (var i = 0; i < ClickActions.DefaultCapacity; i++) harness.Show(Harness.Titled("newer " + i) with { OnClick = () => { } });

        Assert.False(harness.Center.Activate(oldest));
    }

    [Fact]
    public void Activating_from_another_thread_runs_the_action_on_the_next_main_thread_frame()
    {
        var harness = new Harness();
        var opened = 0;
        var id = harness.Show(Harness.Stamina() with { OnClick = () => opened++ });
        harness.IsMainThread = false;

        var accepted = harness.Center.Activate(id);
        var beforeFrame = opened;
        harness.IsMainThread = true;
        harness.Pump();

        Assert.True(accepted);
        Assert.Equal(0, beforeFrame);
        Assert.Equal(1, opened);
    }

    [Fact]
    public void With_toasts_switched_off_nothing_is_drawn_but_every_trigger_is_still_reported_and_clickable()
    {
        var harness = new Harness(isEnabled: false);
        var opened = 0;

        var first = harness.Show(Harness.Stamina() with { OnClick = () => opened++ });
        var second = harness.Show(Harness.Stamina());
        var ops = harness.Pump();
        harness.Center.Activate(first);

        Assert.Empty(ops);
        Assert.NotEqual(first, second);
        Assert.Equal(2, harness.Heard.Count);
        Assert.All(harness.Heard, record => Assert.Equal(1, record.Count));
        Assert.Equal(1, opened);
    }

    [Fact]
    public void A_missing_request_shows_nothing_and_is_logged()
    {
        var harness = new Harness();

        var id = harness.Center.Show(null);

        Assert.True(id.IsNone);
        Assert.Empty(harness.Pump());
        Assert.Single(harness.Logs);
    }

    [Fact]
    public void Empty_body_and_icon_count_as_none()
    {
        var harness = new Harness();

        harness.Show(Harness.Titled("Saved") with { Body = "", Icon = "" });
        harness.Pump();

        Assert.Null(harness.Heard[0].Body);
        Assert.Null(harness.Heard[0].Icon);
    }

    [Theory]
    [InlineData(null, 7_000)]
    [InlineData(3.5, 3_500)]
    [InlineData(0.1, 1_000)]
    [InlineData(500.0, 120_000)]
    [InlineData(double.NaN, 7_000)]
    [InlineData(double.PositiveInfinity, 7_000)]
    public void A_toasts_time_is_its_own_if_usable_else_the_setting_and_always_in_range(double? seconds, int expected)
    {
        var harness = new Harness(seconds: 7);

        harness.Show(Harness.Stamina() with { Seconds = seconds });
        var show = Assert.Single(harness.Pump().OfType<PageOp.Show>());

        Assert.Equal(expected, show.Toast.Milliseconds);
    }

    [Fact]
    public void A_broken_seconds_setting_falls_back_to_seven()
    {
        Assert.Equal(7_000, new ToastSettings(ToastPosition.TopLeft, double.NaN, 4, true).Milliseconds(null));
    }
}
