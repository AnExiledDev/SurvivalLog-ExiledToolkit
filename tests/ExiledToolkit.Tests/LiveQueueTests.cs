using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>What a mod watching its settings can count on: one apply per frame, on the frame's thread, and only for its own settings.</summary>
public class LiveQueueTests
{
    private readonly LiveQueue _queue = new();
    private readonly List<string> _log = new();
    private readonly object _speed = new();
    private readonly object _volume = new();
    private readonly object _other = new();

    [Fact]
    public void A_change_from_another_thread_applies_only_when_the_frame_runs()
    {
        var applied = 0;
        var threadOfApply = -1;
        _queue.Watch(new[] { _speed }, () => { applied++; threadOfApply = Environment.CurrentManagedThreadId; }, "speed");

        Task.Run(() => _queue.Changed(_speed)).Wait();
        var beforeRun = applied;
        _queue.Run(_log.Add);

        Assert.Equal(0, beforeRun);
        Assert.Equal(1, applied);
        Assert.Equal(Environment.CurrentManagedThreadId, threadOfApply);
    }

    [Fact]
    public void Many_changes_in_one_frame_apply_once()
    {
        var applied = 0;
        _queue.Watch(new[] { _speed, _volume }, () => applied++, "both");

        _queue.Changed(_speed);
        _queue.Changed(_volume);
        _queue.Changed(_speed);
        _queue.Run(_log.Add);
        _queue.Run(_log.Add);

        Assert.Equal(1, applied);
    }

    [Fact]
    public void A_change_to_a_setting_nobody_watches_applies_nothing()
    {
        var applied = 0;
        _queue.Watch(new[] { _speed }, () => applied++, "speed");

        _queue.Changed(_other);
        _queue.Run(_log.Add);

        Assert.Equal(0, applied);
    }

    [Fact]
    public void Every_watcher_of_a_setting_applies()
    {
        var heard = new List<string>();
        _queue.Watch(new[] { _speed }, () => heard.Add("a"), "a");
        _queue.Watch(new[] { _speed, _volume }, () => heard.Add("b"), "b");

        _queue.Changed(_speed);
        _queue.Run(_log.Add);

        Assert.Equal(new[] { "a", "b" }, heard);
    }

    [Fact]
    public void A_stopped_watcher_never_applies_even_with_a_change_waiting()
    {
        var applied = 0;
        var watcher = _queue.Watch(new[] { _speed }, () => applied++, "speed");

        _queue.Changed(_speed);
        watcher.Dispose();
        _queue.Run(_log.Add);

        Assert.Equal(0, applied);
    }

    [Fact]
    public void A_setting_stays_watched_until_its_last_watcher_stops()
    {
        var first = _queue.Watch(new[] { _speed }, () => { }, "a");
        var second = _queue.Watch(new[] { _speed }, () => { }, "b");

        first.Dispose();
        var afterFirst = _queue.IsWatched(_speed);
        second.Dispose();

        Assert.True(afterFirst);
        Assert.False(_queue.IsWatched(_speed));
        Assert.False(_queue.IsWatched(_other));
    }

    [Fact]
    public void An_apply_that_throws_is_logged_and_the_others_still_apply()
    {
        var applied = 0;
        _queue.Watch(new[] { _speed }, () => throw new InvalidOperationException("boom"), "broken");
        _queue.Watch(new[] { _speed }, () => applied++, "fine");

        _queue.Changed(_speed);
        _queue.Run(_log.Add);

        Assert.Equal(1, applied);
        Assert.Contains(_log, line => line.Contains("broken") && line.Contains("boom"));
    }

    [Fact]
    public void An_apply_that_changes_its_own_setting_runs_again_next_frame_not_in_the_same_one()
    {
        var applied = 0;
        _queue.Watch(new[] { _speed }, () => { applied++; _queue.Changed(_speed); }, "self");

        _queue.Changed(_speed);
        _queue.Run(_log.Add);
        var afterFirstFrame = applied;
        _queue.Run(_log.Add);

        Assert.Equal(1, afterFirstFrame);
        Assert.Equal(2, applied);
    }
}
